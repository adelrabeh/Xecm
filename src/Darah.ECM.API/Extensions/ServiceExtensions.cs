using Darah.ECM.Infrastructure.Persistence;
using Darah.ECM.Infrastructure.Security;
using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Infrastructure.Security.Abac;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.Services;
using Darah.ECM.xECM.Domain.Services;
using Hangfire;
using Hangfire.InMemory;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Darah.ECM.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddEcmServices(
        this IServiceCollection services, IConfiguration config)
    {
        // Database
        var connStr = config.GetConnectionString("DefaultConnection");
        services.AddDbContext<EcmDbContext>(opt =>
        {
            if (!string.IsNullOrEmpty(connStr))
                opt.UseNpgsql(connStr, sql => sql.EnableRetryOnFailure(3));
            else
                opt.UseInMemoryDatabase("DarahECM_Dev");
        });

        // JWT
        var jwt = config.GetSection("Jwt");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt => opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwt["Issuer"],
                ValidAudience            = jwt["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt["SecretKey"] ?? "default-dev-key-change-me-32chars!")),
                ClockSkew = TimeSpan.Zero
            });
        services.AddAuthorization();

        // Redis Cache (falls back to memory if Redis not available)
        var redis = config["Redis:ConnectionString"];
        if (!string.IsNullOrEmpty(redis))
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
        else
            services.AddDistributedMemoryCache();

        // Hangfire - always use InMemory to avoid connection string format issues
        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
        services.AddHangfireServer();

        // MediatR — scans Application + xECM assemblies
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<
                Darah.ECM.Application.Common.Models.ApiResponse<object>>();
        });

        // Domain Services
        services.AddScoped<DocumentLifecycleService>();
        services.AddScoped<WorkspaceLifecycleService>();

        // Security
        services.AddScoped<IPolicyEngine, PolicyEngine>();
        // Register CurrentUserService - implements ICurrentUser and ICurrentUserAccessor
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, Darah.ECM.Infrastructure.Security.CurrentUserService>();
        services.AddScoped<ICurrentUserAccessor, Darah.ECM.Infrastructure.Security.CurrentUserService>();

        // Repositories
        services.AddScoped<Darah.ECM.Domain.Interfaces.Repositories.IUserRepository,
            Darah.ECM.Infrastructure.Persistence.UserRepository>();
        services.AddScoped<Darah.ECM.Domain.Interfaces.Repositories.IDocumentRepository,
            Darah.ECM.Infrastructure.Persistence.DocumentRepository>();
        services.AddScoped<Darah.ECM.Domain.Interfaces.Repositories.IDocumentVersionRepository,
            Darah.ECM.Infrastructure.Persistence.DocumentVersionRepository>();
        services.AddScoped<Darah.ECM.Domain.Interfaces.Repositories.IWorkflowRepository,
            Darah.ECM.Infrastructure.Persistence.WorkflowRepository>();

        // Domain Services (Infrastructure implementations)
        services.AddScoped<Darah.ECM.Domain.Interfaces.Services.IAuditService,
            Darah.ECM.Infrastructure.Security.AuditService>();
        services.AddScoped<Darah.ECM.Application.Notifications.INotificationService,
            Darah.ECM.Infrastructure.Notifications.NotificationService>();

        // Health Checks
        var hc = services.AddHealthChecks();
        var conn = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(conn))
            hc.AddNpgSql(conn!, name: "postgresql");

        services.AddHttpContextAccessor();

        return services;
    }
}
