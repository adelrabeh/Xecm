using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Darah.ECM.Infrastructure.CoAuthoring;

/// <summary>
/// OnlyOffice Document Server integration for real-time co-authoring.
/// Enables: simultaneous editing, track changes, comments, version history.
/// Compatible with: .docx, .xlsx, .pptx, .odt, .pdf
/// </summary>
public interface ICoAuthoringService
{
    Task<CoAuthorSession> CreateSessionAsync(Guid documentId, int userId,
        string mode, CancellationToken ct);
    Task<bool> HandleCallbackAsync(OnlyOfficeCallback callback, CancellationToken ct);
    string GenerateEditorConfig(Guid documentId, int userId, string mode,
        string fullName, string lang);
}

public record CoAuthorSession(
    string SessionId, Guid DocumentId, string EditorUrl,
    string DocumentKey, string Config);

public record OnlyOfficeCallback(
    int Status, string Key, string Url,
    IEnumerable<string> Users, string? UserData);

public sealed class OnlyOfficeCoAuthoringService : ICoAuthoringService
{
    private readonly EcmDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<OnlyOfficeCoAuthoringService> _log;

    private string ServerUrl => _config["OnlyOffice:ServerUrl"]
        ?? "http://onlyoffice:80";
    private string JwtSecret => _config["OnlyOffice:JwtSecret"]
        ?? "darah-onlyoffice-secret";
    private string DocServerUrl => _config["OnlyOffice:DocumentServerUrl"]
        ?? "https://onlyoffice.darah.gov.sa";

    public OnlyOfficeCoAuthoringService(EcmDbContext db, IConfiguration config,
        HttpClient http, ILogger<OnlyOfficeCoAuthoringService> log)
    {
        _db = db;
        _config = config;
        _http = http;
        _log = log;
    }

    public async Task<CoAuthorSession> CreateSessionAsync(Guid documentId,
        int userId, string mode, CancellationToken ct)
    {
        var doc = await _db.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, ct);

        if (doc == null)
            throw new KeyNotFoundException($"Document {documentId} not found");

        // Create unique document key for OnlyOffice session
        var documentKey = GenerateDocumentKey(documentId, doc.UpdatedAt ?? doc.CreatedAt);
        var config = GenerateEditorConfig(documentId, userId, mode,
            "User", "ar");

        var sessionId = Guid.NewGuid().ToString();

        _log.LogInformation(
            "OnlyOffice session created: doc={DocId}, user={UserId}, mode={Mode}",
            documentId, userId, mode);

