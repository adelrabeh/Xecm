using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Interfaces.Services;


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
