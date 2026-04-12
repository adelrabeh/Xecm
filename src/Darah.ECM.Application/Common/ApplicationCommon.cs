namespace Darah.ECM.Application.Common.Models;

// ─── STANDARD API RESPONSE ────────────────────────────────────────────────────
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
    public int  TotalPages  => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext     => Page < TotalPages;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new() { Items = Array.Empty<T>(), TotalCount = 0, Page = page, PageSize = pageSize };
}
