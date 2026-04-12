using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Interfaces.Services;

/// <summary>
/// File storage abstraction — provider-agnostic.
/// Implementations: LocalFileStorageService, S3FileStorageService (future).
/// </summary>
public interface IFileStorageService
{
    string ProviderName { get; }
    Task<string>  StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream>  RetrieveAsync(string storageKey, CancellationToken ct = default);
    Task          DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool>    ExistsAsync(string storageKey, CancellationToken ct = default);
    Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default);
}

/// <summary>Email service abstraction.</summary>
public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedAsync(string toEmail, string templateCode, Dictionary<string, string> variables, CancellationToken ct = default);
}

/// <summary>Audit service — append-only, never modifies existing records.</summary>
public interface IAuditService
{
    Task LogAsync(
        string  eventType,
        string? entityType     = null,
        string? entityId       = null,
        object? oldValues      = null,
        object? newValues      = null,
        string  severity       = "Info",
        bool    isSuccessful   = true,
        string? failureReason  = null,
        string? additionalInfo = null,
        CancellationToken ct   = default);
}

/// <summary>
/// Event bus — dispatches domain events to registered IEventHandler implementations.
/// In-process for Modular Monolith phase; swap to MassTransit for Microservices.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}

/// <summary>Event handler contract — implement one handler per event type per concern.</summary>
public interface IEventHandler<in T> where T : class
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}

/// <summary>Generates unique, formatted document numbers per document type.</summary>
public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(int documentTypeId, CancellationToken ct = default);
}

/// <summary>Generates unique, formatted workspace numbers per workspace type.</summary>

/// <summary>Current authenticated user — populated from JWT claims by CurrentUserService.</summary>
public interface ICurrentUser
{
    int                  UserId          { get; }
    string               Username        { get; }
    string               Email           { get; }
    string               FullNameAr      { get; }
    string?              FullNameEn      { get; }
    string               Language        { get; }
    string?              IPAddress       { get; }
    string?              SessionId       { get; }
    bool                 IsAuthenticated { get; }
    IEnumerable<string>  Permissions     { get; }
    bool HasPermission(string permission);
}

/// <summary>External system connector — one implementation per vendor.</summary>
public interface IExternalSystemConnector
{
    string SystemCode { get; }
    Task<bool>   TestConnectionAsync(CancellationToken ct = default);
    Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default);
    Task<bool>   PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default);
    Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default);
}

public sealed record ExternalObjectPayload(
    string ObjectId,
    string ObjectType,
    IReadOnlyDictionary<string, object?> Fields,
    DateTime FetchedAt);

/// <summary>File validation service — validates content type against file signature (magic bytes).</summary>
public interface IFileValidationService
{
    Task<FileValidationResult> ValidateAsync(Stream fileStream, string fileName, string declaredContentType, CancellationToken ct = default);
}

public sealed record FileValidationResult(
    bool   IsValid,
    string? FailureReason,
    string? DetectedMimeType);
