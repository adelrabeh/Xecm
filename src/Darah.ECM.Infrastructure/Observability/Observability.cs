using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Darah.ECM.Infrastructure.Observability;

/// <summary>Assigns a unique correlation ID to every HTTP request.</summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Activity.Current?.TraceId.ToString()
                         ?? Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            await _next(context);
        }
    }
}

/// <summary>Application-level metrics using System.Diagnostics.Metrics.</summary>
public sealed class EcmMetrics
{
    private static readonly Meter Meter = new("Darah.ECM", "1.0");

    public static readonly Counter<long> DocumentsCreated =
        Meter.CreateCounter<long>("ecm.documents.created");

    public static readonly Counter<long> DocumentsDownloaded =
        Meter.CreateCounter<long>("ecm.documents.downloaded");

    public static readonly Counter<long> WorkflowApproved =
        Meter.CreateCounter<long>("ecm.workflow.approved");

    public static readonly Counter<long> AuthLoginFailed =
        Meter.CreateCounter<long>("ecm.auth.login_failed");

    public static readonly Counter<long> ApiErrors =
        Meter.CreateCounter<long>("ecm.api.errors");

    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("ecm.api.request_duration_ms", unit: "ms");
}

/// <summary>Records request duration for every API call.</summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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

            EcmMetrics.RequestDuration.Record(sw.Elapsed.TotalMilliseconds);

            if (context.Response.StatusCode >= 500)
            {
                EcmMetrics.ApiErrors.Add(1);
            }
        }
    }
}
