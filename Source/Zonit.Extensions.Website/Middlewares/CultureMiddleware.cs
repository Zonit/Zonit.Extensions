using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Globalization;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Cultures.Services;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Resolves the active culture for the current request and pushes it into both the
/// thread-local <see cref="CultureInfo.CurrentCulture"/> and the scoped
/// <see cref="ICultureManager"/>. Culture detection order is URL → cookie →
/// <c>Accept-Language</c> header → configured default.
/// </summary>
/// <remarks>
/// <para><b>Cookie write discipline.</b> The previous implementation appended the
/// <c>Culture</c> cookie on <em>every</em> request — including hundreds of static-asset
/// fetches that follow each page navigation. That bloated response headers and cost
/// CPU for cookie serialisation. We now write the cookie only when the resolved value
/// genuinely differs from what the browser already sent.</para>
///
/// <para><b>Static-asset bypass.</b> Like the other Zonit middlewares this one opts out
/// of work for static / framework / library traffic via <see cref="WebsiteRequestFilter"/>.</para>
///
/// <para><b>Resolution order:</b></para>
/// <list type="number">
///   <item>URL prefix (<c>/pl-pl/...</c> / <c>/pl/...</c>) handled by
///         <see cref="DetectCultureService"/>. The path is rewritten so downstream
///         routing sees <c>/...</c>.</item>
///   <item>Existing <c>Culture</c> cookie when valid and supported.</item>
///   <item><c>Accept-Language</c> first entry, when supported.</item>
///   <item>Configured <see cref="CultureOption.DefaultCulture"/>.</item>
/// </list>
/// </remarks>
internal sealed class CultureMiddleware(RequestDelegate next, IOptionsMonitor<CultureOption> settings)
{
    private const string CookieName = "Culture";

    private readonly RequestDelegate _next = next;

    // IOptionsMonitor, not IOptions: middleware is a singleton, so IOptions.Value would freeze
    // the allow-list at startup and a language added to appsettings.json would need a process
    // restart to take effect. CurrentValue is read ONCE per request into a local and threaded
    // through the resolution — re-reading it per step could observe a reload mid-request and
    // validate the cookie against one list and the default against another.
    private readonly IOptionsMonitor<CultureOption> _settings = settings;

    public Task InvokeAsync(
        HttpContext httpContext,
        DetectCultureService detectCultureService,
        ICultureManager cultureManager)
    {
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
            return _next(httpContext);

        var path = httpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
            return _next(httpContext);

        var resolved = ResolveCulture(httpContext, path, detectCultureService, _settings.CurrentValue);

        ApplyCulture(resolved, cultureManager);
        PersistCookieIfChanged(httpContext, resolved);

        return _next(httpContext);
    }

    /// <summary>
    /// Picks the canonical (lower-cased BCP-47) culture for the current request.
    /// Always returns a value present in <see cref="CultureOption.SupportedCultures"/>
    /// (or the configured default). May rewrite <see cref="HttpRequest.Path"/> when the
    /// culture is encoded as a URL prefix.
    /// </summary>
    private static string ResolveCulture(
        HttpContext httpContext, string path, DetectCultureService detector, CultureOption settings)
    {
        // 1. URL prefix takes priority — typing /pl/home is an explicit choice that
        //    must trump any stored cookie. DetectCultureService also handles the
        //    primary-subtag fallback (pl → pl-pl) and returns a canonical tag.
        var match = detector.GetUrl(path);
        if (match is not null)
        {
            httpContext.Request.Path = "/" + match.Url;
            return match.Culture; // already canonical + supported
        }

        // 2. Cookie. Trust it when supported; ignore otherwise (could be stale or
        //    forged — we never want to render a culture we cannot translate to).
        var cookieValue = httpContext.Request.Cookies[CookieName];
        if (!string.IsNullOrWhiteSpace(cookieValue) &&
            TryNormalizeAndValidate(cookieValue, settings, out var fromCookie))
        {
            return fromCookie;
        }

        // 3. Accept-Language. We pick only the first entry; full quality-factor
        //    negotiation belongs in a dedicated middleware if a project needs it.
        var preferred = httpContext.Request.GetTypedHeaders()
            .AcceptLanguage?.FirstOrDefault()?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(preferred) &&
            TryNormalizeAndValidate(preferred, settings, out var fromHeader))
        {
            return fromHeader;
        }

        // 4. Default. Configuration value should already be supported but we re-check
        //    to be defensive (typo in appsettings → render in en-us instead of crashing).
        return TryNormalizeAndValidate(settings.DefaultCulture, settings, out var defaulted)
            ? defaulted
            : "en-us";
    }

    /// <summary>
    /// Pushes <paramref name="culture"/> into <see cref="CultureInfo.CurrentCulture"/>,
    /// <see cref="CultureInfo.CurrentUICulture"/> and the scoped <see cref="ICultureManager"/>.
    /// </summary>
    private static void ApplyCulture(string culture, ICultureManager cultureManager)
    {
        // GetCultureInfo is cached by the runtime — repeated calls are cheap.
        var info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
        cultureManager.SetCulture(culture);
    }

    /// <summary>
    /// Writes the <c>Culture</c> cookie iff the browser does not already carry the
    /// resolved value. Stops the cookie from being re-emitted on every static-asset
    /// fetch and keeps response headers small.
    /// </summary>
    private static void PersistCookieIfChanged(HttpContext httpContext, string resolved)
    {
        var existing = httpContext.Request.Cookies[CookieName];
        if (string.Equals(existing, resolved, StringComparison.OrdinalIgnoreCase))
            return;

        httpContext.Response.Cookies.Append(
            CookieName,
            resolved,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,    // i18n is a functional concern, not tracking
                SameSite = SameSiteMode.Lax,
                HttpOnly = false,      // language switcher in JS may need to read it
            });
    }

    /// <summary>
    /// Attempts to canonicalise <paramref name="raw"/> via <see cref="CultureInfo.GetCultureInfo(string)"/>
    /// and verifies the canonical tag is in the configured supported list.
    /// </summary>
    private static bool TryNormalizeAndValidate(string? raw, CultureOption settings, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        CultureInfo info;
        try
        {
            info = CultureInfo.GetCultureInfo(raw);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        var candidate = info.Name.ToLowerInvariant();
        var supported = settings.SupportedCultures;
        for (int i = 0; i < supported.Length; i++)
        {
            if (string.Equals(supported[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                canonical = candidate;
                return true;
            }
        }

        return false;
    }
}