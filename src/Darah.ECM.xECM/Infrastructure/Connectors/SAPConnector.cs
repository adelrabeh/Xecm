using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Connectors;

/// <summary>SAP OData v4 connector. Fetches and pushes via SAP REST APIs.</summary>
public sealed class SAPConnector : IExternalSystemConnector
{
    public string SystemCode => "SAP_PROD";
    private readonly HttpClient _http;
    private readonly ILogger<SAPConnector> _logger;

    public SAPConnector(HttpClient http, ILogger<SAPConnector> logger) { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try { var r = await _http.GetAsync("/sap/opu/odata/sap/", ct); return r.IsSuccessStatusCode; }
        catch (Exception ex) { _logger.LogWarning(ex, "SAP connection test failed"); return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        var url = BuildUrl(objectType, objectId);
        if (url is null) { _logger.LogWarning("No URL mapping for SAP objectType={Type}", objectType); return null; }
        try
        {
            var r = await _http.GetAsync(url, ct);
            if (!r.IsSuccessStatusCode) { _logger.LogWarning("SAP fetch failed: {Status} for {Type}/{Id}", r.StatusCode, objectType, objectId); return null; }
            var json = await r.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var fields = new Dictionary<string, object?>();
            if (doc.RootElement.TryGetProperty("d", out var d))
                foreach (var p in d.EnumerateObject())
                    fields[p.Name] = p.Value.ToString();
            return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
        }
        catch (Exception ex) { _logger.LogError(ex, "SAP fetch error for {Type}/{Id}", objectType, objectId); return null; }
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var url = BuildUrl(objectType, objectId);
        if (url is null) return false;
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var req = new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        var r = await _http.SendAsync(req, ct);
        return r.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());

    private static string? BuildUrl(string t, string id) => t switch
    {
        "WBSElement"    => $"/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement('{id}')",
        "PurchaseOrder" => $"/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('{id}')",
        "Contract"      => $"/sap/opu/odata/sap/API_CONTRACT/A_Contract('{id}')",
        _ => null
    };
}

/// <summary>Salesforce REST API v58 connector.</summary>
public sealed class SalesforceConnector : IExternalSystemConnector
{
    public string SystemCode => "SF_CRM";
    private readonly HttpClient _http;
    private readonly ILogger<SalesforceConnector> _logger;

    public SalesforceConnector(HttpClient http, ILogger<SalesforceConnector> logger) { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try { var r = await _http.GetAsync("/services/data/v58.0/", ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"/services/data/v58.0/sobjects/{objectType}/{objectId}", ct);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadAsStringAsync(ct);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var fields = new Dictionary<string, object?>();
        foreach (var p in doc.RootElement.EnumerateObject())
            if (p.Name != "attributes") fields[p.Name] = p.Value.ToString();
        return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/services/data/v58.0/sobjects/{objectType}/{objectId}")
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        var r = await _http.SendAsync(req, ct);
        return r.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
}
