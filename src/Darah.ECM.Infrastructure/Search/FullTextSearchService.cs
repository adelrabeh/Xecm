using Darah.ECM.Application.Common.Models;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Search;

public interface IFullTextSearchService
{
    Task<SearchResultDto> SearchAsync(SearchQuery query, CancellationToken ct = default);
    Task IndexDocumentAsync(Guid documentId, string content, CancellationToken ct = default);
}

public record SearchQuery(
    string Terms,
    string Language = "arabic",
    int Page = 1,
    int PageSize = 20,
    string? DocumentType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public record SearchResultDto(
    IEnumerable<SearchHitDto> Hits,
    int TotalCount,
    int Page,
    int PageSize,
    TimeSpan Elapsed);

public record SearchHitDto(
    Guid DocumentId,
    string TitleAr,
    string? TitleEn,
    string Status,
    double Rank,
    string? Headline);

public sealed class PostgresFullTextSearchService : IFullTextSearchService
{
    private readonly EcmDbContext _db;
    private readonly ILogger<PostgresFullTextSearchService> _log;

    public PostgresFullTextSearchService(EcmDbContext db,
        ILogger<PostgresFullTextSearchService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<SearchResultDto> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // PostgreSQL full-text search with Arabic + English support
        // Uses GIN index on tsvector column for performance
        var sql = """
            SELECT
                d."DocumentId",
                d."TitleAr",
                d."TitleEn",
                d."Status",
                ts_rank_cd(d."SearchVector", query) AS rank,
                ts_headline('arabic', d."TitleAr", query,
                    'StartSel=<mark>, StopSel=</mark>, MaxWords=50') AS headline
            FROM "Documents" d,
                 websearch_to_tsquery('arabic', {0}) query
            WHERE d."SearchVector" @@ query
              AND d."IsDeleted" = false
            ORDER BY rank DESC
            LIMIT {1} OFFSET {2}
            """;

        var offset = (query.Page - 1) * query.PageSize;

        try
        {
            var results = await _db.Database
                .SqlQueryRaw<SearchRawResult>(sql,
                    query.Terms, query.PageSize, offset)
                .ToListAsync(ct);

            var countSql = """
                SELECT COUNT(*) FROM "Documents" d
                WHERE d."SearchVector" @@ websearch_to_tsquery('arabic', {0})
                  AND d."IsDeleted" = false
                """;

            var total = await _db.Database
                .SqlQueryRaw<int>(countSql, query.Terms)
                .FirstOrDefaultAsync(ct);

            sw.Stop();

            return new SearchResultDto(
                results.Select(r => new SearchHitDto(
                    r.DocumentId, r.TitleAr, r.TitleEn,
                    r.Status, r.Rank, r.Headline)),
                total, query.Page, query.PageSize, sw.Elapsed);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Full-text search failed for: {Terms}", query.Terms);
            return new SearchResultDto([], 0, query.Page, query.PageSize, sw.Elapsed);
        }
    }

    public async Task IndexDocumentAsync(Guid documentId, string content, CancellationToken ct)
    {
        // Update the tsvector column with extracted content
        await _db.Database.ExecuteSqlRawAsync("""
            UPDATE "Documents"
            SET "SearchVector" = 
                setweight(to_tsvector('arabic', coalesce("TitleAr", '')), 'A') ||
                setweight(to_tsvector('simple', coalesce("TitleEn", '')), 'A') ||
                setweight(to_tsvector('arabic', {1}), 'B')
            WHERE "DocumentId" = {0}
            """, documentId, content, ct);
    }

    private record SearchRawResult(
        Guid DocumentId, string TitleAr, string? TitleEn,
        string Status, double Rank, string? Headline);
}
