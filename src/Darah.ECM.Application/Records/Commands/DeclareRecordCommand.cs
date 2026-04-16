using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Application.Records.Commands;

// ─── Command ──────────────────────────────────────────────────────────────────
public sealed record DeclareRecordCommand(
    Guid DocumentId,
    string RetentionSchedule,
    int DeclaredBy) : IRequest<ApiResponse<RecordDeclarationDto>>;

public sealed record RecordDeclarationDto(
    Guid DocumentId,
    string RecordId,
    DateTime DeclaredAt,
    string RetentionSchedule,
    DateTime DisposalDate);

// ─── Handler ──────────────────────────────────────────────────────────────────
public sealed class DeclareRecordCommandHandler
    : IRequestHandler<DeclareRecordCommand, ApiResponse<RecordDeclarationDto>>
{
    private readonly EcmDbContext _db;

    public DeclareRecordCommandHandler(EcmDbContext db) => _db = db;

    public async Task<ApiResponse<RecordDeclarationDto>> Handle(
        DeclareRecordCommand cmd, CancellationToken ct)
    {
        var doc = await _db.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == cmd.DocumentId && !d.IsDeleted, ct);

        if (doc is null)
            return ApiResponse<RecordDeclarationDto>.Fail("الوثيقة غير موجودة");

        if (doc.IsRecord)
            return ApiResponse<RecordDeclarationDto>.Fail("تم إعلان هذه الوثيقة سجلاً رسمياً مسبقاً");

        // Lock the document as immutable record
        doc.DeclareAsRecord(cmd.RetentionSchedule, cmd.DeclaredBy);

        // Insert into Records table with a unique record number
        var recordNumber = $"REC-{DateTime.UtcNow:yyyyMMdd}-{cmd.DocumentId.ToString()[..8].ToUpper()}";

        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Records" (
                "DocumentId", "RecordNumber", "RetentionSchedule",
                "DeclaredById", "DeclaredAt", "Status"
            ) VALUES ({0}, {1}, {2}, {3}, NOW(), 'Active')
            ON CONFLICT ("DocumentId") DO NOTHING
            """,
            cmd.DocumentId, recordNumber, cmd.RetentionSchedule, cmd.DeclaredBy, ct);

        await _db.SaveChangesAsync(ct);

        // Calculate disposal date from retention schedule
        var retentionYears = ParseRetentionYears(cmd.RetentionSchedule);
        var disposalDate = DateTime.UtcNow.AddYears(retentionYears);

        return ApiResponse<RecordDeclarationDto>.Ok(new RecordDeclarationDto(
            cmd.DocumentId, recordNumber, DateTime.UtcNow,
            cmd.RetentionSchedule, disposalDate));
    }

    private static int ParseRetentionYears(string schedule) =>
        schedule.ToUpper() switch
        {
            var s when s.Contains("25") => 25,
            var s when s.Contains("15") => 15,
            var s when s.Contains("10") => 10,
            var s when s.Contains("7")  => 7,
            var s when s.Contains("5")  => 5,
            _ => 10
        };
}
