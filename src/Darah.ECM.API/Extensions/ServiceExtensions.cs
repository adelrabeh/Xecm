using Darah.ECM.Application.Common.Behaviors;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Events.Document;
using Darah.ECM.Domain.Events.Workspace;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.FileStorage.Local;
using Darah.ECM.Infrastructure.Logging;
using Darah.ECM.Infrastructure.Messaging;
using Darah.ECM.Infrastructure.Persistence;
using Darah.ECM.Infrastructure.Persistence.Repositories;
using Darah.ECM.Infrastructure.Security;
using FluentValidation;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Darah.ECM.API.Extensions;

public static class ServiceCollectionExtensions
{
    // ─── INFRASTRUCTURE ───────────────────────────────────────────────────────
    public static IServiceCollection AddEcmInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Database
        services.AddDbContext<EcmDbContext>(o => o.UseSqlServer(
            config.GetConnectionString("DefaultConnection"),
            sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.CommandTimeout(60);
                sql.MigrationsAssembly("Darah.ECM.Infrastructure");
            }));

        // Repositories & Unit of Work
        services.AddScoped<IDocumentRepository,        DocumentRepository>();
        services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<IUserRepository,            UserRepository>();
        services.AddScoped<IWorkflowRepository,        WorkflowRepository>();
        services.AddScoped<IUnitOfWork,                UnitOfWork>();

        // File storage — swap provider via config ("LocalFileSystem" | "AWSS3" | "AzureBlob")
        var storageProvider = config["Storage:Provider"] ?? "LocalFileSystem";
        if (storageProvider == "AWSS3")
            services.AddScoped<IFileStorageService, S3FileStorageService>();
        else
            services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Core services
        services.AddScoped<IAuditService,   AuditService>();
        services.AddScoped<ITokenService,   JwtTokenService>();
        services.AddSingleton<IEventBus,    InProcessEventBus>();

        // Current user (singleton accessor, scoped user)
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUserService>());

        return services;
    }

    // ─── AUTHENTICATION ───────────────────────────────────────────────────────
    public static IServiceCollection AddEcmAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(
                                                System.Text.Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer   = true,  ValidIssuer   = config["Jwt:Issuer"],
                    ValidateAudience = true,  ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true,  ClockSkew     = TimeSpan.FromSeconds(30)
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

        services.AddAuthorization();
        return services;
    }

    // ─── APPLICATION (MediatR + FluentValidation) ─────────────────────────────
    public static IServiceCollection AddEcmApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateDocumentCommand>();
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<CreateDocumentCommandValidator>();
        return services;
    }

    // ─── SWAGGER ──────────────────────────────────────────────────────────────
    public static IServiceCollection AddEcmSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "DARAH ECM API",
                Version     = "v1",
                Description = "Enterprise Content Management — دارة الملك عبدالعزيز"
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.ApiKey,
                Scheme       = "Bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "أدخل: Bearer {token}"
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {{
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                        { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }});
        });
        return services;
    }

    // ─── HANGFIRE ─────────────────────────────────────────────────────────────
    public static IServiceCollection AddEcmBackgroundJobs(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(config.GetConnectionString("DefaultConnection")));

        services.AddHangfireServer(o => { o.WorkerCount = 4; });
        return services;
    }
}

// ─── PROGRAM STARTUP ──────────────────────────────────────────────────────────
public static class ProgramStartup
{
    public static WebApplication BuildApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config  = builder.Configuration;

        // Logging
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console()
            .WriteTo.File("logs/ecm-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
        builder.Host.UseSerilog();

        var services = builder.Services;

        services.AddHttpContextAccessor();
        services.AddEcmInfrastructure(config);
        services.AddEcmAuthentication(config);
        services.AddEcmApplication();
        services.AddEcmSwagger();
        services.AddEcmBackgroundJobs(config);
        services.AddMemoryCache();

        services.AddCors(o => o.AddPolicy("ECM_Frontend", p =>
            p.WithOrigins(config.GetSection("Cors:AllowedOrigins")
                               .Get<string[]>() ?? new[] { "http://localhost:5173" })
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials()));

        services.AddControllers(o =>
        {
            o.Filters.Add<GlobalExceptionFilter>();
        })
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
            o.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddHealthChecks()
            .AddDbContextCheck<EcmDbContext>("database")
            .AddHangfire(o => o.MinimumAvailableServers = 1);

        return builder.Build();
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseSecurityHeaders();
        app.UseRequestLogging();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DARAH ECM API v1");
                c.DisplayRequestDuration();
            });
        }

        app.UseHttpsRedirection();
        app.UseCors("ECM_Frontend");
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHangfireDashboard("/admin/jobs", new DashboardOptions
        {
            Authorization = new[] { new HangfireAdminAuthFilter() }
        });

        // Recurring jobs
        RecurringJob.AddOrUpdate<SlaCheckerJob>(
            "sla-check",
            j => j.CheckAsync(CancellationToken.None),
            "*/15 * * * *");

        RecurringJob.AddOrUpdate<RetentionPolicyJob>(
            "retention-check",
            j => j.ProcessAsync(CancellationToken.None),
            Cron.Daily(2, 0));

        app.MapControllers();
        app.MapHealthChecks("/health");
    }
}

// Hangfire auth filter placeholder
public sealed class HangfireAdminAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true
            && http.User.FindFirst("perm")?.Value == "admin.system";
    }
}

// Hangfire job placeholders
public sealed class SlaCheckerJob
{
    public Task CheckAsync(CancellationToken ct) => Task.CompletedTask; // implemented in Jobs/
}
public sealed class RetentionPolicyJob
{
    public Task ProcessAsync(CancellationToken ct) => Task.CompletedTask; // implemented in Jobs/
}

// S3 stub forward reference
public sealed class S3FileStorageService : IFileStorageService
{
    public string ProviderName => "AWSS3";
    public Task<string> StoreAsync(Stream s, string f, string c, CancellationToken ct = default) => throw new NotImplementedException("S3 not yet implemented");
    public Task<Stream> RetrieveAsync(string k, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(string k, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(string k, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<string?> GenerateSignedUrlAsync(string k, TimeSpan e, CancellationToken ct = default) => throw new NotImplementedException();
}
