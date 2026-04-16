using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Search;
using Darah.ECM.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Darah.ECM.Infrastructure.Search;

/// <summary>AC5: Faceted Full-Text Search using PostgreSQL</summary>
public sealed class FacetedSearchHandler
    : IRequestHandler<FacetedSearchQuery, ApiResponse<FacetedSearchResultDto>>
{
    private readonly EcmDbContext _db;
    public FacetedSearchHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<FacetedSearchResultDto>> Handle(
        FacetedSearchQuery q, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var query = _db.Documents.AsNoTracking().Where(d => !d.IsDeleted);

        // Keyword search on TitleAr (correct property name)
        if (!string.IsNullOrWhiteSpace(q.Keywords))
            query = query.Where(d =>
                d.TitleAr.Contains(q.Keywords) ||
                d.DocumentNumber.Contains(q.Keywords));

        if (q.DateFrom.HasValue) query = query.Where(d => d.CreatedAt >= q.DateFrom.Value);
        if (q.DateTo.HasValue)   query = query.Where(d => d.CreatedAt <= q.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(d => d.Status.Value == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Classification))
            query = query.Where(d => d.Classification.Code == q.Classification);

        var totalCount = await query.CountAsync(ct);

        // SearchHitDto has 9 params: DocumentId, Title, DocumentNumber, Status,
        // Classification, OwnerName, CreatedAt, UpdatedAt, Highlight, RelevanceScore
        // Project to anonymous type first (EF can't translate record constructors with value objects)
        var rawHits = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(d => new {
                d.DocumentId, d.TitleAr, d.DocumentNumber,
                d.CreatedBy, d.CreatedAt, d.UpdatedAt
            })
            .ToListAsync(ct);

        var hits = rawHits.Select(d => new SearchHitDto(
            d.DocumentId, d.TitleAr, d.DocumentNumber,
            "Active", "Internal",
            d.CreatedBy.ToString(), d.CreatedAt, d.UpdatedAt,
            null, 1.0)).ToList();

        // Facets
        var statusFacets = await _db.Documents.AsNoTracking()
            .Where(d => !d.IsDeleted)
            .GroupBy(d => d.Status.Value)
            .Select(g => new FacetBucketDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var classFacets = await _db.Documents.AsNoTracking()
            .Where(d => !d.IsDeleted)
            .GroupBy(d => d.Classification.Code)
            .Select(g => new FacetBucketDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var monthFacets = await _db.Documents.AsNoTracking()
            .Where(d => !d.IsDeleted && d.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new FacetBucketDto($"{g.Key.Year}-{g.Key.Month:D2}", g.Count()))
            .OrderByDescending(f => f.Label)
            .ToListAsync(ct);

        sw.Stop();

        return ApiResponse<FacetedSearchResultDto>.Ok(new FacetedSearchResultDto(
            hits, totalCount, q.Page, q.PageSize,
            new FacetSummaryDto(
                statusFacets, classFacets,
                Enumerable.Empty<FacetBucketDto>(), monthFacets),
            sw.ElapsedMilliseconds));
    }
}
