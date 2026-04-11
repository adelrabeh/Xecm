using Darah.ECM.Application.Common.Abstractions;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Application.Documents.DTOs;
using Darah.ECM.Application.Documents.Queries;
using Darah.ECM.API.Filters;
using Darah.ECM.Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>
/// Documents controller — handles all document lifecycle operations.
///
/// IFormFile → FileUploadRequest conversion:
///   IFormFile lives in ASP.NET Core (Microsoft.AspNetCore.Http).
///   The Application layer must NOT reference it (Clean Architecture violation).
///   This controller is the correct boundary: it translates IFormFile into the
///   application-level FileUploadRequest abstraction before calling MediatR.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/documents")]
[Produces("application/json")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;

    public DocumentsController(IMediator mediator, IFileStorageService fileStorage)
        { _mediator = mediator; _fileStorage = fileStorage; }

    // ── GET list ──────────────────────────────────────────────────────────────
    [HttpGet]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentListItemDto>>>> GetAll(
        [FromQuery] int?      libraryId       = null,
        [FromQuery] int?      folderId        = null,
        [FromQuery] int?      documentTypeId  = null,
        [FromQuery] string?   statusCode      = null,
        [FromQuery] string?   search          = null,
        [FromQuery] Guid?     workspaceId     = null,
        [FromQuery] string    sortBy          = "CreatedAt",
        [FromQuery] string    sortDir         = "DESC",
        [FromQuery] int       page            = 1,
        [FromQuery] int       pageSize        = 20,
        CancellationToken ct = default)
    {
        var query = new SearchDocumentsQuery(
            TextQuery:          search,
            DocumentTypeId:     documentTypeId,
            LibraryId:          libraryId,
            FolderId:           folderId,
            StatusCode:         statusCode,
            ClassificationOrder: null,
            CreatedBy:          null,
            DateFrom:           null,
            DateTo:             null,
            IsLegalHold:        null,
            TagIds:             null,
            WorkspaceId:        workspaceId,
            ExternalSystemId:   null,
            ExternalObjectId:   null,
            SortBy:             sortBy,
            SortDirection:      sortDir,
            Page:               page,
            PageSize:           Math.Min(pageSize, 100));

        return Ok(await _mediator.Send(query, ct));
    }

    // ── GET by ID ─────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Get(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDocumentByIdQuery(id), ct);
        return result.Data is null
            ? NotFound(ApiResponse<DocumentDto>.Fail("الوثيقة غير موجودة"))
            : Ok(result);
    }

    // ── CREATE (upload) ───────────────────────────────────────────────────────
    /// <summary>
    /// Uploads a new document.
    /// IFormFile is received here (API boundary) and converted to FileUploadRequest
    /// before being passed to the Application layer — preserving Clean Architecture.
    /// </summary>
    [HttpPost]
    [RequirePermission("documents.create")]
    [RequestSizeLimit(536_870_912)]   // 512 MB
    public async Task<ActionResult<ApiResponse<DocumentCreatedDto>>> Create(
        [FromForm] CreateDocumentRequest request,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<DocumentCreatedDto>.Fail("يجب رفع ملف"));

        // ★ Clean Architecture boundary: convert IFormFile → FileUploadRequest here ★
        using var uploadRequest = new FileUploadRequest(
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream());

        var command = new CreateDocumentCommand
        {
            TitleAr                  = request.TitleAr,
            TitleEn                  = request.TitleEn,
            DocumentTypeId           = request.DocumentTypeId,
            LibraryId                = request.LibraryId,
            FolderId                 = request.FolderId,
            ClassificationLevelOrder = request.ClassificationLevelOrder,
            DocumentDate             = request.DocumentDate,
            Keywords                 = request.Keywords,
            Summary                  = request.Summary,
            RetentionPolicyId        = request.RetentionPolicyId,
            WorkspaceId              = request.WorkspaceId,
            File                     = uploadRequest,
            MetadataValues           = request.MetadataValues ?? new(),
            TagIds                   = request.TagIds ?? new()
        };

        var result = await _mediator.Send(command, ct);
        return result.Success
            ? CreatedAtAction(nameof(Get), new { id = result.Data!.DocumentId }, result)
            : BadRequest(result);
    }

    // ── GET versions ──────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/versions")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersions(
        Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDocumentVersionsQuery(id), ct));

    // ── CHECK-OUT ─────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/checkout")]
    [RequirePermission("documents.checkout")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckOut(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckOutDocumentCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── DOWNLOAD ──────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/download")]
    [RequirePermission("documents.download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? versionId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDocumentDownloadQuery(id, versionId), ct);
        if (!result.Success) return NotFound(result.Message);

        var stream = await _fileStorage.RetrieveAsync(result.Data!.StorageKey, ct);

        if (result.Data.RequiresWatermark)
            Response.Headers.Append("X-Watermark", User.FindFirst("name_ar")?.Value ?? string.Empty);

        return File(stream, result.Data.ContentType, result.Data.FileName);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.delete")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id, [FromBody] DeleteDocumentRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteDocumentCommand(id, request?.Reason), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── LEGAL HOLD ────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApplyLegalHoldToDocumentCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── REQUEST MODELS (API-layer only, not shared with Application) ──────────────
public sealed class CreateDocumentRequest
{
    public string    TitleAr                  { get; set; } = string.Empty;
    public string?   TitleEn                  { get; set; }
    public int       DocumentTypeId           { get; set; }
    public int       LibraryId                { get; set; }
    public int?      FolderId                 { get; set; }
    public int       ClassificationLevelOrder { get; set; } = 2;
    public DateOnly? DocumentDate             { get; set; }
    public string?   Keywords                 { get; set; }
    public string?   Summary                  { get; set; }
    public int?      RetentionPolicyId        { get; set; }
    public Guid?     WorkspaceId              { get; set; }
    public Dictionary<int, string>? MetadataValues { get; set; }
    public List<int>? TagIds                  { get; set; }
}

public sealed class DeleteDocumentRequest
{
    public string? Reason { get; set; }
}
