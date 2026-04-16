using Darah.ECM.Application.Common.Models;
using Darah.ECM.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Application.Search;

// ─── GAP 7: Faceted Search ────────────────────────────────────────────────────

public sealed record FacetedSearchQuery(
    string? Terms = null,
    string? Status = null,
    string? DocumentType = null,
    string? Classification = null,
    string? Department = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? RetentionPolicyId = null,
    bool? IsRecord = null,
    IEnumerable<int>? MetadataFieldIds = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "createdAt",
    string SortDir = "desc")
    : IRequest<ApiResponse<FacetedSearchResult>>;

public record FacetedSearchResult(
    IEnumerable<SearchDocumentDto> Documents,
    int TotalCount,
    FacetResults Facets,
    int Page,
    int PageSize);

public record SearchDocumentDto(
    Guid DocumentId, string TitleAr, string? TitleEn,
    string Status, string? DocumentType, int CreatedBy,
    DateTime CreatedAt, bool IsRecord, string? Classification);

public record FacetResults(
    IEnumerable<FacetBucket> ByStatus,
    IEnumerable<FacetBucket> ByDocumentType,
    IEnumerable<FacetBucket> ByClassification,
    IEnumerable<FacetBucket> ByYear,
    IEnumerable<FacetBucket> ByDepartment);

public record FacetBucket(string Value, int Count, string? Label = null);

