using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.Protocols;
using System.Net;

namespace Darah.ECM.Infrastructure.Identity;

/// <summary>
/// LDAP / Active Directory authentication provider.
/// Supports: Windows AD, OpenLDAP, Azure AD (via LDAP).
/// Falls back to local DB auth if LDAP unavailable.
/// </summary>
public interface ILdapAuthService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password,
        CancellationToken ct = default);
    Task<LdapUserInfo?> GetUserInfoAsync(string username, CancellationToken ct = default);
    Task SyncUserFromLdapAsync(string username, CancellationToken ct = default);
}

public record LdapAuthResult(
    bool Success,
    string? Username,
    string? DisplayNameAr,
    string? DisplayNameEn,
    string? Email,
    string? Department,
    IEnumerable<string> Groups,
    string? Error = null);

public record LdapUserInfo(
    string SamAccountName,
    string DisplayName,
    string Email,
    string? Department,
    string? Title,
    IEnumerable<string> MemberOf);

public sealed class LdapAuthService : ILdapAuthService
{
    private readonly IConfiguration _config;
    private readonly EcmDbContext _db;
    private readonly ILogger<LdapAuthService> _log;

    private string LdapHost => _config["LDAP:Host"] ?? "ldap.darah.gov.sa";
    private int LdapPort => int.Parse(_config["LDAP:Port"] ?? "389");
    private string BaseDn => _config["LDAP:BaseDn"] ?? "dc=darah,dc=gov,dc=sa";
    private string BindDn => _config["LDAP:BindDn"] ?? "";
    private string BindPassword => _config["LDAP:BindPassword"] ?? "";
    private bool UseSsl => bool.Parse(_config["LDAP:UseSsl"] ?? "false");

    public LdapAuthService(IConfiguration config, EcmDbContext db,
        ILogger<LdapAuthService> log)
    {
        _config = config;
        _db = db;
        _log = log;
    }

    public async Task<LdapAuthResult> AuthenticateAsync(string username,
        string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config["LDAP:Host"]))
            return new LdapAuthResult(false, null, null, null, null, null, [],
                "LDAP not configured");

        try
        {
            var identifier = new LdapDirectoryIdentifier(LdapHost, LdapPort);
            using var connection = new LdapConnection(identifier)
            {
                AuthType = AuthType.Basic,
                Timeout = TimeSpan.FromSeconds(10)
            };

            if (UseSsl)
                connection.SessionOptions.SecureSocketLayer = true;

            connection.SessionOptions.ProtocolVersion = 3;

            // Bind with service account to find user DN
            connection.Credential = new NetworkCredential(BindDn, BindPassword);
            connection.Bind();

            // Search for user
            var userDn = await FindUserDnAsync(connection, username);
            if (userDn == null)
                return new LdapAuthResult(false, null, null, null, null, null, [],
                    "User not found in directory");

            // Authenticate with user credentials
            using var userConn = new LdapConnection(identifier)
            {
                AuthType = AuthType.Basic,
                Timeout = TimeSpan.FromSeconds(10)
            };
            userConn.Credential = new NetworkCredential(userDn, password);

            try
            {
                userConn.Bind();
            }
            catch (LdapException)
            {
                return new LdapAuthResult(false, null, null, null, null, null, [],
                    "Invalid credentials");
            }

            // Get user attributes
            var userInfo = await GetUserAttributesAsync(connection, userDn);
            if (userInfo == null)
                return new LdapAuthResult(false, null, null, null, null, null, []);

            _log.LogInformation("LDAP auth success for {User}", username);

            // Sync user to local DB
            await SyncUserFromLdapAsync(username, ct);

            return new LdapAuthResult(
                true, userInfo.SamAccountName,
                userInfo.DisplayName, // Arabic display name
                userInfo.DisplayName,
                userInfo.Email,
                userInfo.Department,
                userInfo.MemberOf);
        }
        catch (Exception ex) when (ex is not LdapException)
        {
            _log.LogError(ex, "LDAP authentication error for {User}", username);
            return new LdapAuthResult(false, null, null, null, null, null, [],
                "LDAP service unavailable");
        }
    }

    public async Task<LdapUserInfo?> GetUserInfoAsync(string username,
        CancellationToken ct)
    {
        try
        {
            var identifier = new LdapDirectoryIdentifier(LdapHost, LdapPort);
            using var connection = new LdapConnection(identifier);
            connection.Credential = new NetworkCredential(BindDn, BindPassword);
            connection.Bind();

            var dn = await FindUserDnAsync(connection, username);
            if (dn == null) return null;

            return await GetUserAttributesAsync(connection, dn);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to get LDAP user info for {User}", username);
            return null;
        }
    }

    public async Task SyncUserFromLdapAsync(string username, CancellationToken ct)
    {
        var ldapUser = await GetUserInfoAsync(username, ct);
        if (ldapUser == null) return;

        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username.ToLower(), ct);

        if (existing != null) return; // User exists, skip

        // Create user from LDAP data
        var hash = System.Convert.ToHexString(
            System.Security.Cryptography.SHA256.Create()
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    Guid.NewGuid().ToString()))); // Random hash — LDAP auth only

        var user = User.Create(
            username.ToLower(),
            ldapUser.Email,
            hash,
            ldapUser.DisplayName,
            createdBy: 0,
            fullNameEn: ldapUser.DisplayName);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Synced LDAP user {User} to local DB", username);
    }

    private async Task<string?> FindUserDnAsync(LdapConnection conn,
        string username)
    {
        var filter = $"(&(objectClass=user)(sAMAccountName={username}))";
        var request = new SearchRequest(
            BaseDn, filter, SearchScope.Subtree,
            "distinguishedName", "sAMAccountName");

        var response = await Task.Run(() =>
            (SearchResponse)conn.SendRequest(request));

        return response.Entries.Count > 0
            ? response.Entries[0].DistinguishedName
            : null;
    }

    private async Task<LdapUserInfo?> GetUserAttributesAsync(
        LdapConnection conn, string userDn)
    {
        var request = new SearchRequest(
            userDn, "(objectClass=*)", SearchScope.Base,
            "sAMAccountName", "displayName", "mail",
            "department", "title", "memberOf");

        var response = await Task.Run(() =>
            (SearchResponse)conn.SendRequest(request));

        if (response.Entries.Count == 0) return null;

        var entry = response.Entries[0];
        var groups = entry.Attributes["memberOf"]?
            .GetValues(typeof(string))
            .Cast<string>()
            .Select(g => g.Split(',')[0].Replace("CN=", ""))
            .ToList() ?? [];

        return new LdapUserInfo(
            entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? "",
            entry.Attributes["displayName"]?[0]?.ToString() ?? "",
            entry.Attributes["mail"]?[0]?.ToString() ?? "",
            entry.Attributes["department"]?[0]?.ToString(),
            entry.Attributes["title"]?[0]?.ToString(),
            groups);
    }
}

