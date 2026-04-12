using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Connectors.SAP;

/// <summary>SAP OData v4 connector for business objects: WBSElement, PurchaseOrder, Contract, Vendor, CostCenter</summary>
public sealed class SAPConnector : IExternalSystemConnector
{
    public string SystemCode => "SAP_PROD";

    private readonly HttpClient _http;
    private readonly ILogger<SAPConnector> _logger;

    private static readonly IReadOnlyDictionary<string, string> EntitySetMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WBSElement"]    = "/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement('{id}')",
            ["PurchaseOrder"] = "/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('{id}')",
            ["Contract"]      = "/sap/opu/odata/sap/API_CONTRACT/A_Contract('{id}')",
            ["Vendor"]        = "/sap/opu/odata/sap/API_BUSINESS_PARTNER/A_Supplier('{id}')",
            ["CostCenter"]    = "/sap/opu/odata/sap/API_COSTCENTER_SRV/CostCenterCollection('{id}')"
        };

    public SAPConnector(HttpClient http, ILogger<SAPConnector> logger)
        { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try { var r = await _http.GetAsync("/sap/opu/odata/sap/", ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(
        string objectType, string objectId, CancellationToken ct = default)
    {
        if (!EntitySetMap.TryGetValue(objectType, out var template)) return null;
        var endpoint = template.Replace("'{id}'", $"'{objectId}'");
        try
        {
            var r = await _http.GetAsync(endpoint, ct);
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadAsStringAsync(ct);
            var doc  = System.Text.Json.JsonDocument.Parse(json);
            var fields = new Dictionary<string, object?>();
            var root = doc.RootElement.TryGetProperty("d", out var d) ? d : doc.RootElement;
            foreach (var p in root.EnumerateObject())
                if (!p.Name.StartsWith("__", StringComparison.Ordinal))
                    fields[p.Name] = p.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                        ? null : p.Value.ToString();
            return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
        }
        catch (Exception ex) { _logger.LogError(ex, "SAP fetch error {Type}/{Id}", objectType, objectId); return null; }
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId,
        Dictionary<string, object> fields, CancellationToken ct = default)
    {
        if (!EntitySetMap.TryGetValue(objectType, out var template)) return false;
        var endpoint = template.Replace("'{id}'", $"'{objectId}'");
        var json     = System.Text.Json.JsonSerializer.Serialize(fields);
        var content  = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var request  = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var r = await _http.SendAsync(request, ct);
        return r.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
        string objectType, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
}

namespace Darah.ECM.xECM.Infrastructure.Connectors.Salesforce
{
    /// <summary>Salesforce REST API v58 connector: Account, Opportunity, Case, Contact, Contract</summary>
    public sealed class SalesforceConnector : IExternalSystemConnector
    {
        public string SystemCode => "SF_CRM";
        private readonly HttpClient _http;
        private readonly ILogger<SalesforceConnector> _logger;
    
        public SalesforceConnector(HttpClient http, ILogger<SalesforceConnector> logger)
            { _http = http; _logger = logger; }
    
        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try { var r = await _http.GetAsync("/services/data/v58.0/", ct); return r.IsSuccessStatusCode; }
            catch { return false; }
        }
    
        public async Task<ExternalObjectPayload?> FetchObjectAsync(
            string objectType, string objectId, CancellationToken ct = default)
        {
            try
            {
                var r = await _http.GetAsync($"/services/data/v58.0/sobjects/{objectType}/{objectId}", ct);
                if (!r.IsSuccessStatusCode) return null;
                var json   = await r.Content.ReadAsStringAsync(ct);
                var doc    = System.Text.Json.JsonDocument.Parse(json);
                var fields = new Dictionary<string, object?>();
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Name != "attributes")
                        fields[p.Name] = p.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                            ? null : p.Value.ToString();
                return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
            }
            catch (Exception ex) { _logger.LogError(ex, "SF fetch error {Type}/{Id}", objectType, objectId); return null; }
        }
    
        public async Task<bool> PushUpdateAsync(string objectType, string objectId,
            Dictionary<string, object> fields, CancellationToken ct = default)
        {
            var json    = System.Text.Json.JsonSerializer.Serialize(fields);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"/services/data/v58.0/sobjects/{objectType}/{objectId}") { Content = content };
            var r = await _http.SendAsync(request, ct);
            return r.IsSuccessStatusCode;
        }
    
        public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
            string objectType, DateTime since, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
    }
}

namespace Darah.ECM.xECM.Infrastructure.Connectors.Generic
{
    /// <summary>Generic REST connector for any HTTP-based external system (Oracle HR, Dynamics, etc.)</summary>
    public sealed class GenericRestConnector : IExternalSystemConnector
    {
        public string SystemCode { get; }
        private readonly HttpClient _http;
        private readonly ILogger<GenericRestConnector> _logger;
        private readonly Dictionary<string, string> _urlTemplates = new();
    
        public string UpdateMethod    { get; set; } = "PATCH";
        public string? ResponseDataPath { get; set; }
    
        public GenericRestConnector(string systemCode, HttpClient http,
            ILogger<GenericRestConnector> logger)
            { SystemCode = systemCode; _http = http; _logger = logger; }
    
        public void AddUrlTemplate(string objectType, string template)
            => _urlTemplates[objectType] = template;
    
        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try { var r = await _http.GetAsync("/", ct); return r.IsSuccessStatusCode; }
            catch { return false; }
        }
    
        public async Task<ExternalObjectPayload?> FetchObjectAsync(
            string objectType, string objectId, CancellationToken ct = default)
        {
            if (!_urlTemplates.TryGetValue(objectType, out var template)) return null;
            var url = template.Replace("{id}", objectId);
            try
            {
                var r = await _http.GetAsync(url, ct);
                if (!r.IsSuccessStatusCode) return null;
                var json   = await r.Content.ReadAsStringAsync(ct);
                var doc    = System.Text.Json.JsonDocument.Parse(json);
                var fields = new Dictionary<string, object?>();
                foreach (var p in doc.RootElement.EnumerateObject())
                    fields[p.Name] = p.Value.ToString();
                return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
            }
            catch (Exception ex) { _logger.LogError(ex, "GenericREST error {Sys}/{Type}/{Id}", SystemCode, objectType, objectId); return null; }
        }
    
        public async Task<bool> PushUpdateAsync(string objectType, string objectId,
            Dictionary<string, object> fields, CancellationToken ct = default)
        {
            if (!_urlTemplates.TryGetValue(objectType, out var template)) return false;
            var url     = template.Replace("{id}", objectId);
            var method  = UpdateMethod == "PUT" ? HttpMethod.Put : HttpMethod.Patch;
            var json    = System.Text.Json.JsonSerializer.Serialize(new { data = fields });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var r       = await _http.SendAsync(new HttpRequestMessage(method, url) { Content = content }, ct);
            return r.IsSuccessStatusCode;
        }
    
        public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
            string objectType, DateTime since, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
    }
}
