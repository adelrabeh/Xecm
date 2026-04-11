using Darah.ECM.Application.Common.Models;
using MediatR;

namespace Darah.ECM.Application.Audit.Queries;

// ─── AUDIT QUERY ─────────────────────────────────────────────────────────────
public sealed record GetAuditLogsQuery(
    string?   EventType,
    string?   EntityType,
    string?   EntityId,
    int?      UserId,
    string?   Severity,
    bool?     IsSuccessful,
    DateTime? DateFrom,
    DateTime? DateTo,
    string    SortDirection = "DESC",
    int       Page          = 1,
    int       PageSize      = 50)
    : IRequest<ApiResponse<PagedResult<AuditLogDto>>>;

public sealed record GetAuditSummaryQuery(DateTime? DateFrom, DateTime? DateTo)
    : IRequest<ApiResponse<AuditSummaryDto>>;

public sealed record ExportAuditLogsQuery(
    string?   EventType,
    string?   EntityType,
    string?   EntityId,
    int?      UserId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string    Format = "Excel")   // Excel | Pdf
    : IRequest<ApiResponse<AuditExportDto>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public sealed record AuditLogDto(
    long     AuditId,
    string   EventType,
    string?  EntityType,
    string?  EntityId,
    int?     UserId,
    string?  Username,
    string?  IPAddress,
    string?  OldValues,
    string?  NewValues,
    string?  AdditionalInfo,
    string   Severity,
    bool     IsSuccessful,
    string?  FailureReason,
    DateTime CreatedAt);

public sealed record AuditSummaryDto(
    int TotalEvents,
    int TotalErrors,
    int TotalSecurityEvents,
    int UniqueUsers,
    List<EventTypeCountDto> TopEventTypes,
    List<EventTypeCountDto> TopUsers);

public sealed record EventTypeCountDto(string Label, int Count);

public sealed record AuditExportDto(
    string Format,
    string FileName,
    byte[] FileBytes,
    int    RecordCount);

// ─── AUDIT LOG QUERY HANDLER ─────────────────────────────────────────────────
public sealed class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, ApiResponse<PagedResult<AuditLogDto>>>
{
    private readonly IAuditQueryRepository _repo;

    public GetAuditLogsQueryHandler(IAuditQueryRepository repo) => _repo = repo;

    public async Task<ApiResponse<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery query, CancellationToken ct)
    {
        var result = await _repo.QueryAsync(query, ct);
        return ApiResponse<PagedResult<AuditLogDto>>.Ok(result);
    }
}

public interface IAuditQueryRepository
{
    Task<PagedResult<AuditLogDto>> QueryAsync(GetAuditLogsQuery query, CancellationToken ct);
    Task<AuditSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct);
    Task<List<AuditLogDto>> GetForExportAsync(ExportAuditLogsQuery query, CancellationToken ct);
}