/// <summary>
/// Azure AD / OIDC SSO integration.
/// Validates Azure AD tokens and maps claims to ECM users.
/// </summary>
public sealed class AzureAdSsoService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<AzureAdSsoService> _log;

    public AzureAdSsoService(IConfiguration config, HttpClient http,
        ILogger<AzureAdSsoService> log)
    {
        _config = config;
        _http = http;
        _log = log;
    }

    public async Task<AzureAdUser?> ValidateTokenAsync(string accessToken,
        CancellationToken ct)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", accessToken);

            var response = await _http.GetAsync(
                "https://graph.microsoft.com/v1.0/me", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var user = System.Text.Json.JsonSerializer.Deserialize<AzureAdUser>(
                json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            _log.LogInformation("Azure AD SSO: {User}", user?.UserPrincipalName);
            return user;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Azure AD token validation failed");
            return null;
        }
    }
}

public record AzureAdUser(
    string Id,
    string DisplayName,
    string UserPrincipalName,
    string? Mail,
    string? JobTitle,
    string? Department,
    string? OfficeLocation);

/// <summary>
/// SSO Controller — handles Azure AD and LDAP auth endpoints.
/// </summary>
[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/sso")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class SsoController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly ILdapAuthService _ldap;
    private readonly IConfiguration _config;

    public SsoController(ILdapAuthService ldap, IConfiguration config)
    {
        _ldap = ldap;
        _config = config;
    }

    /// <summary>Login via LDAP/AD credentials.</summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("ldap/login")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> LdapLogin(
        [Microsoft.AspNetCore.Mvc.FromBody] LdapLoginRequest req,
        System.Threading.CancellationToken ct)
    {
        var result = await _ldap.AuthenticateAsync(req.Username, req.Password, ct);

        if (!result.Success)
            return Unauthorized(new { success = false, message = result.Error });

        return Ok(new { success = true, data = result });
    }

    /// <summary>Azure AD OAuth2 redirect URL.</summary>
    [Microsoft.AspNetCore.Mvc.HttpGet("azure/redirect")]
    public Microsoft.AspNetCore.Mvc.IActionResult AzureRedirect()
    {
        var tenantId = _config["AzureAD:TenantId"];
        var clientId = _config["AzureAD:ClientId"];
        var redirectUri = _config["AzureAD:RedirectUri"];
        var scope = "openid profile email User.Read";

        var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
                  $"?client_id={clientId}" +
                  $"&response_type=code" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&state={Guid.NewGuid()}";

        return Redirect(url);
    }
}

public record LdapLoginRequest(string Username, string Password);
