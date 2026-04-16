using Darah.ECM.Application.Common.Models;
using Darah.ECM.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Domain.Entities;

// ─── Document Annotation Entity ───────────────────────────────────────────────
public sealed class DocumentAnnotation : BaseEntity
{
    public int        AnnotationId   { get; private set; }
    public Guid       DocumentId     { get; private set; }
    public string     Type           { get; private set; } = "comment"; // comment|highlight|redline|stamp
    public string     ContentAr      { get; private set; } = string.Empty;
    public string?    ContentEn      { get; private set; }
    public int?       PageNumber     { get; private set; }
    public string?    PositionJson   { get; private set; } // {x, y, width, height}
    public string?    Color          { get; private set; }
    public bool       IsResolved     { get; private set; }
    public int?       ResolvedById   { get; private set; }
    public DateTime?  ResolvedAt     { get; private set; }
    public int?       ParentId       { get; private set; } // for replies

    private DocumentAnnotation() { }

    public static DocumentAnnotation Create(
        Guid documentId, string type, string contentAr,
        string? contentEn, int createdBy, int? pageNumber = null,
        string? positionJson = null, string? color = null,
        int? parentId = null)
    {
        var ann = new DocumentAnnotation
        {
            DocumentId   = documentId,
            Type         = type,
            ContentAr    = contentAr.Trim(),
            ContentEn    = contentEn?.Trim(),
            PageNumber   = pageNumber,
            PositionJson = positionJson,
            Color        = color ?? "#FFD700",
            IsResolved   = false,
            ParentId     = parentId
        };
        ann.SetCreated(createdBy);
        return ann;
    }

    public void Resolve(int userId)
    {
        IsResolved   = true;
        ResolvedById = userId;
        ResolvedAt   = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Edit(string contentAr, string? contentEn, int userId)
    {
        ContentAr = contentAr.Trim();
        ContentEn = contentEn?.Trim();
        SetUpdated(userId);
    }
}

namespace Darah.ECM.Application.Annotations;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record AnnotationDto(
    int AnnotationId, Guid DocumentId, string Type,
    string ContentAr, string? ContentEn, int? PageNumber,
    string? PositionJson, string? Color, bool IsResolved,
    DateTime CreatedAt, int CreatedBy, int? ParentId,
    IEnumerable<AnnotationDto>? Replies = null);

// ─── Get Annotations Query ────────────────────────────────────────────────────
public sealed record GetAnnotationsQuery(Guid DocumentId, int? PageNumber = null)
    : IRequest<ApiResponse<IEnumerable<AnnotationDto>>>;

public sealed class GetAnnotationsHandler
    : IRequestHandler<GetAnnotationsQuery, ApiResponse<IEnumerable<AnnotationDto>>>
{
    private readonly EcmDbContext _db;
    public GetAnnotationsHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<IEnumerable<AnnotationDto>>> Handle(
        GetAnnotationsQuery q, CancellationToken ct)
    {
        var query = _db.Set<DocumentAnnotation>()
            .Where(a => a.DocumentId == q.DocumentId && a.ParentId == null);

        if (q.PageNumber.HasValue)
            query = query.Where(a => a.PageNumber == q.PageNumber);

        var annotations = await query.OrderBy(a => a.CreatedAt).ToListAsync(ct);

        // Load replies
        var allIds = annotations.Select(a => a.AnnotationId).ToList();
        var replies = await _db.Set<DocumentAnnotation>()
            .Where(a => a.ParentId.HasValue && allIds.Contains(a.ParentId.Value))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        var replyMap = replies.GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(ToDto).ToList());

        var result = annotations.Select(a =>
            ToDto(a) with { Replies = replyMap.GetValueOrDefault(a.AnnotationId) });

        return ApiResponse<IEnumerable<AnnotationDto>>.Ok(result);
    }

