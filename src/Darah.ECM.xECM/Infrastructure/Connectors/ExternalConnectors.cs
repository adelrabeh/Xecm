using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Connectors;

/// <summary>
/// SAP ERP connector via OData v4 REST API.
/// Supports: WBSElement, PurchaseOrder, Contract object types.
/// Authentication: OAuth2 Client Credentials (configured in ExternalSystems table).
/// </summary>
public sealed class SAPConnector : IExternalSystemConnector
{
    public string SystemCode => "SAP_PROD";

    private readonly HttpClient _http;
    private readonly ILogger<SAPConnector> _logger;

    public SAPConnector(HttpClient http, ILogger<SAPConnector> logger)
        { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/sap/opu/odata/sap/API_PROJECT/", ct);
            _logger.LogInformation("SAP connection test: {Status}", response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP connection test failed");
            return false;
        }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(
        string objectType, string objectId, CancellationToken ct = default)
    {
        var url = BuildODataUrl(objectType, objectId);
        if (url is null)
        {
            _logger.LogWarning("Unknown SAP object type: {Type}", objectType);
            return null;
        }

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP fetch failed: {Status} for {Type}/{Id}",
                    response.StatusCode, objectType, objectId);
                return null;
            }

            var json  = await response.Content.ReadAsStringAsync(ct);
            var root  = System.Text.Json.JsonDocument.Parse(json).RootElement;
            var fields = new Dictionary<string, object?>();

            if (root.TryGetProperty("d", out var d))
                foreach (var prop in d.EnumerateObject())
                    fields[prop.Name] = ExtractValue(prop.Value);

            return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP connector error fetching {Type}/{Id}", objectType, objectId);
            return null;
        }
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId,
        Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var url = BuildODataUrl(objectType, objectId);
        if (url is null) return false;

        var json     = System.Text.Json.JsonSerializer.Serialize(fields);
        var content  = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync(url, content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
        string objectType, DateTime since, CancellationToken ct = default)
    {
        var sinceStr = since.ToString("yyyy-MM-ddTHH:mm:ss");
        var baseUrl  = BuildBaseUrl(objectType);
        if (baseUrl is null) return Enumerable.Empty<ExternalObjectPayload>();

        var url      = $"{baseUrl}?$filter=LastChangedDateTime ge datetime'{sinceStr}'&$top=500";

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<ExternalObjectPayload>();

            var json   = await response.Content.ReadAsStringAsync(ct);
            var root   = System.Text.Json.JsonDocument.Parse(json).RootElement;
            var results = new List<ExternalObjectPayload>();

            if (root.TryGetProperty("d", out var d) && d.TryGetProperty("results", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var fields = new Dictionary<string, object?>();
                    foreach (var p in item.EnumerateObject())
                        fields[p.Name] = ExtractValue(p.Value);

                    var id = fields.GetValueOrDefault("ObjectID")?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(id))
                        results.Add(new ExternalObjectPayload(id, objectType, fields, DateTime.UtcNow));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP delta fetch failed for {Type}", objectType);
            return Enumerable.Empty<ExternalObjectPayload>();
        }
    }

    private static string? BuildODataUrl(string objectType, string objectId) => objectType switch
    {
        "WBSElement"    => $"/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement('{objectId}')",
        "PurchaseOrder" => $"/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('{objectId}')",
        "Contract"      => $"/sap/opu/odata/sap/API_CONTRACT/A_Contract('{objectId}')",
        "Vendor"        => $"/sap/opu/odata/sap/API_BUSINESS_PARTNER/A_BusinessPartner('{objectId}')",
        _               => null
    };

    private static string? BuildBaseUrl(string objectType) => objectType switch
    {
        "WBSElement"    => "/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement",
        "PurchaseOrder" => "/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder",
        _               => null
    };

    private static object? ExtractValue(System.Text.Json.JsonElement element) =>
        element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String  => element.GetString(),
            System.Text.Json.JsonValueKind.Number  => (object)element.GetDecimal(),
            System.Text.Json.JsonValueKind.True    => true,
            System.Text.Json.JsonValueKind.False   => false,
            System.Text.Json.JsonValueKind.Null    => null,
            _                                      => element.ToString()
        };
}

/// <summary>
/// Salesforce CRM connector via REST API v58.0.
/// Supports: Account, Opportunity, Case, Contact object types.
/// Authentication: OAuth2 Connected App (Client Credentials or Authorization Code).
/// </summary>
public sealed class SalesforceConnector : IExternalSystemConnector
{
    public string SystemCode => "SF_CRM";

    private readonly HttpClient _http;
    private readonly ILogger<SalesforceConnector> _logger;

    public SalesforceConnector(HttpClient http, ILogger<SalesforceConnector> logger)
        { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _http.GetAsync("/services/data/v58.0/", ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(
        string objectType, string objectId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync(
            $"/services/data/v58.0/sobjects/{objectType}/{objectId}", ct);

        if (!r.IsSuccessStatusCode)
        {
            _logger.LogWarning("Salesforce fetch failed: {Status} for {Type}/{Id}",
                r.StatusCode, objectType, objectId);
            return null;
        }

        var json   = await r.Content.ReadAsStringAsync(ct);
        var root   = System.Text.Json.JsonDocument.Parse(json).RootElement;
        var fields = new Dictionary<string, object?>();

        foreach (var prop in root.EnumerateObject())
            if (prop.Name != "attributes")
                fields[prop.Name] = prop.Value.ToString();

        return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId,
        Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var req  = new HttpRequestMessage(HttpMethod.Patch,
            $"/services/data/v58.0/sobjects/{objectType}/{objectId}")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        var r = await _http.SendAsync(req, ct);
        return r.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
        string objectType, DateTime since, CancellationToken ct = default)
    {
        // Salesforce supports SOQL delta queries — implement via /services/data/v58.0/query
        // e.g.: SELECT Id, Name, Status FROM Account WHERE LastModifiedDate >= {since}
        _logger.LogInformation("Salesforce delta sync for {Type} since {Since}", objectType, since);
        return Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
    }
}
