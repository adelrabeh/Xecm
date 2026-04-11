// ============================================================
// API FILTERS
// ============================================================
namespace Darah.ECM.API.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Services;

/// <summary>Enforces permission check before action executes.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(typeof(PermissionFilter)) => Arguments = new object[] { permission };
}

public class PermissionFilter : IAuthorizationFilter
{
    private readonly string _permission;
    private readonly ICurrentUser _user;

    public PermissionFilter(string permission, ICurrentUser user)
        { _permission = permission; _user = user; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!_user.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (!_user.HasPermission(_permission))
            context.Result = new ObjectResult(
                ApiResponse<object>.Unauthorized("ليس لديك صلاحية لتنفيذ هذا الإجراء"))
                { StatusCode = 403 };
    }
}

/// <summary>Global exception handling filter — prevents stack trace leakage.</summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment env)
        { _logger = logger; _env = env; }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception on {Path}", context.HttpContext.Request.Path);

        var (status, message) = context.Exception switch
        {
            FluentValidation.ValidationException vex => (400,
                string.Join("; ", vex.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException => (403, "غير مصرح بالوصول"),
            KeyNotFoundException => (404, "المورد المطلوب غير موجود"),
            InvalidOperationException iex => (422, iex.Message),
            _ => (500, _env.IsDevelopment()
                ? context.Exception.Message
                : "حدث خطأ داخلي في النظام. يرجى المحاولة لاحقاً أو التواصل مع الدعم الفني")
        };

        context.Result = new ObjectResult(ApiResponse<object>.Fail(message)) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

// ============================================================
// API MODELS (Request / Response contracts)
// ============================================================
namespace Darah.ECM.API.Models.Requests;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record WorkflowCommentRequest(string? Comment);
public record DelegateTaskRequest(int DelegateToUserId, string? Comment);
public record ResolveConflictRequest(int FieldId, string Resolution);
public record UpdateSettingRequest(string Value);
public record AssignRoleRequest(int RoleId);

namespace Darah.ECM.API.Models.Responses;

public record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    int UserId,
    string Username,
    string FullNameAr,
    string? FullNameEn,
    string Language,
    bool MustChangePassword)
{
    // RefreshToken deliberately excluded from body — set in HttpOnly cookie
    public string? RefreshToken { get; set; }
}

public record UserProfileResponse(
    int UserId,
    string Username,
    string Email,
    string FullNameAr,
    string? FullNameEn,
    string Language,
    string? JobTitle,
    bool MFAEnabled,
    IEnumerable<string> Permissions);

// ============================================================
// CONTROLLERS
// ============================================================
namespace Darah.ECM.API.Controllers.v1;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Darah.ECM.API.Filters;
using Darah.ECM.API.Models.Requests;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Application.Documents.Queries;
using Darah.ECM.Application.Documents.DTOs;
using Darah.ECM.Application.Workflow.Commands;
using Darah.ECM.Application.Workflow.Queries;
using Darah.ECM.Application.Search.Queries;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class EcmControllerBase : ControllerBase
{
    protected readonly IMediator Mediator;
    protected EcmControllerBase(IMediator mediator) => Mediator = mediator;
}

// ── AUTH ──────────────────────────────────────────────────────
[AllowAnonymous]
[ApiController]
[Route("api/v1/auth")]
public class AuthController : EcmControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IMediator mediator, IAuthService auth) : base(mediator) => _auth = auth;

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.LoginAsync(req.Username, req.Password,
            GetClientIp(), GetUserAgent());
        if (!result.Success) return Unauthorized(result);

        Response.Cookies.Append("ecm_refresh", result.Data!.RefreshToken!, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8), Path = "/api/v1/auth"
        });
        result.Data.RefreshToken = null;
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh()
    {
        var token = Request.Cookies["ecm_refresh"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(ApiResponse<LoginResponse>.Fail("جلسة منتهية"));
        var result = await _auth.RefreshTokenAsync(token, GetClientIp());
        if (!result.Success) return Unauthorized(result);
        Response.Cookies.Append("ecm_refresh", result.Data!.RefreshToken!, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8), Path = "/api/v1/auth"
        });
        result.Data.RefreshToken = null;
        return Ok(result);
    }

    [Authorize, HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var token = Request.Cookies["ecm_refresh"];
        if (!string.IsNullOrEmpty(token)) await _auth.RevokeSessionAsync(token);
        Response.Cookies.Delete("ecm_refresh", new CookieOptions { Path = "/api/v1/auth" });
        return Ok(ApiResponse<bool>.Ok(true, "تم تسجيل الخروج"));
    }

    [Authorize, HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var result = await _auth.ChangePasswordAsync(req.CurrentPassword, req.NewPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private string GetUserAgent() => Request.Headers.UserAgent.ToString();
}

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(string username, string password, string ip, string agent);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken, string ip);
    Task RevokeSessionAsync(string refreshToken);
    Task<ApiResponse<bool>> ChangePasswordAsync(string current, string newPass);
}

