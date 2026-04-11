namespace Darah.ECM.Application.Common.Models;

// ─── STANDARD RESPONSE ────────────────────────────────────────────────────────
public sealed class ApiResponse<T>
{
    public bool               Success   { get; private set; }
    public string?            Message   { get; private set; }
    public T?                 Data      { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();
    public DateTime           Timestamp { get; private set; } = DateTime.UtcNow;
    public string?            TraceId   { get; set; }

    private ApiResponse() { }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors?.ToArray() ?? Array.Empty<string>() };

    public static ApiResponse<T> ValidationFail(IEnumerable<string> errors) =>
        new() { Success = false, Message = "Validation failed", Errors = errors.ToArray() };

    public static ApiResponse<T> Unauthorized(string message = "غير مصرح بالوصول") =>
        new() { Success = false, Message = message };
}

// ─── PAGINATION ───────────────────────────────────────────────────────────────
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items      { get; init; } = Array.Empty<T>();
    public int              TotalCount { get; init; }
    public int              Page       { get; init; }
    public int              PageSize   { get; init; }
    public int  TotalPages    => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrevious   => Page > 1;
    public bool HasNext       => Page < TotalPages;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new() { Items = Array.Empty<T>(), TotalCount = 0, Page = page, PageSize = pageSize };
}

namespace Darah.ECM.Application.Common.Abstractions;

/// <summary>
/// Application-level file upload abstraction — decouples Application layer from
/// ASP.NET Core's IFormFile. The API layer creates this from IFormFile and passes it down.
/// This preserves Clean Architecture boundary: Application has NO dependency on web framework types.
/// </summary>
public sealed class FileUploadRequest : IDisposable
{
    public string FileName    { get; }
    public string ContentType { get; }
    public long   Length      { get; }
    public Stream Content     { get; }

    public FileUploadRequest(string fileName, string contentType, long length, Stream content)
    {
        FileName    = fileName;
        ContentType = contentType;
        Length      = length;
        Content     = content;
    }

    public void Dispose() => Content.Dispose();
}

namespace Darah.ECM.Application.Common.Behaviors;

using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

// ─── VALIDATION BEHAVIOR ──────────────────────────────────────────────────────
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var ctx      = new ValidationContext<TRequest>(request);
        var failures = _validators.Select(v => v.Validate(ctx))
                                  .SelectMany(r => r.Errors)
                                  .Where(f => f != null)
                                  .ToList();

        if (failures.Any()) throw new ValidationException(failures);
        return await next();
    }
}

// ─── LOGGING BEHAVIOR ─────────────────────────────────────────────────────────
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogDebug("Handling {Request}", name);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            if (sw.ElapsedMilliseconds > 2_000)
                _logger.LogWarning("Slow handler {Request}: {Elapsed}ms", name, sw.ElapsedMilliseconds);
            else
                _logger.LogDebug("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Handler {Request} failed after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
