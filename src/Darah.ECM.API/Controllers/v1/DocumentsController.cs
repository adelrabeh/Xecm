using Darah.ECM.Application.Common.Abstractions;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Application.Documents.DTOs;
using Darah.ECM.Application.Documents.Queries;
using Darah.ECM.API.Filters;
using Darah.ECM.Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>
/// Document management API.
/// IFormFile BOUNDARY: Converted to FileUploadRequest before entering Application layer.
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize]
[Produces("application/json")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;

    public DocumentsController(IMediator mediator, IFileStorageService fileStorage)
    { _mediator = mediator; _fileStorage = fileStorage; }

    [HttpGet]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentListItemDto>>>> GetAll(
        [FromQuery] int? libraryId, [FromQuery] int? folderId, [FromQuery] int? documentTypeId,
        [FromQuery] string? statusCode, [FromQuery] string? search, [FromQuery] Guid? workspaceId,
        [FromQuery] string sortBy = "CreatedAt", [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new SearchDocumentsQuery(search, documentTypeId, libraryId, folderId, statusCode, null, null, null, null, null, null, workspaceId, null, null, sortBy, sortDir, page, Math.Min(pageSize, 100)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Get(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDocumentByIdQuery(id), ct);
        return r.Data is null ? NotFound(ApiResponse<DocumentDto>.Fail("الوثيقة غير موجودة")) : Ok(r);
    }

    /// <summary>
    /// Upload new document.
    /// ★ IFormFile converted to FileUploadRequest here (Clean Architecture boundary) ★
    /// </summary>
    [HttpPost]
    [RequirePermission("documents.create")]
    [RequestSizeLimit(536_870_912)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<DocumentCreatedDto>>> Create(
        [FromForm] string titleAr, [FromForm] string? titleEn,
        [FromForm] int documentTypeId, [FromForm] int libraryId, [FromForm] int? folderId,
        [FromForm] int classificationLevelOrder = 2,
        [FromForm] string? keywords = null, [FromForm] string? summary = null,
        [FromForm] Guid? workspaceId = null, IFormFile? file = null, CancellationToken ct = default)
    {
        if (file is null)
            return BadRequest(ApiResponse<DocumentCreatedDto>.Fail("يجب رفع ملف"));

        using var uploadRequest = new FileUploadRequest(
            file.FileName, file.ContentType, file.Length, file.OpenReadStream());

        var cmd = new CreateDocumentCommand
        {
            TitleAr = titleAr, TitleEn = titleEn,
            DocumentTypeId = documentTypeId, LibraryId = libraryId, FolderId = folderId,
            ClassificationLevelOrder = classificationLevelOrder,
            Keywords = keywords, Summary = summary,
            WorkspaceId = workspaceId, File = uploadRequest
        };
        var r = await _mediator.Send(cmd, ct);
        return r.Success ? CreatedAtAction(nameof(Get), new { id = r.Data!.DocumentId }, r) : BadRequest(r);
    }

    [HttpGet("{id:guid}/versions")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersions(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDocumentVersionsQuery(id), ct));

    [HttpPost("{id:guid}/checkout")]
    [RequirePermission("documents.checkout")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckOut(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new CheckOutDocumentCommand(id), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("documents.download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? versionId, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDocumentDownloadQuery(id, versionId), ct);
        if (!r.Success) return NotFound(r.Message);
        var stream = await _fileStorage.RetrieveAsync(r.Data!.StorageKey, ct);
        if (r.Data.RequiresWatermark) Response.Headers.Append("X-Watermark", "CONFIDENTIAL");
        return File(stream, r.Data.ContentType, $"{r.Data.DocumentNumber}_{r.Data.FileName}");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.delete")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, [FromBody] string? reason, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteDocumentCommand(id, reason), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new ApplyLegalHoldToDocumentCommand(id), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }
}
