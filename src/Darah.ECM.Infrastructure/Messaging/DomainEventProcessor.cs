using Darah.ECM.Infrastructure.OCR;
using Darah.ECM.Infrastructure.Persistence;
using Darah.ECM.Infrastructure.Search;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Messaging;

/// <summary>
/// Event-driven document processing pipeline.
/// Triggered on upload → OCR → Index → Notify
/// Uses Hangfire with PostgreSQL for persistence (no lost jobs on restart).
/// </summary>
public sealed class DocumentProcessingPipeline
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<DocumentProcessingPipeline> _log;

    public DocumentProcessingPipeline(
        IBackgroundJobClient jobs,
        ILogger<DocumentProcessingPipeline> log)
    {
        _jobs = jobs;
        _log = log;
    }

    /// <summary>Trigger full processing pipeline after document upload.</summary>
    public string TriggerProcessing(Guid documentId, string storageKey)
    {
        // Chain: OCR → Index → Notify (each step depends on previous)
        var ocrJobId = _jobs.Enqueue<OcrJob>(j =>
            j.ProcessAsync(documentId, storageKey, CancellationToken.None));

        var indexJobId = _jobs.ContinueJobWith<SearchIndexJob>(ocrJobId, j =>
            j.IndexAsync(documentId, CancellationToken.None));

        _jobs.ContinueJobWith<NotifyJob>(indexJobId, j =>
            j.SendProcessingCompleteAsync(documentId, CancellationToken.None));

        _log.LogInformation(
            "Processing pipeline queued for document {DocId}", documentId);

        return ocrJobId;
    }
}

[Queue("ocr")]
public sealed class OcrJob
{
    private readonly IOcrService _ocr;
    private readonly EcmDbContext _db;
    private readonly ILogger<OcrJob> _log;

    public OcrJob(IOcrService ocr, EcmDbContext db, ILogger<OcrJob> log)
    {
        _ocr = ocr; _db = db; _log = log;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ProcessAsync(Guid documentId, string storageKey,
        CancellationToken ct)
    {
        _log.LogInformation("OCR processing document {DocId}", documentId);

        // Load file from storage
        var filePath = Path.Combine("/app/ecm-storage", storageKey);
        if (!File.Exists(filePath))
        {
            _log.LogWarning("File not found for OCR: {Path}", filePath);
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var result = await _ocr.ExtractTextAsync(stream, "application/pdf", ct);

        if (!result.Success)
        {
            _log.LogWarning("OCR failed for {DocId}: {Error}", documentId, result.Error);
            return;
        }

        // Store extracted text for search indexing
        await _db.Database.ExecuteSqlRawAsync("""
            UPDATE "Documents"
            SET "ExtractedText" = {1},
                "DetectedLanguage" = {2},
                "OcrCompletedAt" = NOW(),
                "OcrConfidence" = {3}
            WHERE "DocumentId" = {0}
            """, documentId, result.ExtractedText,
            result.DetectedLanguage, result.Confidence, ct);

        _log.LogInformation(
            "OCR complete for {DocId}: {Chars} chars, {Lang}, confidence {Conf:P0}",
            documentId, result.ExtractedText.Length,
            result.DetectedLanguage, result.Confidence);
    }
}

[Queue("search-index")]
public sealed class SearchIndexJob
{
    private readonly IFullTextSearchService _search;
    private readonly EcmDbContext _db;
    private readonly ILogger<SearchIndexJob> _log;

    public SearchIndexJob(IFullTextSearchService search, EcmDbContext db,
        ILogger<SearchIndexJob> log)
    {
        _search = search; _db = db; _log = log;
    }

    [AutomaticRetry(Attempts = 5)]
    public async Task IndexAsync(Guid documentId, CancellationToken ct)
    {
        var text = await _db.Database
            .SqlQueryRaw<string>(
                "SELECT \"ExtractedText\" FROM \"Documents\" WHERE \"DocumentId\" = {0}",
                documentId)
            .FirstOrDefaultAsync(ct);

        await _search.IndexDocumentAsync(documentId, text ?? "", ct);
        _log.LogInformation("Document {DocId} indexed for search", documentId);
    }
}

[Queue("notifications")]
public sealed class NotifyJob
{
    private readonly ILogger<NotifyJob> _log;
    public NotifyJob(ILogger<NotifyJob> log) => _log = log;

    public Task SendProcessingCompleteAsync(Guid documentId, CancellationToken ct)
    {
        _log.LogInformation(
            "Document {DocId} fully processed and ready", documentId);
        // SignalR hub notification goes here
        return Task.CompletedTask;
    }
}
