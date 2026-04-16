using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(
        new Uri(ctx.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200"))
    {
        IndexFormat = "ecm-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true,
    }));

// ─── Elasticsearch ────────────────────────────────────────────────────────────
var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var esClient = new ElasticsearchClient(new Uri(esUri));
builder.Services.AddSingleton(esClient);
builder.Services.AddScoped<ISearchService, ElasticsearchService>();

// ─── MassTransit (RabbitMQ consumer) ─────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DocumentIndexConsumer>();
    x.AddConsumer<DocumentDeleteConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rmq = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        cfg.Host(rmq, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

// ─── OpenTelemetry ────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["OTLP:Endpoint"] ?? "http://localhost:4317")));

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri(esUri), name: "elasticsearch");

var app = builder.Build();

// Ensure Elasticsearch index exists on startup
await EnsureIndexAsync(esClient);

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapGet("/", () => Results.Ok(new { service = "DARAH ECM Search Service", version = "1.0" }));

Log.Information("Search Service starting — ES: {Uri}", esUri);
app.Run();

// ─── Ensure Index Setup ───────────────────────────────────────────────────────
static async Task EnsureIndexAsync(ElasticsearchClient client)
{
    var exists = await client.Indices.ExistsAsync("ecm-documents");
    if (exists.Exists) return;

    await client.Indices.CreateAsync<DocumentIndex>("ecm-documents", c => c
        .Mappings(m => m
            .Properties(p => p
                .Text(t => t.TitleAr, tf => tf.Analyzer("arabic"))
                .Text(t => t.TitleEn, tf => tf.Analyzer("english"))
                .Text(t => t.ExtractedText, tf => tf
                    .Analyzer("arabic")
                    .Fields(f => f.Text("english", tf2 => tf2.Analyzer("english"))))
                .Keyword(k => k.Status)
                .Keyword(k => k.DocumentType)
                .Date(d => d.CreatedAt)
            ))
        .Settings(s => s
            .NumberOfShards(2)
            .NumberOfReplicas(1)));

    Log.Information("Elasticsearch index 'ecm-documents' created");
}

// ─── Document Index Model ─────────────────────────────────────────────────────
public record DocumentIndex(
    string Id,
    string TitleAr,
    string? TitleEn,
    string? ExtractedText,
    string Status,
    string? DocumentType,
    string? DetectedLanguage,
    DateTime CreatedAt,
    int CreatedBy,
    IEnumerable<string> Tags);

// ─── Search Service Interface ─────────────────────────────────────────────────
public interface ISearchService
{
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct);
    Task IndexAsync(DocumentIndex document, CancellationToken ct);
    Task DeleteAsync(string documentId, CancellationToken ct);
}

public record SearchRequest(
    string Query,
    int Page = 1,
    int Size = 20,
    string? Status = null,
    string? Language = null,
    DateTime? From = null,
    DateTime? To = null);

public record SearchResponse(
    IEnumerable<SearchHit> Hits,
    long Total,
    double MaxScore,
    TimeSpan Elapsed);

public record SearchHit(
    string DocumentId,
    string TitleAr,
    string? TitleEn,
    string Status,
    double Score,
    string? Highlight,
    DateTime CreatedAt);

