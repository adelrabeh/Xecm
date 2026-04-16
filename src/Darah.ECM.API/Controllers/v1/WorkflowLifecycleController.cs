using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Search;
using Darah.ECM.Application.Workflows;
using Darah.ECM.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>
/// User Story: Automated Document Lifecycle and Metadata-Driven Workflow
/// Covers AC2, AC4, AC5
/// </summary>
[ApiController]
[Route("api/v1/lifecycle")]
[Authorize]
[Produces("application/json")]
public sealed class WorkflowLifecycleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly MetadataSecurityPolicy _securityPolicy;

    public WorkflowLifecycleController(
        IMediator mediator, MetadataSecurityPolicy securityPolicy)
    {
        _mediator = mediator;
        _securityPolicy = securityPolicy;
    }

    // ─── AC4: Transition document state ────────────────────────────────────────
    /// <summary>
    /// Transition a document between workflow states.
    /// Automatically notifies stakeholders and validates signature requirements.
    /// Draft → InternalReview → Approved / Rejected
    /// </summary>
    [HttpPost("documents/{id:guid}/transition")]
    public async Task<ActionResult<ApiResponse<WorkflowTransitionResultDto>>> Transition(
        Guid id,
        [FromBody] TransitionRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new TransitionDocumentCommand(
            id, request.TargetState, request.Comment,
            request.IsElectronicSignature), ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── AC4: Get valid next states ─────────────────────────────────────────────
    [HttpGet("documents/{id:guid}/transitions")]
    public IActionResult GetValidTransitions([FromRoute] string currentState)
    {
        var transitions = WorkflowRoutingRules.GetValidTransitions(currentState);
        return Ok(ApiResponse<IEnumerable<string>>.Ok(transitions));
    }

    // ─── AC2: Evaluate document access rights ──────────────────────────────────
    /// <summary>
    /// Returns the access right for the current user on a given document,
    /// evaluated using metadata-driven security rules.
    /// </summary>
    [HttpGet("documents/{id:guid}/access")]
    public async Task<IActionResult> GetAccessRight(
        Guid id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var right = await _securityPolicy.EvaluateByMetadataAsync(id, userId, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            documentId = id,
            userId,
            accessRight = right.ToString(),
            canRead  = right >= DocumentAccessRight.ReadOnly,
            canWrite = right >= DocumentAccessRight.ReadWrite,
        }));
    }

    // ─── AC5: Faceted full-text search ─────────────────────────────────────────
    /// <summary>
    /// Full-text search with faceted filtering.
    /// Performance target: &lt;2 seconds for 1M documents (GIN index).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<FacetedSearchResultDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? status,
        [FromQuery] string? classification,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new FacetedSearchQuery(
            q, dateFrom, dateTo,
            User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            status, null, classification, page, pageSize), ct);

        return Ok(result);
    }
}

public sealed record TransitionRequest(
    string TargetState,
    string? Comment,
    bool IsElectronicSignature = false);