        return new CoAuthorSession(
            sessionId, documentId,
            $"{DocServerUrl}/web-apps/apps/api/documents/api.js",
            documentKey, config);
    }

    public async Task<bool> HandleCallbackAsync(OnlyOfficeCallback callback,
        CancellationToken ct)
    {
        _log.LogInformation("OnlyOffice callback: status={Status}, key={Key}",
            callback.Status, callback.Key);

        switch (callback.Status)
        {
            case 2: // Document ready for saving
            case 6: // Document force-saved
                await SaveDocumentFromOnlyOfficeAsync(callback, ct);
                break;

            case 1: // Document being edited
                _log.LogDebug("Document {Key} being edited by {Users}",
                    callback.Key, string.Join(", ", callback.Users));
                break;

            case 4: // Document closed with no changes
                _log.LogInformation("Document {Key} closed without changes", callback.Key);
                break;
        }

        return true;
    }

    public string GenerateEditorConfig(Guid documentId, int userId,
        string mode, string fullName, string lang)
    {
        var apiBaseUrl = _config["Api:BaseUrl"] ?? "https://xecm-production.up.railway.app";
        var docKey = GenerateDocumentKey(documentId, DateTime.UtcNow);

        var config = new
        {
            document = new
            {
                fileType = "docx",
                key = docKey,
                title = documentId.ToString(),
                url = $"{apiBaseUrl}/api/v1/documents/{documentId}/content",
                permissions = new
                {
                    comment   = true,
                    download  = mode != "view",
                    edit      = mode == "edit",
                    print     = true,
                    review    = mode is "edit" or "review",
                    copy      = true,
                    modifyFilter      = false,
                    modifyContentControl = false
                }
            },
            editorConfig = new
            {
                callbackUrl = $"{apiBaseUrl}/api/v1/documents/{documentId}/coauthor/callback",
                lang,
                mode,
                user = new { id = userId.ToString(), name = fullName },
                customization = new
                {
                    autosave     = true,
                    chat         = true,
                    comments     = true,
                    compactHeader = false,
                    feedback     = false,
                    forcesave    = false,
                    help         = false,
                    logo         = new { image = "", url = "" },
                    macros       = false,
                    mentionShare = true,
                    plugins      = false,
                    review       = new { hideReviewDisplay = false, showReviewChanges = true },
                    trackChanges = mode == "edit",
                    uiTheme      = "theme-classic-light",
                    features     = new { roles = new { mode = "disabled" } }
                }
            },
            token = GenerateToken(new { documentId, userId, mode })
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private async Task SaveDocumentFromOnlyOfficeAsync(
        OnlyOfficeCallback callback, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(callback.Url)) return;

        // Download the saved document from OnlyOffice
        var fileBytes = await _http.GetByteArrayAsync(callback.Url, ct);

        // Extract documentId from key
        var keyParts = callback.Key.Split('_');
        if (keyParts.Length < 1 || !Guid.TryParse(keyParts[0], out var docId))
        {
            _log.LogWarning("Invalid document key format: {Key}", callback.Key);
            return;
        }

        // Save to storage
        var storagePath = Path.Combine("/app/ecm-storage", docId.ToString());
        await File.WriteAllBytesAsync(storagePath, fileBytes, ct);

        _log.LogInformation("Document {DocId} saved from OnlyOffice: {Bytes} bytes",
            docId, fileBytes.Length);
    }

    private static string GenerateDocumentKey(Guid documentId, DateTime updatedAt)
    {
        var input = $"{documentId}_{updatedAt:yyyyMMddHHmmss}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{documentId}_{Convert.ToHexString(hash)[..8]}";
    }

    private string GenerateToken(object payload)
    {
        var header = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var body = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(JwtSecret),
                Encoding.UTF8.GetBytes($"{header}.{body}")));
        return $"{header}.{body}.{signature}";
    }
}

// ─── Co-authoring Controller ──────────────────────────────────────────────────
namespace Darah.ECM.API.Controllers.v1;

[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/documents/{documentId:guid}/coauthor")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class CoAuthoringController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly ICoAuthoringService _coAuthor;

    public CoAuthoringController(ICoAuthoringService coAuthor)
        => _coAuthor = coAuthor;

    /// <summary>Create an OnlyOffice editing session.</summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("session")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> CreateSession(
        Guid documentId,
        [Microsoft.AspNetCore.Mvc.FromQuery] string mode = "edit",
        System.Threading.CancellationToken ct = default)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "0");
        var session = await _coAuthor.CreateSessionAsync(documentId, userId, mode, ct);
        return Ok(new { success = true, data = session });
    }

    /// <summary>OnlyOffice callback — save document after editing.</summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("callback")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> Callback(
        Guid documentId,
        [Microsoft.AspNetCore.Mvc.FromBody] OnlyOfficeCallback callback,
        System.Threading.CancellationToken ct)
    {
        var success = await _coAuthor.HandleCallbackAsync(callback, ct);
        return Ok(new { error = success ? 0 : 1 }); // OnlyOffice expects {error: 0}
    }

    /// <summary>Get editor configuration for embedding OnlyOffice.</summary>
    [Microsoft.AspNetCore.Mvc.HttpGet("config")]
    public Microsoft.AspNetCore.Mvc.IActionResult GetConfig(
        Guid documentId,
        [Microsoft.AspNetCore.Mvc.FromQuery] string mode = "view")
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "0");
        var name = User.FindFirst("name_ar")?.Value ?? "مستخدم";
        var lang = User.FindFirst("lang")?.Value ?? "ar";
        var config = _coAuthor.GenerateEditorConfig(documentId, userId, mode, name, lang);
        return Ok(new { success = true, data = config });
    }
}
