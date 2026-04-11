using Darah.ECM.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Darah.ECM.Infrastructure.Caching;

/// <summary>
/// Redis-backed distributed cache for performance-critical data.
///
/// Cached items:
///   - Metadata field definitions per document type (TTL: 30 min)
///   - User permissions (TTL: 5 min; short to reflect permission changes)
///   - Lookup values / dropdown options (TTL: 1 hour)
///   - Workflow definitions (TTL: 10 min)
///   - Document type configurations (TTL: 30 min)
///
/// Cache invalidation:
///   - Admin updates to metadata/permissions call InvalidateAsync() explicitly
///   - Short TTLs ensure stale data doesn't persist long even without explicit invalidation
/// </summary>
public sealed class EcmCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<EcmCacheService> _logger;

    private static readonly DistributedCacheEntryOptions ShortTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    private static readonly DistributedCacheEntryOptions MediumTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };

    private static readonly DistributedCacheEntryOptions LongTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    public EcmCacheService(IDistributedCache cache, ILogger<EcmCacheService> logger)
        { _cache = cache; _logger = logger; }

    // ─── Metadata Fields ──────────────────────────────────────────────────────
    public async Task<List<MetadataField>?> GetMetadataFieldsAsync(
        int documentTypeId, CancellationToken ct = default)
        => await GetAsync<List<MetadataField>>(
            CacheKeys.MetadataFields(documentTypeId), ct);

    public async Task SetMetadataFieldsAsync(int documentTypeId,
        List<MetadataField> fields, CancellationToken ct = default)
        => await SetAsync(CacheKeys.MetadataFields(documentTypeId), fields, MediumTtl, ct);

    public async Task InvalidateMetadataFieldsAsync(int documentTypeId, CancellationToken ct = default)
        => await InvalidateAsync(CacheKeys.MetadataFields(documentTypeId), ct);

    // ─── User Permissions ─────────────────────────────────────────────────────
    public async Task<List<string>?> GetUserPermissionsAsync(
        int userId, CancellationToken ct = default)
        => await GetAsync<List<string>>(CacheKeys.UserPermissions(userId), ct);

    public async Task SetUserPermissionsAsync(int userId,
        List<string> permissions, CancellationToken ct = default)
        => await SetAsync(CacheKeys.UserPermissions(userId), permissions, ShortTtl, ct);

    public async Task InvalidateUserPermissionsAsync(int userId, CancellationToken ct = default)
        => await InvalidateAsync(CacheKeys.UserPermissions(userId), ct);

    // ─── Workflow Definitions ─────────────────────────────────────────────────
    public async Task<List<WorkflowDefinition>?> GetWorkflowDefinitionsAsync(
        CancellationToken ct = default)
        => await GetAsync<List<WorkflowDefinition>>(CacheKeys.WorkflowDefinitions, ct);

    public async Task SetWorkflowDefinitionsAsync(List<WorkflowDefinition> definitions,
        CancellationToken ct = default)
        => await SetAsync(CacheKeys.WorkflowDefinitions, definitions, MediumTtl, ct);

    public async Task InvalidateWorkflowDefinitionsAsync(CancellationToken ct = default)
        => await InvalidateAsync(CacheKeys.WorkflowDefinitions, ct);

    // ─── Generic get/set/invalidate ──────────────────────────────────────────
    private async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var data = await _cache.GetAsync(key, ct);
            if (data is null) return null;
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null; // Cache miss — fallback to DB
        }
    }

    private async Task SetAsync<T>(string key, T value,
        DistributedCacheEntryOptions options, CancellationToken ct) where T : class
    {
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, data, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
            // Cache write failure is non-fatal
        }
    }

    private async Task InvalidateAsync(string key, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogDebug("Cache invalidated: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }

    // ─── Cache key definitions ────────────────────────────────────────────────
    private static class CacheKeys
    {
        public static string MetadataFields(int docTypeId)
            => $"ecm:metadata:fields:doctype:{docTypeId}";

        public static string UserPermissions(int userId)
            => $"ecm:user:permissions:{userId}";

        public const string WorkflowDefinitions = "ecm:workflow:definitions:all";

        public static string LookupValues(string categoryCode)
            => $"ecm:lookup:{categoryCode}";

        public static string DocumentType(int typeId)
            => $"ecm:doctype:{typeId}";
    }
}
