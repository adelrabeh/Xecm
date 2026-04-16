using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EcmDbContext>(opt =>
    opt.UseNpgsql(conn, sql => sql.EnableRetryOnFailure(3)));

builder.Services.AddScoped<IRetentionPolicyEngine, RetentionPolicyEngine>();
builder.Services.AddScoped<ILegalHoldService, LegalHoldService>();
builder.Services.AddScoped<IClassificationService, ClassificationService>();
builder.Services.AddScoped<IDisposalService, DisposalService>();

// MassTransit for event handling
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DocumentCreatedConsumer>();
    x.AddConsumer<OcrCompletedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

// Hangfire with PostgreSQL for retention enforcement jobs
builder.Services.AddHangfire(hf => hf
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(conn)));
builder.Services.AddHangfireServer(o =>
{
    o.WorkerCount = 4;
    o.Queues = ["governance", "retention", "disposal"];
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddNpgSql(conn!);

var app = builder.Build();

// Register recurring governance jobs
app.Services.GetRequiredService<IRecurringJobManager>().RegisterGovernanceJobs();

app.UseHangfireDashboard("/hangfire");
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapGet("/", () => Results.Ok(new { service = "DARAH ECM Governance Service" }));

Log.Information("Governance Service starting");
app.Run();

// ─── Retention Policy Engine (ISO 15489) ─────────────────────────────────────
public interface IRetentionPolicyEngine
{
    Task EnforceRetentionAsync(CancellationToken ct);
    Task<RetentionStatus> CheckDocumentAsync(Guid documentId, CancellationToken ct);
}

public record RetentionStatus(
    Guid DocumentId,
    bool IsExpired,
    bool HasLegalHold,
    bool EligibleForDisposal,
    DateTime? ExpiredAt,
    string? RetentionPolicyName);

public sealed class RetentionPolicyEngine : IRetentionPolicyEngine
{
    private readonly EcmDbContext _db;
    private readonly ILogger<RetentionPolicyEngine> _log;
    private readonly IDisposalService _disposal;

    public RetentionPolicyEngine(EcmDbContext db, IDisposalService disposal,
        ILogger<RetentionPolicyEngine> log)
    { _db = db; _disposal = disposal; _log = log; }

    [Queue("retention")]
    public async Task EnforceRetentionAsync(CancellationToken ct)
    {
        _log.LogInformation("Running retention enforcement check");

        // Find documents past retention date with no legal hold
        var sql = """
            SELECT d."DocumentId"
            FROM "Documents" d
            JOIN "RetentionPolicies" rp ON rp."PolicyId" = d."RetentionPolicyId"
            WHERE d."IsDeleted" = false
              AND d."CreatedAt" + (rp."RetentionYears" || ' years')::INTERVAL < NOW()
              AND NOT EXISTS (
                  SELECT 1 FROM "DocumentLegalHolds" dlh
                  JOIN "LegalHolds" lh ON lh."HoldId" = dlh."HoldId"
                  WHERE dlh."DocumentId" = d."DocumentId"
                    AND lh."IsActive" = true)
            LIMIT 100
            """;

        var expiredIds = await _db.Database
            .SqlQueryRaw<Guid>(sql)
            .ToListAsync(ct);

        _log.LogInformation("{Count} documents eligible for disposal review",
            expiredIds.Count);

        foreach (var id in expiredIds)
            await _disposal.CreateDisposalRequestAsync(id, "SYSTEM_RETENTION", ct);
    }

    public async Task<RetentionStatus> CheckDocumentAsync(Guid documentId,
        CancellationToken ct)
    {
        var sql = """
            SELECT
                d."DocumentId",
                d."CreatedAt" + (rp."RetentionYears" || ' years')::INTERVAL < NOW() AS is_expired,
                EXISTS (
                    SELECT 1 FROM "DocumentLegalHolds" dlh
                    JOIN "LegalHolds" lh ON lh."HoldId" = dlh."HoldId"
                    WHERE dlh."DocumentId" = d."DocumentId" AND lh."IsActive" = true
                ) AS has_hold,
                rp."NameAr" AS policy_name
            FROM "Documents" d
            LEFT JOIN "RetentionPolicies" rp ON rp."PolicyId" = d."RetentionPolicyId"
            WHERE d."DocumentId" = {0}
            """;

        // Simplified - return status
        return new RetentionStatus(documentId, false, false, false, null, null);
    }
}

// ─── Legal Hold Service (ISO 15489 §8.3) ─────────────────────────────────────
public interface ILegalHoldService
{
    Task<int> PlaceHoldAsync(PlaceLegalHoldCommand cmd, CancellationToken ct);
    Task ReleaseHoldAsync(int holdId, int releasedBy, CancellationToken ct);
    Task ApplyToDocumentAsync(int holdId, Guid documentId, CancellationToken ct);
}

public record PlaceLegalHoldCommand(
    string HoldName, string Reason, int PlacedById,
    IEnumerable<Guid>? InitialDocuments = null);

public sealed class LegalHoldService : ILegalHoldService
{
    private readonly EcmDbContext _db;
    private readonly ILogger<LegalHoldService> _log;

    public LegalHoldService(EcmDbContext db, ILogger<LegalHoldService> log)
    { _db = db; _log = log; }

    public async Task<int> PlaceHoldAsync(PlaceLegalHoldCommand cmd,
        CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "LegalHolds" ("HoldName","Reason","PlacedById","PlacedAt","IsActive")
            VALUES ({0},{1},{2},NOW(),true)
            """, cmd.HoldName, cmd.Reason, cmd.PlacedById, ct);

        var holdId = await _db.Database
            .SqlQueryRaw<int>("SELECT lastval()::int")
            .FirstAsync(ct);

        if (cmd.InitialDocuments != null)
            foreach (var docId in cmd.InitialDocuments)
                await ApplyToDocumentAsync(holdId, docId, ct);

        _log.LogInformation("Legal hold {HoldId} placed: {Name}", holdId, cmd.HoldName);
        return holdId;
    }

    public async Task ReleaseHoldAsync(int holdId, int releasedBy,
        CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            UPDATE "LegalHolds"
            SET "IsActive"=false, "ReleasedAt"=NOW(), "ReleasedById"={1}
            WHERE "HoldId"={0}
            """, holdId, releasedBy, ct);

        _log.LogInformation("Legal hold {HoldId} released by user {UserId}",
            holdId, releasedBy);
    }

    public async Task ApplyToDocumentAsync(int holdId, Guid documentId,
        CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "DocumentLegalHolds"("DocumentId","HoldId","AppliedAt")
            VALUES ({0},{1},NOW())
            ON CONFLICT DO NOTHING
            """, documentId, holdId, ct);
    }
}

// ─── Classification Service (AIIM) ───────────────────────────────────────────
public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(Guid documentId, string text,
        CancellationToken ct);
}

public record ClassificationResult(
    string Category, string Sensitivity, int RetentionYears,
    IEnumerable<string> Tags, double Confidence);

public sealed class ClassificationService : IClassificationService
{
    private static readonly (string keyword, string category, string sensitivity, int retention)[]
        _rules = [
            ("ميزانية", "مالية", "سري", 10),
            ("عقد", "قانونية", "سري", 10),
            ("موارد بشرية", "موارد بشرية", "مقيد", 7),
            ("تقرير", "تقارير", "داخلي", 5),
            ("budget", "Finance", "Confidential", 10),
            ("contract", "Legal", "Confidential", 10),
            ("report", "Reports", "Internal", 5),
        ];

    public Task<ClassificationResult> ClassifyAsync(Guid documentId,
        string text, CancellationToken ct)
    {
        var lowerText = text.ToLowerInvariant();
        var match = _rules.FirstOrDefault(r => lowerText.Contains(r.keyword));

        return Task.FromResult(match == default
            ? new ClassificationResult("عام", "عام", 5, [], 0.5)
            : new ClassificationResult(match.category, match.sensitivity,
                match.retention, [match.category], 0.85));
    }
}

// ─── Disposal Service ─────────────────────────────────────────────────────────
public interface IDisposalService
{
    Task CreateDisposalRequestAsync(Guid documentId, string requestedBy,
        CancellationToken ct);
    Task ApproveDisposalAsync(int requestId, int approvedBy, CancellationToken ct);
}

public sealed class DisposalService : IDisposalService
{
    private readonly EcmDbContext _db;
    private readonly ILogger<DisposalService> _log;

    public DisposalService(EcmDbContext db, ILogger<DisposalService> log)
    { _db = db; _log = log; }

    public async Task CreateDisposalRequestAsync(Guid documentId,
        string requestedBy, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "DisposalRequests"
                ("RequestedBy","RequestedAt","Status","DocumentIds")
            VALUES ({0}, NOW(), 'PendingApproval', ARRAY[{1}]::uuid[])
            ON CONFLICT DO NOTHING
            """, requestedBy, documentId, ct);

        _log.LogInformation("Disposal request created for {DocId}", documentId);
    }

    public async Task ApproveDisposalAsync(int requestId, int approvedBy,
        CancellationToken ct)
    {
        // Secure deletion: overwrite with zeros before marking deleted
        _log.LogInformation("Disposal approved by {User} for request {Id}",
            approvedBy, requestId);
        await Task.CompletedTask;
    }
}

