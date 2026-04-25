using Darah.ECM.Application.Common.Models;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>
/// Full-text search across Documents, Records, KnowledgeAssets.
/// In production: replace LIKE queries with Elasticsearch/Azure Search.
/// </summary>
[ApiController]
[Route("api/v1/search")]
[Authorize]
[Produces("application/json")]
public sealed class SearchController : ControllerBase
{
    private readonly EcmDbContext _db;
    public SearchController(EcmDbContext db) => _db = db;

    // ── Universal search ────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string? type     = null,   // doc | record | asset | all
        [FromQuery] string? status   = null,
        [FromQuery] string? domain   = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo   = null,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponse<object>.Fail("يجب إدخال نص للبحث"));

        q = q.Trim();
        var results = new List<SearchHit>();

        var scope = type?.ToLower() ?? "all";

        // ── Search Documents ──────────────────────────────────────────────
        if (scope is "all" or "doc")
        {
            var docs = await _db.Documents.AsNoTracking()
                .Where(d => !d.IsDeleted &&
                    (d.TitleAr.Contains(q) || d.TitleEn!.Contains(q) ||
                     d.Summary!.Contains(q) || d.Tags!.Contains(q)))
                .OrderByDescending(d => d.UpdatedAt)
                .Take(50)
                .Select(d => new SearchHit
                {
                    Id         = d.DocumentId.ToString(),
                    Type       = "doc",
                    TypeAr     = "وثيقة",
                    TypeIcon   = "📄",
                    Title      = d.TitleAr,
                    TitleEn    = d.TitleEn,
                    Summary    = d.Summary,
                    Status     = d.Status.ToString(),
                    Date       = d.CreatedAt,
                    Tags       = d.Tags,
                    Score      = ScoreDoc(d.TitleAr, d.Summary, d.Tags, q),
                    Url        = $"/documents/{d.DocumentId}",
                    Highlights = BuildHighlights(q, d.TitleAr, d.Summary, d.Tags),
                })
                .ToListAsync(ct);
            results.AddRange(docs);
        }

        // ── Search Records ────────────────────────────────────────────────
        if (scope is "all" or "record")
        {
            var recs = await _db.Records.AsNoTracking()
                .Where(r =>
                    r.TitleAr.Contains(q) || (r.TitleEn != null && r.TitleEn.Contains(q)) ||
                    (r.Description != null && r.Description.Contains(q)) ||
                    (r.Tags != null && r.Tags.Contains(q)) ||
                    (r.MetadataJson != null && r.MetadataJson.Contains(q)))
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(r => new SearchHit
                {
                    Id       = r.RecordId.ToString(),
                    Type     = "record",
                    TypeAr   = "سجل",
                    TypeIcon = "🗂",
                    Title    = r.TitleAr,
                    TitleEn  = r.TitleEn,
                    Summary  = r.Description,
                    Status   = r.Status.ToString(),
                    Date     = r.CreatedAt,
                    Tags     = r.Tags,
                    Score    = ScoreDoc(r.TitleAr, r.Description, r.Tags, q),
                    Url      = $"/records/{r.RecordId}",
                    Highlights = BuildHighlights(q, r.TitleAr, r.Description, r.Tags),
                })
                .ToListAsync(ct);
            results.AddRange(recs);
        }

        // ── Search Knowledge Assets ────────────────────────────────────────
        if (scope is "all" or "asset")
        {
            var assets = await _db.KnowledgeAssets.AsNoTracking()
                .Where(a =>
                    a.TitleAr.Contains(q) || (a.TitleEn != null && a.TitleEn.Contains(q)) ||
                    (a.DescriptionAr != null && a.DescriptionAr.Contains(q)) ||
                    (a.Keywords != null && a.Keywords.Contains(q)) ||
                    (a.Creator != null && a.Creator.Contains(q)))
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .Select(a => new SearchHit
                {
                    Id       = a.AssetId.ToString(),
                    Type     = "asset",
                    TypeAr   = "أصل معرفي",
                    TypeIcon = "📦",
                    Title    = a.TitleAr,
                    TitleEn  = a.TitleEn,
                    Summary  = a.DescriptionAr,
                    Status   = a.Status.ToString(),
                    Date     = a.CreatedAt,
                    Tags     = a.Keywords,
                    Score    = ScoreDoc(a.TitleAr, a.DescriptionAr, a.Keywords, q),
                    Url      = $"/content-model/{a.AssetId}",
                    Highlights = BuildHighlights(q, a.TitleAr, a.DescriptionAr, a.Keywords),
                })
                .ToListAsync(ct);
            results.AddRange(assets);
        }

        // ── Apply filters & rank ──────────────────────────────────────────
        if (!string.IsNullOrEmpty(status))
            results = results.Where(r => r.Status?.ToLower() == status.ToLower()).ToList();

        var ranked  = results.OrderByDescending(r => r.Score).ToList();
        var total   = ranked.Count;
        var paged   = ranked.Skip((page-1)*pageSize).Take(pageSize).ToList();

        // Build facets
        var facets = new
        {
            byType = results.GroupBy(r => r.TypeAr)
                .Select(g => new { label = g.Key, icon = g.First().TypeIcon, count = g.Count() }),
            byStatus = results.GroupBy(r => r.Status ?? "Unknown")
                .Select(g => new { status = g.Key, count = g.Count() }),
        };

        return Ok(ApiResponse<object>.Ok(new {
            query   = q,
            total,
            page,
            pageSize,
            took    = 0,  // ms — would come from Elasticsearch
            results = paged,
            facets,
        }));
    }

    // ── Suggest (autocomplete) ──────────────────────────────────────────────
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q, CancellationToken ct)
    {
        if (q.Length < 2) return Ok(ApiResponse<object>.Ok(Array.Empty<string>()));

        var docTitles = await _db.Documents.AsNoTracking()
            .Where(d => d.TitleAr.Contains(q))
            .Select(d => d.TitleAr).Take(4).ToListAsync(ct);

        var recTitles = await _db.Records.AsNoTracking()
            .Where(r => r.TitleAr.Contains(q))
            .Select(r => r.TitleAr).Take(4).ToListAsync(ct);

        var suggestions = docTitles.Union(recTitles).Distinct().Take(8).ToList();
        return Ok(ApiResponse<object>.Ok(suggestions));
    }

    // ── Scoring (relevance) — simple TF-IDF approximation ──────────────────
    private static double ScoreDoc(string? title, string? body, string? tags, string q)
    {
        double score = 0;
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var term in terms)
        {
            if (title?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) score += 3.0;
            if (tags?.Contains(term, StringComparison.OrdinalIgnoreCase)  == true) score += 2.0;
            if (body?.Contains(term, StringComparison.OrdinalIgnoreCase)  == true) score += 1.0;
        }
        return score;
    }

    private static string? BuildHighlights(string q, params string?[] fields)
    {
        foreach (var field in fields)
        {
            if (field == null) continue;
            var idx = field.IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var start = Math.Max(0, idx - 40);
            var len   = Math.Min(field.Length - start, 120);
            var snippet = field.Substring(start, len);
            // Mark the match
            return (start > 0 ? "..." : "") +
                   snippet.Replace(q, $"**{q}**", StringComparison.OrdinalIgnoreCase) +
                   (start + len < field.Length ? "..." : "");
        }
        return null;
    }
}

public sealed class SearchHit
{
    public string  Id         { get; set; } = "";
    public string  Type       { get; set; } = "";
    public string  TypeAr     { get; set; } = "";
    public string  TypeIcon   { get; set; } = "";
    public string  Title      { get; set; } = "";
    public string? TitleEn    { get; set; }
    public string? Summary    { get; set; }
    public string? Status     { get; set; }
    public DateTime Date      { get; set; }
    public string? Tags       { get; set; }
    public double  Score      { get; set; }
    public string  Url        { get; set; } = "";
    public string? Highlights { get; set; }
}
