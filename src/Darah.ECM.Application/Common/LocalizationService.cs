using System.Text.Json;

namespace Darah.ECM.Application.Common;

/// <summary>
/// Enterprise-grade localization service.
/// Reads from JSON resource files, supports Accept-Language header,
/// stores user language preference, and handles placeholder substitution.
/// </summary>
public interface ILocalizationService
{
    string Get(string key, params object[] args);
    string Lang { get; }
}

public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, string> _messages;
    public string Lang { get; }

    private static readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private static readonly Lock _lock = new();

    public LocalizationService(string lang, string resourcePath)
    {
        Lang = lang.StartsWith("ar") ? "ar" : "en";
        _messages = LoadMessages(Lang, resourcePath);
    }

    public string Get(string key, params object[] args)
    {
        if (!_messages.TryGetValue(key, out var msg))
            return key;   // Return key as fallback (never crash)

        return args.Length == 0 ? msg : string.Format(msg, args);
    }

    private static Dictionary<string, string> LoadMessages(string lang, string basePath)
    {
        if (_cache.TryGetValue(lang, out var cached)) return cached;

        lock (_lock)
        {
            if (_cache.TryGetValue(lang, out cached)) return cached;

            var path = Path.Combine(basePath, $"Resources/Messages.{lang}.json");
            var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(path))
            {
                var json  = File.ReadAllText(path);
                var doc   = JsonDocument.Parse(json);
                Flatten(doc.RootElement, "", flat);
            }

            _cache[lang] = flat;
            return flat;
        }
    }

    private static void Flatten(JsonElement el, string prefix, Dictionary<string, string> result)
    {
        foreach (var prop in el.EnumerateObject())
        {
            var key = prefix.Length > 0 ? $"{prefix}.{prop.Name}" : prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Object)
                Flatten(prop.Value, key, result);
            else
                result[key] = prop.Value.GetString() ?? "";
        }
    }
}

/// <summary>Middleware that reads Accept-Language or X-Lang header and injects ILocalizationService.</summary>
public sealed class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _resourcePath;

    public LocalizationMiddleware(RequestDelegate next, string resourcePath)
    {
        _next = next;
        _resourcePath = resourcePath;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var lang = ctx.Request.Headers["Accept-Language"].FirstOrDefault()
                ?? ctx.Request.Headers["X-Lang"].FirstOrDefault()
                ?? ctx.User.FindFirst("lang")?.Value
                ?? "ar";

        var svc = new LocalizationService(lang, _resourcePath);
        ctx.Items["LocalizationService"] = svc;
        ctx.Features.Set<ILocalizationService>(svc);

        // Set thread culture for date/number formatting
        var culture = lang.StartsWith("en") ? "en-US" : "ar-SA";
        System.Threading.Thread.CurrentThread.CurrentCulture =
            System.Globalization.CultureInfo.GetCultureInfo(culture);

        await _next(ctx);
    }
}
