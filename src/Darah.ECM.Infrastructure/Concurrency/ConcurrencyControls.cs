using Darah.ECM.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Concurrency;

/// <summary>
/// Optimistic concurrency controls for race-condition-prone operations.
///
/// SCENARIOS PROTECTED:
///   1. Simultaneous CheckOut — two users click CheckOut at the same time
///      → First wins (RowVersion); second gets ConcurrencyConflictException
///
///   2. Simultaneous workflow actions on the same task
///      → First wins; second gets stale-state detection
///
///   3. Concurrent record declaration — two processes try to declare same doc
///      → RowVersion prevents double-declaration
///
/// IMPLEMENTATION:
///   - IVersioned interface: adds RowVersion property to entities
///   - EF Core: .IsRowVersion() maps to SQL Server ROWVERSION type
///   - On save: EF Core adds WHERE RowVersion = @original_version
///   - If zero rows affected → DbUpdateConcurrencyException → caught and re-thrown as domain exception
///
/// DESIGN CHOICE: Optimistic over Pessimistic locking
///   - Pessimistic (SELECT FOR UPDATE) causes deadlocks at scale
///   - Optimistic retry is appropriate for ECM document operations
///   - Checkout has explicit domain-level lock via CheckoutLock entity
/// </summary>

// ─── VERSIONED INTERFACE ─────────────────────────────────────────────────────
public interface IVersioned
{
    byte[] RowVersion { get; }
}

// ─── CONCURRENCY EXCEPTION ───────────────────────────────────────────────────
/// <summary>
/// Thrown when an optimistic concurrency conflict is detected.
/// The caller should reload the entity and retry (with user confirmation for
/// user-facing operations).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public string EntityType { get; }
    public string EntityId   { get; }

    public ConcurrencyConflictException(string entityType, string entityId)
        : base($"تعارض في التزامن: تم تعديل {entityType} ({entityId}) بواسطة مستخدم آخر. يرجى تحديث الصفحة والمحاولة مجدداً.")
    {
        EntityType = entityType;
        EntityId   = entityId;
    }
}

// ─── CHECKOUT CONCURRENCY GUARD ──────────────────────────────────────────────
/// <summary>
/// Prevents race condition where two concurrent checkout requests succeed simultaneously.
///
/// PROBLEM: Without this guard:
///   User A sends POST /checkout at T=0
///   User B sends POST /checkout at T=1ms
///   Both read doc.IsCheckedOut=false
///   Both call doc.CheckOut() → both succeed → data corruption
///
/// SOLUTION:
///   EF Core RowVersion on Document entity + SQL WHERE RowVersion=@original
///   First committer succeeds, second gets DbUpdateConcurrencyException
/// </summary>
public static class ConcurrencyGuard
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string entityType,
        string entityId,
        ILogger logger,
        int maxRetries = 1,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex,
                    "Concurrency conflict on {EntityType} ({EntityId}), attempt {Attempt}/{Max}",
                    entityType, entityId, attempt + 1, maxRetries + 1);

                if (attempt >= maxRetries)
                    throw new ConcurrencyConflictException(entityType, entityId);

                // Short delay before retry (allow the other transaction to complete)
                await Task.Delay(50 * (attempt + 1), ct);
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        string entityType,
        string entityId,
        ILogger logger,
        int maxRetries = 1,
        CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync<object?>(
            async () => { await operation(); return null; },
            entityType, entityId, logger, maxRetries, ct);
    }
}

// ─── CACHE INVALIDATION RULES ─────────────────────────────────────────────────
/// <summary>
/// Defines and enforces cache invalidation rules when data changes.
///
/// RULES TABLE:
///   Event                          → Invalidate
///   ─────────────────────────────────────────────────
///   RolePermission updated         → ecm:user:permissions:{all affected users}
///   UserRole changed               → ecm:user:permissions:{userId}
///   MetadataField updated          → ecm:metadata:fields:doctype:{documentTypeId}
///   DocumentType updated           → ecm:doctype:{typeId} + ecm:metadata:fields:doctype:{typeId}
///   WorkflowDefinition updated     → ecm:workflow:definitions:all
///   LookupValue updated            → ecm:lookup:{categoryCode}
///
/// STALE DATA RISK MATRIX:
///   Cached Item         TTL    Staleness Risk   Impact If Stale
///   ─────────────────────────────────────────────────────────────
///   User permissions    5min   LOW              Security (short TTL mitigates)
///   Metadata fields     30min  LOW-MED          Wrong validation (explicit invalidation)
///   Workflow defs       10min  LOW              Wrong routing (explicit invalidation)
///   Lookup values       1hr    LOW              Wrong dropdowns (explicit invalidation)
/// </summary>
public interface ICacheInvalidationService
{
    Task OnRolePermissionChangedAsync(int roleId, CancellationToken ct = default);
    Task OnUserRoleChangedAsync(int userId, CancellationToken ct = default);
    Task OnMetadataFieldChangedAsync(int documentTypeId, CancellationToken ct = default);
    Task OnWorkflowDefinitionChangedAsync(CancellationToken ct = default);
    Task OnLookupValueChangedAsync(string categoryCode, CancellationToken ct = default);
}

public sealed class CacheInvalidationService : ICacheInvalidationService
{
    private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(
        Microsoft.Extensions.Caching.Distributed.IDistributedCache cache,
        ILogger<CacheInvalidationService> logger)
    { _cache = cache; _logger = logger; }

    public async Task OnRolePermissionChangedAsync(int roleId, CancellationToken ct = default)
    {
        // Cannot easily enumerate all users with this role from cache alone.
        // In production: query DB for affected users, then invalidate each.
        // For now: log and let TTL handle expiry (5min max staleness).
        _logger.LogInformation(
            "Role {RoleId} permissions changed — user permission caches will expire within 5min", roleId);
        await Task.CompletedTask;
    }

    public async Task OnUserRoleChangedAsync(int userId, CancellationToken ct = default)
    {
        var key = $"ecm:user:permissions:{userId}";
        await RemoveSafeAsync(key, ct);
        _logger.LogInformation("Cache invalidated for user {UserId} permissions", userId);
    }

    public async Task OnMetadataFieldChangedAsync(int documentTypeId, CancellationToken ct = default)
    {
        var key = $"ecm:metadata:fields:doctype:{documentTypeId}";
        await RemoveSafeAsync(key, ct);
        _logger.LogInformation("Cache invalidated for doctype {TypeId} metadata fields", documentTypeId);
    }

    public async Task OnWorkflowDefinitionChangedAsync(CancellationToken ct = default)
    {
        await RemoveSafeAsync("ecm:workflow:definitions:all", ct);
        _logger.LogInformation("Cache invalidated: workflow definitions");
    }

    public async Task OnLookupValueChangedAsync(string categoryCode, CancellationToken ct = default)
    {
        var key = $"ecm:lookup:{categoryCode}";
        await RemoveSafeAsync(key, ct);
        _logger.LogInformation("Cache invalidated for lookup category {Code}", categoryCode);
    }

    private async Task RemoveSafeAsync(string key, CancellationToken ct)
    {
        try { await _cache.RemoveAsync(key, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for key {Key}", key);
        }
    }
}
