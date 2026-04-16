using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// ─── OCR Providers ────────────────────────────────────────────────────────────
var useAzure = !string.IsNullOrEmpty(builder.Configuration["Azure:DocumentIntelligence:Endpoint"]);
if (useAzure)
{
    builder.Services.AddSingleton(_ => new DocumentAnalysisClient(
        new Uri(builder.Configuration["Azure:DocumentIntelligence:Endpoint"]!),
        new AzureKeyCredential(builder.Configuration["Azure:DocumentIntelligence:ApiKey"]!)));
    builder.Services.AddScoped<IOcrProvider, AzureOcrProvider>();
    Log.Information("OCR Provider: Azure Document Intelligence");
}
else
{
    builder.Services.AddScoped<IOcrProvider, TesseractOcrProvider>();
    Log.Information("OCR Provider: Tesseract (on-premise)");
}

builder.Services.AddScoped<OcrPipelineService>();

// ─── MassTransit (RabbitMQ) ───────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OcrRequestConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rmq = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        cfg.Host(rmq, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ReceiveEndpoint("ocr-requests", e =>
        {
            e.PrefetchCount = 4; // Process 4 docs concurrently max
            e.ConcurrentMessageLimit = 4;
            e.ConfigureConsumer<OcrRequestConsumer>(ctx);
        });
    });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapGet("/", () => Results.Ok(new { service = "DARAH ECM OCR Service", version = "1.0" }));

Log.Information("OCR Service starting");
app.Run();

// ─── OCR Provider Interface ───────────────────────────────────────────────────
public interface IOcrProvider
{
    Task<OcrResult> ProcessAsync(Stream fileStream, string contentType,
        CancellationToken ct);
}

public record OcrResult(
    bool Success,
    string Text,
    string Language,
    double Confidence,
    IReadOnlyList<OcrPage> Pages,
    IReadOnlyDictionary<string, string> ExtractedMetadata,
    string? Error = null);

public record OcrPage(int Number, string Text, double Confidence,
    IReadOnlyList<OcrTable> Tables);

public record OcrTable(int Rows, int Columns, string[][] Data);

// ─── Azure Provider ───────────────────────────────────────────────────────────
public sealed class AzureOcrProvider : IOcrProvider
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<AzureOcrProvider> _log;

    public AzureOcrProvider(DocumentAnalysisClient client,
        ILogger<AzureOcrProvider> log)
    { _client = client; _log = log; }

    public async Task<OcrResult> ProcessAsync(Stream fileStream,
        string contentType, CancellationToken ct)
    {
        try
        {
            var op = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed, "prebuilt-layout", fileStream, ct);
            var result = op.Value;

            var pages = result.Pages.Select((p, i) =>
            {
                var text = string.Join(" ", p.Words.Select(w => w.Content));
                var confidence = p.Words.Any()
                    ? p.Words.Average(w => w.Confidence) : 0.0;
                var tables = result.Tables
                    .Where(t => t.BoundingRegions.Any(r => r.PageNumber == i + 1))
                    .Select(t => new OcrTable(
                        t.RowCount, t.ColumnCount,
                        ExtractTableData(t)))
                    .ToList();
                return new OcrPage(i + 1, text, confidence, tables);
            }).ToList();

            var allText = string.Join("\n\n", pages.Select(p => p.Text));
            var avgConf = pages.Any() ? pages.Average(p => p.Confidence) : 0;
            var lang = IsArabic(allText) ? "ar" : "en";

            // Extract key-value pairs (metadata)
            var metadata = result.KeyValuePairs
                .Where(kv => kv.Key?.Content != null && kv.Value?.Content != null)
                .ToDictionary(kv => kv.Key!.Content!, kv => kv.Value!.Content!);

            _log.LogInformation(
                "Azure OCR complete: {Pages} pages, {Chars} chars, {Lang}, {Conf:P0}",
                pages.Count, allText.Length, lang, avgConf);

            return new OcrResult(true, allText, lang, avgConf, pages, metadata);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Azure OCR failed");
            return new OcrResult(false, "", "unknown", 0, [], new Dictionary<string, string>(), ex.Message);
        }
    }

    private static string[][] ExtractTableData(DocumentTable table)
    {
        var grid = new string[table.RowCount][];
        for (int i = 0; i < table.RowCount; i++)
            grid[i] = new string[table.ColumnCount];
        foreach (var cell in table.Cells)
            grid[cell.RowIndex][cell.ColumnIndex] = cell.Content;
        return grid;
    }

    private static bool IsArabic(string text) =>
        text.Count(c => c >= '\u0600' && c <= '\u06FF') > text.Length * 0.25;
}