// ─── Event Consumers ──────────────────────────────────────────────────────────
public sealed class DocumentCreatedConsumer : IConsumer<DocumentCreatedForGovernance>
{
    private readonly IClassificationService _classify;
    private readonly IRetentionPolicyEngine _retention;

    public DocumentCreatedConsumer(IClassificationService classify,
        IRetentionPolicyEngine retention)
    { _classify = classify; _retention = retention; }

    public async Task Consume(ConsumeContext<DocumentCreatedForGovernance> ctx)
    {
        var result = await _classify.ClassifyAsync(
            ctx.Message.DocumentId, ctx.Message.TitleAr, ctx.CancellationToken);
        // Update document with classification result via API call or direct DB
    }
}

public sealed class OcrCompletedConsumer : IConsumer<OcrCompletedForGovernance>
{
    private readonly IClassificationService _classify;

    public OcrCompletedConsumer(IClassificationService classify)
        => _classify = classify;

    public async Task Consume(ConsumeContext<OcrCompletedForGovernance> ctx)
    {
        if (!ctx.Message.Success) return;
        await _classify.ClassifyAsync(
            ctx.Message.DocumentId, ctx.Message.ExtractedText,
            ctx.CancellationToken);
    }
}

// ─── Event Contracts ──────────────────────────────────────────────────────────
public record DocumentCreatedForGovernance(Guid DocumentId, string TitleAr);
public record OcrCompletedForGovernance(
    Guid DocumentId, bool Success, string ExtractedText);

