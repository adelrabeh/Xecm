using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Darah.ECM.Infrastructure.Observability;

// ─── CORRELATION ID ───────────────────────────────────────────────────────────
/// <summary>
/// Assigns a unique correlation ID to every HTTP request.
/// Reads from X-Correlation-Id header (from upstream gateway) or generates a new one.
/// Adds it to response headers and Serilog LogContext for log correlation.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Activity.Current?.TraceId.ToString()
                         ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Push to Serilog so every log line in this request includes CorrelationId
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath",   context.Request.Path.Value))
        using (LogContext.PushProperty("UserId",        context.User?.FindFirst("uid")?.Value ?? "anonymous"))
        {
            await _next(context);
        }
    }
}

// ─── METRICS ──────────────────────────────────────────────────────────────────
/// <summary>
/// Application-level metrics using System.Diagnostics.Metrics (.NET 8).
/// Exposed via /metrics endpoint for Prometheus scraping.
///
/// Counters tracked:
///   - ecm.documents.created     (total documents uploaded)
///   - ecm.documents.downloaded  (total downloads)
///   - ecm.workflow.approved     (total approvals)
///   - ecm.workflow.rejected     (total rejections)
///   - ecm.auth.login_failed     (brute-force monitoring)
///   - ecm.api.errors            (error rate)
///
/// Histograms tracked:
///   - ecm.api.request_duration_ms  (latency per endpoint)
///   - ecm.files.size_bytes         (file size distribution)
/// </summary>
public sealed class EcmMetrics
{
    private static readonly Meter Meter = new("Darah.ECM", "1.0");

    // Counters
    public static readonly Counter<long> DocumentsCreated    = Meter.CreateCounter<long>("ecm.documents.created");
    public static readonly Counter<long> DocumentsDownloaded = Meter.CreateCounter<long>("ecm.documents.downloaded");
    public static readonly Counter<long> WorkflowApproved    = Meter.CreateCounter<long>("ecm.workflow.approved");
    public static readonly Counter<long> WorkflowRejected    = Meter.CreateCounter<long>("ecm.workflow.rejected");
    public static readonly Counter<long> AuthLoginFailed     = Meter.CreateCounter<long>("ecm.auth.login_failed");
    public static readonly Counter<long> ApiErrors           = Meter.CreateCounter<long>("ecm.api.errors");
    public static readonly Counter<long> SyncEventsProcessed = Meter.CreateCounter<long>("ecm.sync.events_processed");
    public static readonly Counter<long> SyncConflicts       = Meter.CreateCounter<long>("ecm.sync.conflicts");

    // Histograms
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "ecm.api.request_duration_ms", unit: "ms");
    public static readonly Histogram<long> FileSizeBytes = Meter.CreateHistogram<long>(
        "ecm.files.size_bytes", unit: "bytes");
}

// ─── METRICS MIDDLEWARE ───────────────────────────────────────────────────────
/// <summary>Records request duration and error counts for every API call.</summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    public MetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var tags = new TagList
            {
                { "method",   context.Request.Method },
                { "endpoint", context.Request.Path.Value?.Split('/', 4).Take(4).LastOrDefault() ?? "unknown" },
                { "status",   context.Response.StatusCode.ToString() }
            };
            EcmMetrics.RequestDuration.Record(sw.Elapsed.TotalMilliseconds, tags);

            if (context.Response.StatusCode >= 500)
                EcmMetrics.ApiErrors.Add(1, tags);
        }
    }
}

// ─── HEALTH CHECKS ────────────────────────────────────────────────────────────
/// <summary>Checks that the file storage root is accessible and has sufficient free space.</summary>
public sealed class FileStorageHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;
    private const long MinFreeSpaceBytes = 512L * 1024 * 1024; // 512 MB minimum

    public FileStorageHealthCheck(IConfiguration config) => _config = config;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var path = _config["Storage:LocalPath"] ?? "ecm-storage";
            if (!Directory.Exists(path))
                return Task.FromResult(HealthCheckResult.Degraded($"Storage path not found: {path}"));

            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            if (drive.AvailableFreeSpace < MinFreeSpaceBytes)
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space: {drive.AvailableFreeSpace / 1_073_741_824.0:F2} GB free"));

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Storage OK — {drive.AvailableFreeSpace / 1_073_741_824.0:F2} GB free"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("File storage check failed", ex));
        }
    }
}

/// <summary>Checks that Hangfire has active workers to process background jobs.</summary>
public sealed class HangfireHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var mon = Hangfire.JobStorage.Current.GetMonitoringApi();
            var servers = mon.Servers();
            if (!servers.Any())
                return Task.FromResult(HealthCheckResult.Degraded("No Hangfire servers running"));

            var queued = mon.EnqueuedCount("default");
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Hangfire OK — {servers.Count} server(s), {queued} queued"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire check failed", ex));
        }
    }
}

// ─── DI EXTENSION ─────────────────────────────────────────────────────────────
public static class ObservabilityExtensions
{
    public static IServiceCollection AddEcmObservability(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHealthChecks()
            .AddCheck<FileStorageHealthCheck>("file_storage", tags: new[] { "storage" })
            .AddCheck<HangfireHealthCheck>("hangfire", tags: new[] { "jobs" });

        // OpenTelemetry metrics (for Prometheus scraping)
        services.AddOpenTelemetry()
            .WithMetrics(builder => builder
                .AddMeter("Darah.ECM")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        return services;
    }
}