// ─── Tesseract Provider (on-premise) ─────────────────────────────────────────
public sealed class TesseractOcrProvider : IOcrProvider
{
    private readonly ILogger<TesseractOcrProvider> _log;
    public TesseractOcrProvider(ILogger<TesseractOcrProvider> log) => _log = log;

    public async Task<OcrResult> ProcessAsync(Stream fileStream,
        string contentType, CancellationToken ct)
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await using (var fs = File.Create(tmp))
                await fileStream.CopyToAsync(fs, ct);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"{tmp} stdout -l ara+eng --psm 3",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = System.Diagnostics.Process.Start(psi)!;
            var text = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
                return new OcrResult(false, "", "unknown", 0, [],
                    new Dictionary<string, string>(), "Tesseract exited with error");

            var lang = text.Count(c => c >= '\u0600' && c <= '\u06FF') >
                       text.Length * 0.25 ? "ar" : "en";
            var page = new OcrPage(1, text.Trim(), 0.85, []);

            _log.LogInformation("Tesseract OCR: {Chars} chars, {Lang}", text.Length, lang);
            return new OcrResult(true, text.Trim(), lang, 0.85, [page],
                new Dictionary<string, string>());
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tesseract OCR failed");
            return new OcrResult(false, "", "unknown", 0, [],
                new Dictionary<string, string>(), ex.Message);
        }
        finally { File.Delete(tmp); }
    }
}

// ─── OCR Pipeline Service ─────────────────────────────────────────────────────
public sealed class OcrPipelineService
{
    private readonly IOcrProvider _ocr;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<OcrPipelineService> _log;

    public OcrPipelineService(IOcrProvider ocr, IPublishEndpoint bus,
        ILogger<OcrPipelineService> log)
    { _ocr = ocr; _bus = bus; _log = log; }

    public async Task ProcessDocumentAsync(Guid documentId, string storagePath,
        string contentType, CancellationToken ct)
    {
        _log.LogInformation("OCR starting for {DocId}", documentId);

        if (!File.Exists(storagePath))
        {
            _log.LogWarning("File not found: {Path}", storagePath);
            return;
        }

        await using var stream = File.OpenRead(storagePath);
        var result = await _ocr.ProcessAsync(stream, contentType, ct);

        // Publish event for Search Service to index
        await _bus.Publish(new OcrCompletedEvent(
            documentId, result.Success, result.Text,
            result.Language, result.Confidence,
            result.ExtractedMetadata, DateTime.UtcNow), ct);

        _log.LogInformation(
            "OCR complete for {DocId}: success={Ok}, {Chars} chars",
            documentId, result.Success, result.Text.Length);
    }
}

// ─── RabbitMQ Consumer ────────────────────────────────────────────────────────
public sealed class OcrRequestConsumer : IConsumer<OcrRequestedEvent>
{
    private readonly OcrPipelineService _pipeline;
    public OcrRequestConsumer(OcrPipelineService p) => _pipeline = p;

    public Task Consume(ConsumeContext<OcrRequestedEvent> ctx)
        => _pipeline.ProcessDocumentAsync(
            ctx.Message.DocumentId,
            ctx.Message.StoragePath,
            ctx.Message.ContentType,
            ctx.CancellationToken);
}

// ─── Events ───────────────────────────────────────────────────────────────────
public record OcrRequestedEvent(
    Guid DocumentId, string StoragePath, string ContentType);

public record OcrCompletedEvent(
    Guid DocumentId, bool Success, string ExtractedText,
    string Language, double Confidence,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime CompletedAt);
