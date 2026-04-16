using Darah.ECM.Application.Common.Models;
using MediatR;

namespace Darah.ECM.Application.Search;

/// <summary>
/// AC5: Advanced Full-Text Search with Faceted Filtering
/// Performance target: &lt;2 seconds for 1M documents
/// </summary>
public sealed record FacetedSearchQuery(
    string? Keywords,
    // Facets
    DateTime? DateFrom,
    DateTime? DateTo,
    string? OwnedByUsername,
    string? Status,
    string? DocumentClass,
    string? Classification,
    // Pagination
    int Page = 1,
    int PageSize = 20)
    : IRequest<ApiResponse<FacetedSearchResultDto>>;

public sealed record FacetedSearchResultDto(
    IEnumerable<SearchHitDto> Hits,
    int TotalCount,
    int Page,
    int PageSize,
    FacetSummaryDto Facets,
    long QueryTimeMs);

public sealed record SearchHitDto(
    Guid DocumentId,
    string Title,
    string DocumentNumber,
    string Status,
    string Classification,
    string OwnerName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Highlight,       // Snippet with matched text highlighted
    double RelevanceScore);

public sealed record FacetSummaryDto(
    IEnumerable<FacetBucketDto> ByStatus,
    IEnumerable<FacetBucketDto> ByClassification,
    IEnumerable<FacetBucketDto> ByOwner,
    IEnumerable<FacetBucketDto> ByMonth);

public sealed record FacetBucketDto(string Label, int Count);
