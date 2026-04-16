using Darah.ECM.API.Extensions;
using Darah.ECM.API.Middleware;
using Hangfire;
using Serilog;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog from config
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // Services
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

    builder.Services.AddCors(o => o.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    builder.Services.AddEcmServices(builder.Configuration);

    var app = builder.Build();

    // Always enable Swagger (useful for testing)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DARAH ECM v1");
        c.RoutePrefix = "swagger";
    });

    app.UseCors("AllowAll");
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

    // Seed database with initial admin user
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
