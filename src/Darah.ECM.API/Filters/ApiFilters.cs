using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Darah.ECM.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission) : base(typeof(PermissionAuthorizationFilter))
        => Arguments = new object[] { permission };
}

public sealed class PermissionAuthorizationFilter : IAuthorizationFilter
{
    private readonly string _permission;
    private readonly ICurrentUser _currentUser;
    public PermissionAuthorizationFilter(string permission, ICurrentUser currentUser) { _permission = permission; _currentUser = currentUser; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!_currentUser.IsAuthenticated) { context.Result = new UnauthorizedResult(); return; }
        if (!_currentUser.HasPermission(_permission))
            context.Result = new ObjectResult(ApiResponse<object>.Unauthorized($"ليس لديك صلاحية '{_permission}'")) { StatusCode = 403 };
    }
}

public sealed class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IWebHostEnvironment _env;
    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment env) { _logger = logger; _env = env; }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception on {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        var (code, msg) = context.Exception switch
        {
            ValidationException vex => (400, string.Join(" | ", vex.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException => (403, "غير مصرح بالوصول"),
            KeyNotFoundException => (404, "المورد غير موجود"),
            InvalidOperationException iex => (422, iex.Message),
            _ => (500, _env.IsDevelopment() ? context.Exception.Message : "حدث خطأ داخلي في النظام")
        };
        context.Result = new ObjectResult(ApiResponse<object>.Fail(msg)) { StatusCode = code };
        context.ExceptionHandled = true;
    }
}
