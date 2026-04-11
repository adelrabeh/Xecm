using Darah.ECM.Application.Common.Behaviors;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.API.Filters;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.FileStorage.Local;
using Darah.ECM.Infrastructure.Logging;
using Darah.ECM.Infrastructure.Messaging;
using Darah.ECM.Infrastructure.Persistence;
using Darah.ECM.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Darah.ECM.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEcmPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EcmDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection"),
            sql => { sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null); sql.CommandTimeout(60); }));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    public static IServiceCollection AddEcmServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IEventBus, InProcessEventBus>();
        return services;
    }

    public static IServiceCollection AddEcmCqrs(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CreateDocumentCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssemblyContaining<CreateDocumentCommandValidator>();
        return services;
    }

    public static IServiceCollection AddEcmAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");
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
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddEcmSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "DARAH ECM API", Version = "v1", Description = "Enterprise Content Management — دارة الملك عبدالعزيز" });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.ApiKey, Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement {{ new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }});
        });
        return services;
    }
}
