namespace Darah.ECM.Domain.Interfaces.Services;

public interface IFileStorageService
{
    string ProviderName { get; }
    Task<string> StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool>   ExistsAsync(string storageKey, CancellationToken ct = default);
    /// <summary>Returns a time-limited URL for direct download (null for local storage).</summary>
    Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedAsync(string toEmail, string templateCode,
        Dictionary<string, string> variables, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(string eventType, string? entityType = null, string? entityId = null,
        object? oldValues = null, object? newValues = null,
        string severity = "Info", bool isSuccessful = true,
        string? failureReason = null, string? additionalInfo = null,
        CancellationToken ct = default);
}

public interface IEventBus
{
    /// <summary>Publishes an event to all registered handlers.</summary>
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}

public interface IEventHandler<T> where T : class
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}

public interface ICurrentUser
{
    int    UserId       { get; }
    string Username     { get; }
    string Email        { get; }
    string FullNameAr   { get; }
    string? FullNameEn  { get; }
    string Language     { get; }
    string? IPAddress   { get; }
    string? SessionId   { get; }
    bool   IsAuthenticated { get; }
    IEnumerable<string> Permissions { get; }
    bool HasPermission(string permission);
}

public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(int documentTypeId, CancellationToken ct = default);
}

public interface IWorkspaceNumberGenerator
{
    Task<string> GenerateAsync(int workspaceTypeId, CancellationToken ct = default);
}

/// <summary>External system connector — one implementation per vendor (SAP, CRM, HR...).</summary>
public interface IExternalSystemConnector
{
    string SystemCode { get; }
    Task<bool>                         TestConnectionAsync(CancellationToken ct = default);
    Task<ExternalObjectPayload?>       FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default);
    Task<bool>                         PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default);
    Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default);
}

public record ExternalObjectPayload(
    string ObjectId,
    string ObjectType,
    Dictionary<string, object?> Fields,
    DateTime FetchedAt);