// ── DOCUMENTS ────────────────────────────────────────────────
[Authorize]
public class DocumentsController : EcmControllerBase
{
    public DocumentsController(IMediator mediator) : base(mediator) { }

    [HttpGet, RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentListItemDto>>>> GetAll(
        [FromQuery] int? libraryId, [FromQuery] int? folderId,
        [FromQuery] int? documentTypeId, [FromQuery] string? search,
        [FromQuery] Guid? workspaceId,
        [FromQuery] string sortBy = "CreatedAt", [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new SearchDocumentsQuery(
            search, documentTypeId, libraryId, folderId,
            null, null, null, null, null, null, null, null, workspaceId,
            sortBy, sortDir, page, Math.Min(pageSize, 100))));

    [HttpGet("{id:guid}"), RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Get(Guid id)
    {
        var result = await Mediator.Send(new GetDocumentByIdQuery(id));
        return result.Data is null ? NotFound(ApiResponse<DocumentDto>.Fail("الوثيقة غير موجودة")) : Ok(result);
    }

    [HttpPost, RequirePermission("documents.create")]
    [RequestSizeLimit(536_870_912)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Create([FromForm] CreateDocumentCommand cmd)
    {
        var result = await Mediator.Send(cmd);
        return result.Success
            ? CreatedAtAction(nameof(Get), new { id = result.Data!.DocumentId }, result)
            : BadRequest(result);
    }

    [HttpGet("{id:guid}/versions"), RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersions(Guid id)
        => Ok(await Mediator.Send(new GetDocumentVersionsQuery(id)));

    [HttpPost("{id:guid}/checkout"), RequirePermission("documents.checkout")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckOut(Guid id)
    {
        var result = await Mediator.Send(new CheckOutDocumentCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/download"), RequirePermission("documents.download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? versionId)
    {
        var result = await Mediator.Send(new GetDocumentDownloadQuery(id, versionId));
        if (!result.Success) return NotFound(result.Message);
        // Actual file stream returned by FileStorageService in infrastructure
        return Ok(new { result.Data!.StorageKey, result.Data.FileName, result.Data.ContentType });
    }

    [HttpDelete("{id:guid}"), RequirePermission("documents.delete")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, [FromBody] string? reason)
    {
        var result = await Mediator.Send(new DeleteDocumentCommand(id, reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ── WORKFLOW ─────────────────────────────────────────────────
[Authorize]
public class WorkflowController : EcmControllerBase
{
    public WorkflowController(IMediator mediator) : base(mediator) { }

    [HttpPost("submit/{documentId:guid}"), RequirePermission("workflow.submit")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Submit(
        Guid documentId, [FromBody] SubmitToWorkflowCommand cmd)
    {
        var result = await Mediator.Send(cmd with { DocumentId = documentId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<ApiResponse<PagedResult<InboxItemDto>>>> Inbox(
        [FromQuery] bool overdueOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetWorkflowInboxQuery(OverdueOnly: overdueOnly, Page: page, PageSize: pageSize)));

    [HttpGet("tasks/{taskId:int}")]
    public async Task<ActionResult<ApiResponse<WorkflowTaskDto>>> GetTask(int taskId)
    {
        var result = await Mediator.Send(new GetWorkflowTaskDetailQuery(taskId));
        return result.Data is null ? NotFound(ApiResponse<WorkflowTaskDto>.Fail("المهمة غير موجودة")) : Ok(result);
    }

    [HttpPost("tasks/{taskId:int}/approve"), RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Approve(int taskId, [FromBody] WorkflowCommentRequest req)
        => HandleAction(await Mediator.Send(new WorkflowActionCommand(taskId, "Approve", req.Comment, null)));

    [HttpPost("tasks/{taskId:int}/reject"), RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(int taskId, [FromBody] WorkflowCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Comment)) return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الرفض"));
        return HandleAction(await Mediator.Send(new WorkflowActionCommand(taskId, "Reject", req.Comment, null)));
    }

    [HttpPost("tasks/{taskId:int}/return"), RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Return(int taskId, [FromBody] WorkflowCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Comment)) return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الإرجاع"));
        return HandleAction(await Mediator.Send(new WorkflowActionCommand(taskId, "Return", req.Comment, null)));
    }

    [HttpPost("tasks/{taskId:int}/delegate"), RequirePermission("workflow.delegate")]
    public async Task<ActionResult<ApiResponse<bool>>> Delegate(int taskId, [FromBody] DelegateTaskRequest req)
        => HandleAction(await Mediator.Send(new WorkflowActionCommand(taskId, "Delegate", req.Comment, req.DelegateToUserId)));

    [HttpGet("instances/{instanceId:int}/history")]
    public async Task<ActionResult<ApiResponse<List<WorkflowActionDto>>>> History(int instanceId)
        => Ok(await Mediator.Send(new GetWorkflowHistoryQuery(instanceId)));

    private ActionResult<ApiResponse<bool>> HandleAction(ApiResponse<bool> result)
        => result.Success ? Ok(result) : BadRequest(result);
}

// ── SEARCH ───────────────────────────────────────────────────
[Authorize]
public class SearchController : EcmControllerBase
{
    public SearchController(IMediator mediator) : base(mediator) { }

    [HttpGet("quick")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentListItemDto>>>> Quick(
        [FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(ApiResponse<PagedResult<DocumentListItemDto>>.Fail("يجب أن يكون البحث حرفين على الأقل"));
        return Ok(await Mediator.Send(new AdvancedSearchQuery(q, null, null, null, null, null,
            null, null, null, null, null, null, null, Page: page, PageSize: pageSize)));
    }

    [HttpPost("advanced")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentListItemDto>>>> Advanced(
        [FromBody] AdvancedSearchQuery query)
        => Ok(await Mediator.Send(query with { PageSize = Math.Min(query.PageSize, 100) }));

    [HttpGet("saved")]
    public async Task<ActionResult<ApiResponse<List<SavedSearchDto>>>> GetSaved()
        => Ok(await Mediator.Send(new GetSavedSearchesQuery()));

    [HttpPost("saved")]
    public async Task<ActionResult<ApiResponse<SavedSearchDto>>> Save([FromBody] CreateSavedSearchCommand cmd)
    {
        var result = await Mediator.Send(cmd);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ============================================================
// API EXTENSIONS — DI registration + Program.cs
// ============================================================
namespace Darah.ECM.API.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Hangfire;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEcmInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Database
        services.AddDbContext<EcmDbContext>(o => o.UseSqlServer(
            config.GetConnectionString("DefaultConnection"),
            sql => { sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null); sql.CommandTimeout(60); }));

        // Repositories & UoW
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthServiceImpl>();
        services.AddSingleton<IEventBus, InProcessEventBus>();

        // Event handlers
        services.AddScoped<IEventHandler<DocumentApprovedEvent>, DocumentApprovedEventHandler>();
        services.AddScoped<IEventHandler<SLABreachedEvent>, SLABreachedEventHandler>();
        services.AddScoped<IEventHandler<WorkspaceLinkedToExternalEvent>, WorkspaceLinkedEventHandler>();
        services.AddScoped<IEventHandler<RetentionExpiredEvent>, RetentionExpiredEventHandler>();
        services.AddScoped<IEventHandler<WorkspaceArchivedEvent>, WorkspaceArchivedEventHandler>();

        return services;
    }

    public static IServiceCollection AddEcmAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not set");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true, ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
                };
                o.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        if (ctx.Exception is SecurityTokenExpiredException)
                            ctx.Response.Headers.Append("X-Token-Expired", "true");
                        return Task.CompletedTask;
                    }
                };
            });
        return services;
    }

    public static IServiceCollection AddEcmSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DARAH ECM API", Version = "v1",
                Description = "Enterprise Content Management — دارة الملك عبدالعزيز"
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization", Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {{
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }});
        });
        return services;
    }
}

// ── PROGRAM.CS ────────────────────────────────────────────────
// (Placed here as a code reference — in actual project this is at root)
public static class ProgramStartup
{
    public static void Configure(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext().Enrich.WithMachineName()
            .WriteTo.Console().WriteTo.File("logs/ecm-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        builder.Host.UseSerilog();

        var services = builder.Services;
        var config = builder.Configuration;

        services.AddHttpContextAccessor();
        services.AddEcmInfrastructure(config);
        services.AddEcmAuthentication(config);
        services.AddEcmSwagger();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(CreateDocumentCommand).Assembly,
                typeof(SubmitToWorkflowCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<CreateDocumentCommandValidator>();
        services.AddAutoMapper(typeof(CreateDocumentCommand).Assembly);
        services.AddMemoryCache();

        services.AddHangfire(c => c.UseSqlServerStorage(config.GetConnectionString("DefaultConnection")));
        services.AddHangfireServer(o => { o.WorkerCount = 4; });

        services.AddCors(o => o.AddPolicy("ECM_Frontend", p =>
            p.WithOrigins(config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
             .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

        services.AddControllers(o => o.Filters.Add<GlobalExceptionFilter>())
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddHealthChecks()
            .AddDbContextCheck<EcmDbContext>("database")
            .AddHangfire(o => o.MinimumAvailableServers = 1);
    }
}
