using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.FileStorage.Local;

/// <summary>
/// Local file system storage service.
///
/// PATH SAFETY HARDENING:
///   - All storage keys are resolved via GetFullPath which eliminates ".." traversal.
///   - A secondary boundary check verifies the resolved path starts with the configured
///     base path — any attempt to escape the root is rejected with an exception.
///   - Files are organized as yyyy/MM/dd/{guid}{ext} to avoid filesystem inode exhaustion.
///   - The base path is stored as a canonical full path (GetFullPath) to ensure
///     consistent prefix comparison regardless of how it was configured.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;   // canonical, trailing-separator normalized
    private readonly ILogger<LocalFileStorageService> _logger;

    public string ProviderName => "LocalFileSystem";

    public LocalFileStorageService(IConfiguration config, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var configured = config["Storage:LocalPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "ecm-storage");

        // Normalize to a canonical full path with a guaranteed trailing separator
        // so prefix comparison is unambiguous (e.g., /data/ecm never matches /data/ecm2).
        _basePath = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar)
                  + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(_basePath);
        _logger.LogInformation("LocalFileStorageService initialized at {BasePath}", _basePath);
    }

    public async Task<string> StoreAsync(Stream stream, string fileName,
        string contentType, CancellationToken ct = default)
    {
        var ext        = Path.GetExtension(fileName).ToLowerInvariant();
        var storageKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
        var fullPath   = ResolveSafe(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 81_920, useAsync: true);
        stream.Position = 0;
        await stream.CopyToAsync(output, 81_920, ct);

        _logger.LogInformation("File stored: key={Key} size={Size}", storageKey, output.Length);
        return storageKey;
    }

    public async Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(storageKey);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found for key: {storageKey}");

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 81_920, useAsync: true);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: key={Key}", storageKey);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(storageKey);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default)
        // Local storage does not support signed URLs.
        // File access is always via the /api/v1/documents/{id}/download endpoint.
        => Task.FromResult<string?>(null);

    // ─── Path safety ──────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a storage key to an absolute path and asserts it is inside the
    /// configured base path. Throws if the resolved path would escape the root.
    /// </summary>
    private string ResolveSafe(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));

        // Normalize the key: replace forward slash with OS separator, strip leading separator
        var normalized = storageKey.TrimStart('/', '\\')
                                   .Replace('/', Path.DirectorySeparatorChar)
                                   .Replace('\\', Path.DirectorySeparatorChar);

        // Resolve to a canonical absolute path (eliminates ".." and ".")
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, normalized));

        // ★ Critical boundary check: ensure the resolved path is inside _basePath ★
        if (!fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical(
                "Path traversal attempt blocked. Key={Key} ResolvedTo={Path} Base={Base}",
                storageKey, fullPath, _basePath);
            throw new UnauthorizedAccessException(
                $"Storage key '{storageKey}' resolves outside the configured storage root.");
        }

        return fullPath;
    }
}