public sealed class FacetedSearchHandler
    : IRequestHandler<FacetedSearchQuery, ApiResponse<FacetedSearchResult>>
{
    private readonly EcmDbContext _db;

    public FacetedSearchHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<FacetedSearchResult>> Handle(
        FacetedSearchQuery q, CancellationToken ct)
    {
        var query = _db.Documents.Where(d => !d.IsDeleted).AsQueryable();

        // ─── Apply Filters ────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.Terms))
            query = query.Where(d =>
                d.TitleAr.Contains(q.Terms) ||
                (d.TitleEn != null && d.TitleEn.Contains(q.Terms)) ||
                (d.Keywords != null && d.Keywords.Contains(q.Terms)));

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(d => d.Status.Value == q.Status);

        if (q.Classification != null && int.TryParse(q.Classification, out var classOrder))
            query = query.Where(d => d.Classification.Order == classOrder);

        if (q.FromDate.HasValue)
            query = query.Where(d => d.CreatedAt >= q.FromDate.Value);

        if (q.ToDate.HasValue)
            query = query.Where(d => d.CreatedAt <= q.ToDate.Value);

        if (q.IsRecord.HasValue)
            query = query.Where(d => d.IsRecord == q.IsRecord.Value);

        if (q.RetentionPolicyId.HasValue)
            query = query.Where(d => d.RetentionPolicyId == q.RetentionPolicyId.Value);

        // ─── Sort ─────────────────────────────────────────────────────────────
        query = (q.SortBy.ToLower(), q.SortDir.ToLower()) switch
        {
            ("createdat", "asc")  => query.OrderBy(d => d.CreatedAt),
            ("createdat", "desc") => query.OrderByDescending(d => d.CreatedAt),
            ("titlear", "asc")    => query.OrderBy(d => d.TitleAr),
            ("titlear", "desc")   => query.OrderByDescending(d => d.TitleAr),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var offset = (q.Page - 1) * q.PageSize;

        var docs = await query.Skip(offset).Take(q.PageSize)
            .Select(d => new SearchDocumentDto(
                d.DocumentId, d.TitleAr, d.TitleEn,
                d.Status.Value, null, d.CreatedBy,
                d.CreatedAt, d.IsRecord, d.Classification.Order.ToString()))
            .ToListAsync(ct);

        // ─── Calculate Facets (parallel) ──────────────────────────────────────
        var baseQuery = _db.Documents.Where(d => !d.IsDeleted);

        var statusFacets = await baseQuery
            .GroupBy(d => d.Status.Value)
            .Select(g => new FacetBucket(g.Key, g.Count()))
            .ToListAsync(ct);

        var classificationFacets = await baseQuery
            .GroupBy(d => d.Classification.Order)
            .Select(g => new FacetBucket(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var yearFacets = await baseQuery
            .GroupBy(d => d.CreatedAt.Year)
            .Select(g => new FacetBucket(g.Key.ToString(), g.Count()))
            .OrderByDescending(f => f.Value)
            .Take(10)
            .ToListAsync(ct);

        var recordFacets = new[]
        {
            new FacetBucket("true",  await baseQuery.CountAsync(d => d.IsRecord, ct), "سجلات رسمية"),
            new FacetBucket("false", await baseQuery.CountAsync(d => !d.IsRecord, ct), "وثائق")
        };

        return ApiResponse<FacetedSearchResult>.Ok(new FacetedSearchResult(
            docs, total,
            new FacetResults(
                statusFacets, [], classificationFacets, yearFacets, []),
            q.Page, q.PageSize));
    }
}

// ─── GAP 8: CMIS Content Model ────────────────────────────────────────────────

public sealed record ContentType(
    string TypeId, string TypeName, string? ParentTypeId,
    string DisplayNameAr, string? DisplayNameEn,
    IEnumerable<PropertyDefinition> Properties);

public sealed record PropertyDefinition(
    string PropertyId, string DisplayNameAr, string? DisplayNameEn,
    string DataType, bool Required, bool MultiValued,
    string? DefaultValue, IEnumerable<string>? AllowedValues);

public interface IContentModelService
{
    Task<IEnumerable<ContentType>> GetAllTypesAsync(CancellationToken ct);
    Task<ContentType?> GetTypeAsync(string typeId, CancellationToken ct);
    Task<string> CreateTypeAsync(ContentType type, CancellationToken ct);
    Task UpdateTypeAsync(ContentType type, CancellationToken ct);
}

public sealed class ContentModelService : IContentModelService
{
    private readonly EcmDbContext _db;

    public ContentModelService(EcmDbContext db) => _db = db;

    public async Task<IEnumerable<ContentType>> GetAllTypesAsync(CancellationToken ct)
    {
        // Built-in CMIS base types + custom types from metadata
        var builtIn = GetBuiltInTypes();

        var custom = await _db.Set<Darah.ECM.Domain.Entities.MetadataField>()
            .GroupBy(f => f.FieldName)
            .Select(g => new ContentType(
                $"D:darah:{g.Key.ToLower()}",
                g.Key, "cmis:document",
                g.Key, g.Key,
                g.Select(f => new PropertyDefinition(
                    $"darah:{f.FieldName}", f.FieldName, f.FieldName,
                    f.FieldType, f.IsRequired, false, f.DefaultValue, null))))
            .ToListAsync(ct);

        return builtIn.Concat(custom);
    }

    public Task<ContentType?> GetTypeAsync(string typeId, CancellationToken ct)
    {
        var builtIn = GetBuiltInTypes()
            .FirstOrDefault(t => t.TypeId == typeId);
        return Task.FromResult(builtIn);
    }

    public async Task<string> CreateTypeAsync(ContentType type, CancellationToken ct)
    {
        foreach (var prop in type.Properties)
        {
            await _db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "MetadataFields"
                    ("FieldName","FieldType","IsRequired","DefaultValue","CreatedAt","CreatedBy")
                VALUES ({0},{1},{2},{3},NOW(),0)
                ON CONFLICT ("FieldName") DO NOTHING
                """,
                prop.PropertyId.Replace("darah:", ""),
                prop.DataType, prop.Required,
                prop.DefaultValue ?? "", ct);
        }
        return type.TypeId;
    }

    public Task UpdateTypeAsync(ContentType type, CancellationToken ct)
        => CreateTypeAsync(type, ct);

    private static IEnumerable<ContentType> GetBuiltInTypes() =>
    [
        new ContentType("cmis:document", "Document", null,
            "وثيقة", "Document",
            [
                new("cmis:name", "اسم الملف", "Name", "string", true, false, null, null),
                new("cmis:createdBy", "أنشأ بواسطة", "Created By", "string", false, false, null, null),
                new("cmis:creationDate", "تاريخ الإنشاء", "Creation Date", "datetime", false, false, null, null),
                new("cmis:contentStreamLength", "حجم الملف", "File Size", "integer", false, false, null, null),
                new("cmis:contentStreamMimeType", "نوع الملف", "MIME Type", "string", false, false, null, null),
                new("cmis:versionLabel", "رقم الإصدار", "Version", "string", false, false, null, null),
                new("darah:titleAr", "العنوان بالعربية", "Arabic Title", "string", true, false, null, null),
                new("darah:titleEn", "العنوان بالإنجليزية", "English Title", "string", false, false, null, null),
                new("darah:classification", "مستوى التصنيف", "Classification", "string", true, false, "public",
                    ["public", "internal", "confidential", "secret", "top-secret"]),
                new("darah:status", "الحالة", "Status", "string", true, false, "Draft",
                    ["Draft", "UnderReview", "Approved", "Published", "Archived", "Disposed"]),
                new("darah:isRecord", "سجل رسمي", "Is Record", "boolean", false, false, "false", null),
            ]),

        new ContentType("cmis:folder", "Folder", null,
            "مجلد", "Folder",
            [
                new("cmis:name", "اسم المجلد", "Folder Name", "string", true, false, null, null),
                new("cmis:path", "المسار", "Path", "string", false, false, null, null),
            ]),

        new ContentType("darah:officialRecord", "Official Record", "cmis:document",
            "سجل رسمي", "Official Record",
            [
                new("darah:recordNumber", "رقم السجل", "Record Number", "string", true, false, null, null),
                new("darah:retentionSchedule", "جدول الاحتفاظ", "Retention Schedule", "string", true, false, null, null),
                new("darah:declaredAt", "تاريخ الإعلان", "Declaration Date", "datetime", false, false, null, null),
                new("darah:disposalDate", "تاريخ الإتلاف", "Disposal Date", "datetime", false, false, null, null),
            ]),
    ];
}

// ─── Content Model Controller ─────────────────────────────────────────────────
namespace Darah.ECM.API.Controllers.v1;

[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/content-model")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class ContentModelController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly IContentModelService _model;
    private readonly IMediator _mediator;

    public ContentModelController(IContentModelService model, IMediator mediator)
    { _model = model; _mediator = mediator; }

    [Microsoft.AspNetCore.Mvc.HttpGet("types")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> GetTypes(
        System.Threading.CancellationToken ct)
    {
        var types = await _model.GetAllTypesAsync(ct);
        return Ok(new { success = true, data = types });
    }

    [Microsoft.AspNetCore.Mvc.HttpGet("types/{typeId}")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> GetType(
        string typeId, System.Threading.CancellationToken ct)
    {
        var type = await _model.GetTypeAsync(typeId, ct);
        return type != null
            ? Ok(new { success = true, data = type })
            : NotFound(new { success = false, message = "Type not found" });
    }

    [Microsoft.AspNetCore.Mvc.HttpGet("search")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> FacetedSearch(
        [Microsoft.AspNetCore.Mvc.FromQuery] FacetedSearchQuery query,
        System.Threading.CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