    private static AnnotationDto ToDto(DocumentAnnotation a) => new(
        a.AnnotationId, a.DocumentId, a.Type, a.ContentAr, a.ContentEn,
        a.PageNumber, a.PositionJson, a.Color, a.IsResolved,
        a.CreatedAt, a.CreatedBy, a.ParentId);
}

// ─── Add Annotation Command ───────────────────────────────────────────────────
public sealed record AddAnnotationCommand(
    Guid DocumentId, string Type, string ContentAr, string? ContentEn,
    int CreatedBy, int? PageNumber = null,
    string? PositionJson = null, string? Color = null,
    int? ParentId = null) : IRequest<ApiResponse<AnnotationDto>>;

public sealed class AddAnnotationHandler
    : IRequestHandler<AddAnnotationCommand, ApiResponse<AnnotationDto>>
{
    private readonly EcmDbContext _db;
    public AddAnnotationHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<AnnotationDto>> Handle(
        AddAnnotationCommand cmd, CancellationToken ct)
    {
        // Verify document exists and is not a locked record
        var doc = await _db.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == cmd.DocumentId && !d.IsDeleted, ct);

        if (doc is null)
            return ApiResponse<AnnotationDto>.Fail("الوثيقة غير موجودة");

        var annotation = DocumentAnnotation.Create(
            cmd.DocumentId, cmd.Type, cmd.ContentAr, cmd.ContentEn,
            cmd.CreatedBy, cmd.PageNumber, cmd.PositionJson,
            cmd.Color, cmd.ParentId);

        _db.Set<DocumentAnnotation>().Add(annotation);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<AnnotationDto>.Ok(new AnnotationDto(
            annotation.AnnotationId, annotation.DocumentId, annotation.Type,
            annotation.ContentAr, annotation.ContentEn, annotation.PageNumber,
            annotation.PositionJson, annotation.Color, annotation.IsResolved,
            annotation.CreatedAt, annotation.CreatedBy, annotation.ParentId));
    }
}

// ─── Resolve Annotation Command ───────────────────────────────────────────────
public sealed record ResolveAnnotationCommand(int AnnotationId, int ResolvedBy)
    : IRequest<ApiResponse<bool>>;

public sealed class ResolveAnnotationHandler
    : IRequestHandler<ResolveAnnotationCommand, ApiResponse<bool>>
{
    private readonly EcmDbContext _db;
    public ResolveAnnotationHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> Handle(
        ResolveAnnotationCommand cmd, CancellationToken ct)
    {
        var ann = await _db.Set<DocumentAnnotation>()
            .FirstOrDefaultAsync(a => a.AnnotationId == cmd.AnnotationId, ct);

        if (ann is null) return ApiResponse<bool>.Fail("التعليق غير موجود");

        ann.Resolve(cmd.ResolvedBy);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

// ─── Annotations Controller ───────────────────────────────────────────────────
namespace Darah.ECM.API.Controllers.v1;

[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/documents/{documentId:guid}/annotations")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class AnnotationsController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly IMediator _mediator;
    public AnnotationsController(IMediator mediator) => _mediator = mediator;

    [Microsoft.AspNetCore.Mvc.HttpGet]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> GetAnnotations(
        Guid documentId, [Microsoft.AspNetCore.Mvc.FromQuery] int? page,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetAnnotationsQuery(documentId, page), ct);
        return result.Success
            ? Ok(result) : BadRequest(result);
    }

    [Microsoft.AspNetCore.Mvc.HttpPost]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> AddAnnotation(
        Guid documentId,
        [Microsoft.AspNetCore.Mvc.FromBody] AddAnnotationCommand cmd,
        CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "0");
        var result = await _mediator.Send(
            cmd with { DocumentId = documentId, CreatedBy = userId }, ct);
        return result.Success
            ? Ok(result) : BadRequest(result);
    }

    [Microsoft.AspNetCore.Mvc.HttpPatch("{annotationId:int}/resolve")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> Resolve(
        int annotationId, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "0");
        var result = await _mediator.Send(
            new ResolveAnnotationCommand(annotationId, userId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