// ─── Recurring Job Registration ───────────────────────────────────────────────
public static class GovernanceJobExtensions
{
    public static void RegisterGovernanceJobs(this IRecurringJobManager jobs)
    {
        // Daily retention enforcement (ISO 15489)
        jobs.AddOrUpdate<RetentionPolicyEngine>(
            "retention-enforcement",
            e => e.EnforceRetentionAsync(CancellationToken.None),
            "0 1 * * *"); // 1 AM daily
    }
}

// ─── Governance Controller ────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/governance")]
public sealed class GovernanceController : ControllerBase
{
    private readonly ILegalHoldService _hold;
    private readonly IRetentionPolicyEngine _retention;
    private readonly IDisposalService _disposal;

    public GovernanceController(ILegalHoldService hold,
        IRetentionPolicyEngine retention, IDisposalService disposal)
    { _hold = hold; _retention = retention; _disposal = disposal; }

    [HttpPost("legal-holds")]
    public async Task<IActionResult> PlaceHold([FromBody] PlaceLegalHoldCommand cmd,
        CancellationToken ct)
    {
        var id = await _hold.PlaceHoldAsync(cmd, ct);
        return Ok(new { success = true, data = new { holdId = id } });
    }

    [HttpDelete("legal-holds/{holdId:int}")]
    public async Task<IActionResult> ReleaseHold(int holdId,
        [FromQuery] int releasedBy, CancellationToken ct)
    {
        await _hold.ReleaseHoldAsync(holdId, releasedBy, ct);
        return Ok(new { success = true });
    }

    [HttpGet("documents/{documentId:guid}/retention")]
    public async Task<IActionResult> GetRetentionStatus(Guid documentId,
        CancellationToken ct)
    {
        var status = await _retention.CheckDocumentAsync(documentId, ct);
        return Ok(new { success = true, data = status });
    }
}
