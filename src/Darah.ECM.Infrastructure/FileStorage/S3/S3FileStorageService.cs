using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.FileStorage.S3;

/// <summary>
/// AWS S3 storage service — FUTURE-READY STUB ONLY.
///
/// STATUS: NOT IMPLEMENTED. This class provides the correct interface contract
/// and documents the integration approach so it can be completed without
/// breaking any callers (which depend only on IFileStorageService).
///
/// TO IMPLEMENT:
///   1. Install: AWSSDK.S3 (v3.7+) and AWSSDK.Extensions.NETCore.Setup
///   2. Register AmazonS3Client in DI with credentials from IConfiguration/IAM Role
///   3. Replace NotImplementedException bodies with real SDK calls
///   4. Add appsettings: Storage:Provider = "AWSS3", Storage:S3:BucketName, Storage:S3:Region
///
/// SWAP: Change Storage:Provider appsetting value from "LocalFileSystem" to "AWSS3"
///       in appsettings.Production.json — no code changes required in Application layer.
/// </summary>
public sealed class S3FileStorageService : IFileStorageService
{
    public string ProviderName => "AWSS3";

    private readonly string _bucketName;
    private readonly string _region;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(IConfiguration config, ILogger<S3FileStorageService> logger)
    {
        _bucketName = config["Storage:S3:BucketName"]
            ?? throw new InvalidOperationException("Storage:S3:BucketName is not configured.");
        _region = config["Storage:S3:Region"] ?? "me-south-1";
        _logger = logger;
        _logger.LogWarning("S3FileStorageService is NOT YET IMPLEMENTED. Configure AWSSDK.S3 to activate.");
    }

    public Task<string> StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
        => throw new NotImplementedException(
            "S3 storage is not yet implemented. Install AWSSDK.S3 and implement PutObjectAsync.");

    public Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException(
            "S3 storage is not yet implemented. Install AWSSDK.S3 and implement GetObjectAsync.");

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException(
            "S3 storage is not yet implemented. Install AWSSDK.S3 and implement DeleteObjectAsync.");

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException(
            "S3 storage is not yet implemented. Install AWSSDK.S3 and implement GetObjectMetadataAsync.");

    public Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default)
        => throw new NotImplementedException(
            "S3 storage is not yet implemented. Install AWSSDK.S3 and implement GetPreSignedURL.");

    /*
     * REFERENCE IMPLEMENTATION SKETCH (requires AWSSDK.S3 NuGet package):
     *
     * private readonly IAmazonS3 _s3;  // injected via DI
     *
     * public async Task<string> StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct)
     * {
     *     var ext = Path.GetExtension(fileName).ToLowerInvariant();
     *     var key = $"ecm/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
     *     await _s3.PutObjectAsync(new PutObjectRequest
     *     {
     *         BucketName  = _bucketName,
     *         Key         = key,
     *         InputStream = stream,
     *         ContentType = contentType,
     *         ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
     *     }, ct);
     *     return key;
     * }
     *
     * public async Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct)
     * {
     *     var response = await _s3.GetObjectAsync(_bucketName, storageKey, ct);
     *     return response.ResponseStream;
     * }
     *
     * public async Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct)
     * {
     *     var request = new GetPreSignedUrlRequest
     *     {
     *         BucketName = _bucketName,
     *         Key        = storageKey,
     *         Expires    = DateTime.UtcNow.Add(expiry),
     *         Protocol   = Protocol.HTTPS
     *     };
     *     return _s3.GetPreSignedURL(request);
     * }
     */
}
