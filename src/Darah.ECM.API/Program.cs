using Darah.ECM.API.Extensions;
using Darah.ECM.API.Middleware;
using Hangfire;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

    // ─── CORS — must be registered before anything else ──────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Authorization", "Content-Disposition"));
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "DARAH ECM API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT: Bearer {token}",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
    });

    builder.Services.AddEcmServices(builder.Configuration);

    var app = builder.Build();

    // ─── CORS must be first in pipeline ──────────────────────────────────────
    app.UseCors("AllowAll");

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DARAH ECM v1");
        c.RoutePrefix = "swagger";
    });

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    try { app.UseHangfireDashboard("/hangfire"); }
    catch (Exception ex) { Log.Warning(ex, "Hangfire dashboard failed to start"); }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapGet("/health/live", () => Results.Ok(new { status = "alive", time = DateTime.UtcNow }));
    app.MapGet("/", () => Results.Redirect("/swagger"));

    await Darah.ECM.API.Infrastructure.DatabaseSeeder.SeedAsync(app.Services);

    Log.Information("DARAH ECM API starting on port {Port}",
        Environment.GetEnvironmentVariable("PORT") ?? "8080");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
