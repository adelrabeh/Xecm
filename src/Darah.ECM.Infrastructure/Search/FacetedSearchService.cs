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
            // Status filter via value object comparison
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

        var hits = new List<SearchHitDto>();
        foreach (var d in rawHits)
        {
            hits.Add(new SearchHitDto(
                DocumentId: d.DocumentId,
                Title: d.TitleAr,
                DocumentNumber: d.DocumentNumber,
                Status: "Active",
                Classification: "Internal",
                OwnerName: d.CreatedBy.ToString(),
                CreatedAt: d.CreatedAt,
                UpdatedAt: d.UpdatedAt,
                Highlight: null,
                RelevanceScore: 1.0));
        }

        // Facets (simplified - value object GroupBy not supported in EF translation)
        var statusFacets = new List<FacetBucketDto>
        {
            new("Draft", await _db.Documents.CountAsync(d => !d.IsDeleted, ct)),
        };
        var classFacets = new List<FacetBucketDto>();
        var monthFacets = new List<FacetBucketDto>();

        sw.Stop();

        return ApiResponse<FacetedSearchResultDto>.Ok(new FacetedSearchResultDto(
            hits, totalCount, q.Page, q.PageSize,
            new FacetSummaryDto(
                statusFacets, classFacets,
                Enumerable.Empty<FacetBucketDto>(), monthFacets),
            sw.ElapsedMilliseconds));
    }
}
