// ============================================================
// FILE STORAGE — Provider abstraction
// ============================================================
namespace Darah.ECM.Infrastructure.FileStorage;

using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Local file system storage — production-ready with streaming and organized paths.
/// Swap for S3StorageService or AzureBlobStorageService without changing callers.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public string ProviderName => "LocalFileSystem";

    public LocalFileStorageService(IConfiguration config, ILogger<LocalFileStorageService> logger)
    {
        _basePath = config["Storage:LocalPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "ecm-storage");
        Directory.CreateDirectory(_basePath);
        _logger = logger;
    }

    public async Task<string> StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        // Organized: year/month/day/guid.ext — prevents filesystem inode exhaustion
        var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
        var fullPath = BuildPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 81_920, useAsync: true);
        await stream.CopyToAsync(output, 81_920, ct);

        _logger.LogInformation("Stored file: {Key} ({Size} bytes)", key, output.Length);
        return key;
    }

    public async Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default)
    {
        var path = BuildPath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Storage key not found: {storageKey}");
        return new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 81_920, useAsync: true);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = BuildPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(BuildPath(storageKey)));

    public Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default)
        // Local storage doesn't support signed URLs — return null; callers use /download endpoint
        => Task.FromResult<string?>(null);

    private string BuildPath(string key)
        => Path.GetFullPath(Path.Combine(_basePath, key.Replace('/', Path.DirectorySeparatorChar)));
}

/// <summary>AWS S3 storage service — plug-in replacement for local storage.</summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly string _bucketName;
    private readonly string _region;
    private readonly ILogger<S3FileStorageService> _logger;

    public string ProviderName => "AWSS3";

    public S3FileStorageService(IConfiguration config, ILogger<S3FileStorageService> logger)
    {
        _bucketName = config["Storage:S3:BucketName"] ?? throw new InvalidOperationException("S3 bucket not configured");
        _region = config["Storage:S3:Region"] ?? "me-south-1";
        _logger = logger;
    }

    public async Task<string> StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        // TODO: Inject AmazonS3Client and call PutObjectAsync
        // var key = $"ecm/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
        // await _s3.PutObjectAsync(new PutObjectRequest { BucketName = _bucketName, Key = key, InputStream = stream, ContentType = contentType }, ct);
        _logger.LogInformation("S3 store: {File}", fileName);
        throw new NotImplementedException("Install AWSSDK.S3 and implement.");
    }

    public Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => throw new NotImplementedException();

    public async Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default)
    {
        // var request = new GetPreSignedUrlRequest { BucketName = _bucketName, Key = storageKey, Expires = DateTime.UtcNow.Add(expiry) };
        // return _s3.GetPreSignedURL(request);
        throw new NotImplementedException();
    }
}

// ============================================================
// MESSAGING — In-process event bus
// ============================================================
namespace Darah.ECM.Infrastructure.Messaging;

using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-process event bus — publishes to all registered IEventHandler<T> implementations.
/// Replace with MassTransit/RabbitMQ for distributed messaging in microservices phase.
/// </summary>
public class InProcessEventBus : IEventBus
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(IServiceProvider sp, ILogger<InProcessEventBus> logger)
        { _sp = sp; _logger = logger; }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        var eventType = typeof(T).Name;
        using var scope = _sp.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<T>>();
        foreach (var handler in handlers)
        {
            try
            {
                _logger.LogDebug("Dispatching {Event} to {Handler}", eventType, handler.GetType().Name);
                await handler.HandleAsync(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler.GetType().Name, eventType);
                // Don't rethrow — event bus failures should not break the command
            }
        }
    }

    public void Subscribe<T, THandler>() where T : class where THandler : IEventHandler<T>
    {
        // Registration done via DI — nothing needed here for in-process bus
    }
}

// ============================================================
// SECURITY — CurrentUserService, AuthService
// ============================================================
namespace Darah.ECM.Infrastructure.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class CurrentUserService : ICurrentUser, ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public int UserId => int.TryParse(Principal?.FindFirstValue("uid"), out var id) ? id : 0;
    int? ICurrentUserAccessor.UserId => IsAuthenticated ? UserId : null;
    public string Username => Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string FullNameAr => Principal?.FindFirstValue("name_ar") ?? string.Empty;
    public string? FullNameEn => Principal?.FindFirstValue("name_en");
    public string Language => Principal?.FindFirstValue("lang") ?? "ar";
    public string? SessionId => Principal?.FindFirstValue("sid");
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public IEnumerable<string> Permissions => Principal?.FindAll("perm").Select(c => c.Value) ?? Enumerable.Empty<string>();
    public string? IPAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public interface ITokenService
{
    string GenerateAccessToken(int userId, string username, string email, string nameAr, string? nameEn,
        string lang, string sessionId, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashToken(string token);
    bool ValidateToken(string token, out ClaimsPrincipal? principal);
}

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(int userId, string username, string email, string nameAr,
        string? nameEn, string lang, string sessionId, IEnumerable<string> permissions)
    {
        var key = GetSigningKey();
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new("uid", userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new("name_ar", nameAr),
            new("lang", lang),
            new("sid", sessionId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrEmpty(nameEn)) claims.Add(new("name_en", nameEn));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string token)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    public bool ValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = GetSigningKey(),
                ValidateIssuer = true, ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            return true;
        }
        catch { return false; }
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var secret = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
    }
}

// ============================================================
// LOGGING — Structured audit service
// ============================================================
namespace Darah.ECM.Infrastructure.Logging;

using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

public class AuditService : IAuditService
{
    private readonly EcmDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(EcmDbContext context, ICurrentUser currentUser, ILogger<AuditService> logger)
        { _context = context; _currentUser = currentUser; _logger = logger; }

    public async Task LogAsync(string eventType, string? entityType = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, string severity = "Info",
        bool isSuccessful = true, string? failureReason = null, string? additionalInfo = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = AuditLog.Create(
                eventType: eventType,
                entityType: entityType,
                entityId: entityId,
                userId: _currentUser.IsAuthenticated ? _currentUser.UserId : null,
                username: _currentUser.IsAuthenticated ? _currentUser.Username : null,
                ipAddress: _currentUser.IPAddress,
                oldValues: oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                newValues: newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
                additionalInfo: additionalInfo,
                severity: severity,
                isSuccessful: isSuccessful,
                failureReason: failureReason,
                sessionId: _currentUser.SessionId);

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for event {EventType}", eventType);
        }
    }
}
