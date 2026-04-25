using Darah.ECM.Application.Common;

namespace Darah.ECM.API.Middleware;

public static class LocalizationExtensions
{
    public static IApplicationBuilder UseEcmLocalization(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        return app.UseMiddleware<LocalizationMiddleware>(env.ContentRootPath);
    }

    /// <summary>Get localization from current HttpContext.</summary>
    public static ILocalizationService GetLocalizer(this HttpContext ctx)
        => ctx.Features.Get<ILocalizationService>()
           ?? new LocalizationService("ar", Directory.GetCurrentDirectory());
}
