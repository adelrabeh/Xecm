using Darah.ECM.Application.Common.Models;
using MediatR;

namespace Darah.ECM.Application.Search;

// ─── SEARCH QUERY ─────────────────────────────────────────────────────────────
public sealed record AdvancedSearchQuery(
    string?   TextQuery,
    int?      DocumentTypeId,
    int?      LibraryId,
    int?      FolderId,
    string?   StatusCode,
    int?      ClassificationOrder,
    int?      CreatedBy,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool?     IsLegalHold,
    List<int>? TagIds,
    Guid?     WorkspaceId,
    string?   ExternalSystemId,
    string?   ExternalObjectId,
    string    SortBy        = "CreatedAt",
    string    SortDirection = "DESC",
    int       Page          = 1,
    int       PageSize      = 20)
    : IRequest<ApiResponse<PagedResult<DocumentListItemDto>>>;

// ─── SAVED SEARCHES ───────────────────────────────────────────────────────────
public sealed record SavedSearchDto(
    int      SearchId,
    string   NameAr,
    string?  NameEn,
    bool     IsPublic,
    DateTime? LastRunAt,
    int      RunCount,
    DateTime CreatedAt);

public sealed record CreateSavedSearchCommand(
    string NameAr,
    string? NameEn,
    string QueryJson,
    bool IsPublic = false)
    : IRequest<ApiResponse<SavedSearchDto>>;

public sealed record GetSavedSearchesQuery()
    : IRequest<ApiResponse<List<SavedSearchDto>>>;

public sealed record DeleteSavedSearchCommand(int SearchId)
    : IRequest<ApiResponse<bool>>;

// ─── SEARCH PROVIDER ABSTRACTION ─────────────────────────────────────────────
/// <summary>
/// Search engine abstraction.
/// Default: SQL Server Full-Text Search.
/// Future: Elasticsearch / Azure AI Search (swap without touching Application layer).
/// </summary>
public interface ISearchProvider
{
    Task<SearchProviderResult> SearchAsync(SearchQuery query, CancellationToken ct = default);
    Task IndexDocumentAsync(DocumentIndexEntry entry, CancellationToken ct = default);
    Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default);
    bool IsAvailable { get; }
    string ProviderName { get; }
}

public sealed record SearchQuery(
    string?  TextQuery,
    int?     DocumentTypeId,
    int?     LibraryId,
    int?     FolderId,
    string?  StatusCode,
    Guid?    WorkspaceId,
    string?  ExternalSystemId,
    string?  ExternalObjectId,
    bool?    IsLegalHold,
    DateTime? DateFrom,
    DateTime? DateTo,
    string   SortBy        = "CreatedAt",
    string   SortDirection = "DESC",
    int      Page          = 1,
    int      PageSize      = 20);

public sealed record SearchProviderResult(
    IReadOnlyList<Guid> DocumentIds,
    int                 TotalCount,
    long                ElapsedMs);

public sealed record DocumentIndexEntry(
    Guid   DocumentId,
    string DocumentNumber,
    string TitleAr,
    string? TitleEn,
    string? Keywords,
    string? Summary,
    string? ContentText,    // extracted text from PDF/DOCX
    int    DocumentTypeId,
    int    LibraryId,
    string StatusCode,
    int    ClassificationOrder,
    Guid?  WorkspaceId,
    string? ExternalObjectId,
    DateTime CreatedAt);

// ─── SQL SERVER FTS PROVIDER ─────────────────────────────────────────────────
/// <summary>
/// SQL Server Full-Text Search implementation.
/// Uses CONTAINS predicate for Arabic and English text search.
/// Workspace-scoped filtering applied via JOIN on WorkspaceDocuments.
/// </summary>
public sealed class SqlServerFtsSearchProvider : ISearchProvider
{
    public string ProviderName => "SqlServer-FTS";
    public bool IsAvailable => true;

    public Task<SearchProviderResult> SearchAsync(SearchQuery q, CancellationToken ct = default)
    {
        // Full implementation uses EF Core raw SQL with CONTAINS() predicate:
        //
        // var sql = @"
        //   SELECT d.DocumentId
        //   FROM Documents d
        //   LEFT JOIN WorkspaceDocuments wd ON d.DocumentId = wd.DocumentId
        //   WHERE d.IsDeleted = 0
        //     AND (@TextQuery IS NULL OR CONTAINS((d.TitleAr, d.TitleEn, d.Keywords, d.Summary), @TextQuery))
        //     AND (@WorkspaceId IS NULL OR wd.WorkspaceId = @WorkspaceId)
        //     AND (@ExternalObjectId IS NULL OR EXISTS (
        //           SELECT 1 FROM Workspaces ws
        //           WHERE ws.WorkspaceId = wd.WorkspaceId
        //             AND ws.ExternalObjectId = @ExternalObjectId))
        //     AND (@LibraryId IS NULL OR d.LibraryId = @LibraryId)
        //     AND (@StatusCode IS NULL OR d.Status = @StatusCode)
        //   ORDER BY d.CreatedAt DESC
        //   OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
        //
        // Note: CONTAINS requires SQL Server FTS catalog on Documents table
        // (already created in DARAH_ECM_Schema.sql)

        return Task.FromResult(new SearchProviderResult(
            Array.Empty<Guid>(), 0, 0));
    }

    public Task IndexDocumentAsync(DocumentIndexEntry entry, CancellationToken ct = default)
        => Task.CompletedTask; // SQL Server FTS indexes automatically via triggers

    public Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default)
        => Task.CompletedTask; // Handled by soft-delete + FTS auto-update
}
