using Darah.ECM.Infrastructure.Persistence;
using Darah.ECM.Infrastructure.Security;
using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Infrastructure.Security.Abac;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.Services;
using Darah.ECM.xECM.Domain.Services;
using Hangfire;
using Hangfire.SqlServer;
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
        services.AddDbContext<EcmDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3)));

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

        // Hangfire
        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(config.GetConnectionString("DefaultConnection")));
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
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUserService>());

        // Health Checks
        var hc = services.AddHealthChecks();
        var conn = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(conn))
            hc.AddSqlServer(conn, name: "sqlserver");

        services.AddHttpContextAccessor();

        return services;
    }
}
