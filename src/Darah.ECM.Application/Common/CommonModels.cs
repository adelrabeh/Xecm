namespace Darah.ECM.Application.Common.Models;

// ─────────────────────────────────────────────────────────────
// STANDARD RESPONSE WRAPPER
// ─────────────────────────────────────────────────────────────
public sealed class ApiResponse<T>
{
    public bool Success { get; private set; }
    public string? Message { get; private set; }
    public T? Data { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }

    private ApiResponse() { }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
        { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) => new()
        { Success = false, Message = message, Errors = errors?.ToList() ?? Array.Empty<string>() };

    public static ApiResponse<T> ValidationFail(IEnumerable<string> errors) => new()
        { Success = false, Message = "Validation failed", Errors = errors.ToList() };

    public static ApiResponse<T> Unauthorized(string message = "غير مصرح بالوصول") => new()
        { Success = false, Message = message };
}

// ─────────────────────────────────────────────────────────────
// PAGINATION
// ─────────────────────────────────────────────────────────────
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) => new()
        { Items = Array.Empty<T>(), TotalCount = 0, Page = page, PageSize = pageSize };
}

// ─────────────────────────────────────────────────────────────
// PAGINATION QUERY BASE
// ─────────────────────────────────────────────────────────────
public abstract record PagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "CreatedAt";
    public string SortDirection { get; init; } = "DESC";

    public int SafePageSize => Math.Min(Math.Max(PageSize, 1), 100);
    public int Skip => (Math.Max(Page, 1) - 1) * SafePageSize;
}

namespace Darah.ECM.Application.Common.Behaviors;

using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────
// VALIDATION BEHAVIOR
// ─────────────────────────────────────────────────────────────
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var ctx = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(ctx))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}

// ─────────────────────────────────────────────────────────────
// LOGGING BEHAVIOR
// ─────────────────────────────────────────────────────────────
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogDebug("Handling {Request}", name);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            if (sw.ElapsedMilliseconds > 2000)
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

// ─────────────────────────────────────────────────────────────
// DOMAIN EVENT DISPATCH BEHAVIOR
// ─────────────────────────────────────────────────────────────
public sealed class DomainEventDispatchBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;
    private readonly IEventBus _eventBus;

    public DomainEventDispatchBehavior(IUnitOfWork uow, IEventBus eventBus)
        { _uow = uow; _eventBus = eventBus; }

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();

        // After the handler commits, dispatch domain events collected on entities
        var events = CollectDomainEvents();
        foreach (var @event in events)
            await _eventBus.PublishAsync(@event, ct);

        return response;
    }

    private IEnumerable<IDomainEvent> CollectDomainEvents()
    {
        // In a full EF Core implementation, this would iterate DbContext ChangeTracker
        // For now, return empty — events collected per aggregate root
        return Enumerable.Empty<IDomainEvent>();
    }
}
