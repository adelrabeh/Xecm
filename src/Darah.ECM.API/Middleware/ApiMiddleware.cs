namespace Darah.ECM.API.Middleware;
using System.Diagnostics;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        ctx.Response.Headers.Append("X-Frame-Options", "DENY");
        ctx.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        ctx.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        if (ctx.Request.IsHttps) ctx.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        await _next(ctx);
    }
}

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase) { "/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/change-password" };
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger) { _next = next; _logger = logger; }
    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        await _next(ctx);
        sw.Stop();
        var path = Sensitive.Contains(ctx.Request.Path.Value ?? "") ? "[SENSITIVE]" : ctx.Request.Path.Value;
        _logger.LogInformation("{Method} {Path} → {Status} [{Elapsed}ms] {IP}", ctx.Request.Method, path, ctx.Response.StatusCode, sw.ElapsedMilliseconds, ctx.Connection.RemoteIpAddress);
    }
}

public sealed class TraceIdMiddleware
{
    private readonly RequestDelegate _next;
    public TraceIdMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext ctx)
    {
        var traceId = ctx.Request.Headers["X-Trace-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        ctx.Items["TraceId"] = traceId;
        ctx.Response.Headers.Append("X-Trace-Id", traceId);
        await _next(ctx);
    }
}