// ─── Elasticsearch Implementation ─────────────────────────────────────────────
public sealed class ElasticsearchService : ISearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchService> _log;
    private const string INDEX = "ecm-documents";

    public ElasticsearchService(ElasticsearchClient client,
        ILogger<ElasticsearchService> log)
    {
        _client = client;
        _log = log;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest req, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await _client.SearchAsync<DocumentIndex>(s => s
            .Index(INDEX)
            .From((req.Page - 1) * req.Size)
            .Size(req.Size)
            .Query(q => q
                .Bool(b => b
                    .Must(BuildQuery(req.Query))
                    .Filter(BuildFilters(req.Status, req.From, req.To))))
            .Highlight(h => h
                .Fields(f => f
                    .Add("titleAr", new HighlightField())
                    .Add("extractedText", new HighlightField
                    {
                        NumberOfFragments = 2,
                        FragmentSize = 150
                    })))
            .Sort(so => so
                .Score(sc => sc.Order(SortOrder.Desc))
                .Field(f => f.CreatedAt, fd => fd.Order(SortOrder.Desc))), ct);

        sw.Stop();

        if (!response.IsValidResponse)
        {
            _log.LogError("ES search failed: {Error}", response.ElasticsearchServerError);
            return new SearchResponse([], 0, 0, sw.Elapsed);
        }

        var hits = response.Hits.Select(h => new SearchHit(
            h.Source!.Id, h.Source.TitleAr, h.Source.TitleEn,
            h.Source.Status, h.Score ?? 0,
            h.Highlight?.GetValueOrDefault("extractedText")?.FirstOrDefault()
            ?? h.Highlight?.GetValueOrDefault("titleAr")?.FirstOrDefault(),
            h.Source.CreatedAt));

        return new SearchResponse(hits, response.Total, response.MaxScore ?? 0, sw.Elapsed);
    }

    public async Task IndexAsync(DocumentIndex doc, CancellationToken ct)
    {
        var res = await _client.IndexAsync(doc, i => i.Index(INDEX).Id(doc.Id), ct);
        if (!res.IsValidResponse)
            _log.LogError("Index failed for {Id}: {Error}", doc.Id, res.ElasticsearchServerError);
        else
            _log.LogInformation("Indexed document {Id}", doc.Id);
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct)
    {
        await _client.DeleteAsync(INDEX, documentId, ct);
        _log.LogInformation("Deleted document {Id} from index", documentId);
    }

    private static Action<QueryDescriptor<DocumentIndex>> BuildQuery(string query) =>
        q => q.MultiMatch(mm => mm
            .Query(query)
            .Fields(new[]
            {
                "titleAr^3",      // Arabic title: highest weight
                "titleEn^2",      // English title
                "extractedText",  // OCR text
                "tags"
            })
            .Type(TextQueryType.BestFields)
            .Fuzziness(new Fuzziness("AUTO")));

    private static Action<QueryDescriptor<DocumentIndex>>[] BuildFilters(
        string? status, DateTime? from, DateTime? to)
    {
        var filters = new List<Action<QueryDescriptor<DocumentIndex>>>();
        if (status != null)
            filters.Add(q => q.Term(t => t.Status, status));
        if (from.HasValue || to.HasValue)
            filters.Add(q => q.DateRange(dr => dr
                .Field(f => f.CreatedAt)
                .Gte(from).Lte(to)));
        return filters.ToArray();
    }
}

// ─── RabbitMQ Consumers ───────────────────────────────────────────────────────
public sealed class DocumentIndexConsumer : IConsumer<DocumentIndexedEvent>
{
    private readonly ISearchService _search;
    public DocumentIndexConsumer(ISearchService search) => _search = search;

    public async Task Consume(ConsumeContext<DocumentIndexedEvent> ctx)
    {
        var e = ctx.Message;
        await _search.IndexAsync(new DocumentIndex(
            e.DocumentId.ToString(), e.TitleAr, e.TitleEn,
            e.ExtractedText, e.Status, e.DocumentType,
            e.Language, e.CreatedAt, e.CreatedBy,
            e.Tags ?? []), ctx.CancellationToken);
    }
}

public sealed class DocumentDeleteConsumer : IConsumer<DocumentDeletedEvent>
{
    private readonly ISearchService _search;
    public DocumentDeleteConsumer(ISearchService search) => _search = search;

    public Task Consume(ConsumeContext<DocumentDeletedEvent> ctx)
        => _search.DeleteAsync(ctx.Message.DocumentId.ToString(),
            ctx.CancellationToken);
}

// ─── Events (shared contract) ─────────────────────────────────────────────────
public record DocumentIndexedEvent(
    Guid DocumentId, string TitleAr, string? TitleEn,
    string? ExtractedText, string Status, string? DocumentType,
    string? Language, DateTime CreatedAt, int CreatedBy,
    string[]? Tags);

public record DocumentDeletedEvent(Guid DocumentId);

// ─── Search Controller ────────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/search")]
public sealed class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    public SearchController(ISearchService search) => _search = search;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { success = false, message = "Query is required" });

        var result = await _search.SearchAsync(
            new SearchRequest(q, page, size, status), ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                hits = result.Hits,
                total = result.Total,
                page,
                size,
                elapsedMs = result.Elapsed.TotalMilliseconds
            }
        });
    }
}
