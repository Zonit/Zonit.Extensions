using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Resolves the active culture for the current request, enforces one canonical URL per page,
/// and publishes the result as <see cref="ICultureUrlFeature"/>.
/// </summary>
/// <remarks>
/// <para><b>Two behaviours, chosen per Site.</b> Under
/// <see cref="CultureUrlStrategy.None"/> — panels, internal tools, single-language sites — the
/// culture comes from the cookie, then <c>Accept-Language</c>, then the configured default, and
/// the URL is left alone. Under <see cref="CultureUrlStrategy.Prefix"/> the language leads the
/// path and the full public-web contract applies.</para>
///
/// <para><b>PathBase, not a path rewrite.</b> A matched prefix is <em>moved</em> into
/// <c>Request.PathBase</c> rather than deleted from <c>Request.Path</c>. Three things depend on
/// that. The emitted <c>&lt;base href&gt;</c> becomes <c>/pl/</c>, so every relative link the
/// framework renders through <c>UrlPathRendering.ToHref()</c> keeps the language without a
/// single route template mentioning it. <c>PathBase + Path</c> still reconstructs the URL the
/// browser asked for, so <c>NavigationManager</c> does not end up with a <c>Uri</c> outside its
/// own <c>BaseUri</c> — which is what a plain <c>Path</c> rewrite produces, and it throws. And
/// routing still sees the unprefixed path, so <c>@page</c> templates stay clean.</para>
///
/// <para><b>One canonical address per page.</b> Three different spellings can reach the same
/// content — a non-canonical culture form (<c>/pl-pl/</c> where <c>/pl/</c> is canonical), a
/// missing trailing slash on the language root (<c>/pl</c>), and an untranslated route in a
/// language that translates it (<c>/pl/news/x</c> where Polish uses <c>/aktualnosci/x</c>). All
/// three are folded into a single comparison: build what the canonical URL <em>would</em> be and
/// redirect permanently if the request does not already match it. Anything else leaves the page
/// living at several indexable addresses.</para>
///
/// <para><b>The unprefixed form redirects, it does not render.</b> <c>/pricing</c> answers 302
/// to the visitor's language rather than serving a second copy of the page. That keeps the
/// short link usable between people while giving search engines exactly one indexable URL per
/// language, and it is the documented target for <c>hreflang="x-default"</c>. The redirect
/// carries <c>Vary: Cookie, Accept-Language</c> and is marked uncacheable, because its target
/// legitimately differs per visitor. Only <c>GET</c>/<c>HEAD</c> requests that explicitly accept
/// <c>text/html</c> are redirected — that bounds the behaviour to browsers and crawlers without
/// a hand-maintained list of API paths to exclude, and a client sending <c>*/*</c> still gets
/// the page (with a canonical tag pointing at the prefixed form) rather than a surprise
/// redirect.</para>
///
/// <para><b>Cookie write discipline.</b> The cookie is written only when the resolved value
/// differs from what the browser sent. Under <see cref="CultureUrlStrategy.Prefix"/> that means
/// steady-state browsing emits no <c>Set-Cookie</c> at all, which is what keeps the HTML
/// cacheable by a shared cache — a page whose language is already in its URL does not depend on
/// the cookie, so it needs no <c>Vary</c> either. Unprefixed Sites do depend on it, and are
/// marked accordingly.</para>
/// </remarks>
internal sealed class CultureMiddleware(
    RequestDelegate next,
    IOptionsMonitor<CultureOption> settings,
    CultureUrlPolicy policy,
    LocalizedRouteTable routes,
    SiteOptions site)
{
    private const string CookieName = "lang";

    private readonly RequestDelegate _next = next;

    // IOptionsMonitor, not IOptions: middleware is a singleton, so IOptions.Value would freeze
    // the allow-list at startup and a language added to appsettings.json would need a restart.
    // CurrentValue is read ONCE per request into a local and threaded through the resolution —
    // re-reading per step could observe a reload mid-request and validate the cookie against
    // one list and the default against another.
    private readonly IOptionsMonitor<CultureOption> _settings = settings;
    private readonly CultureUrlPolicy _policy = policy;
    private readonly LocalizedRouteTable _routes = routes;
    private readonly SiteOptions _site = site;

    public Task InvokeAsync(HttpContext context, ICultureManager cultureManager, ITenantProvider tenant)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
            path = "/";

        // Static and framework traffic skips the *culture work* — resolution, the cookie, the
        // feature, redirects — but NOT the path split. Those are two different things, and
        // conflating them broke every asset on a prefixed Site.
        //
        // <base href> is "/pl/", so a relative src="_framework/blazor.web.js" makes the browser
        // request "/pl/_framework/blazor.web.js". Returning early here left the "/pl" in
        // Request.Path, the static-asset endpoint had no route for it, and the answer was 404 —
        // silently, on the framework script itself, on every prefixed page. A mount prefix does
        // not have this problem because UsePathBase is real middleware that runs unconditionally;
        // the culture segment is moved by this method, so this method has to run too.
        if (WebsiteRequestFilter.ShouldSkip(context))
            return _policy.IsPrefixed ? SplitPrefixOnly(context, path) : _next(context);

        var options = _settings.CurrentValue;

        return _policy.IsPrefixed
            ? InvokePrefixed(context, path, options, cultureManager, tenant)
            : InvokeUnprefixed(context, path, options, cultureManager, tenant);
    }

    /// <summary>
    /// Moves a culture segment out of the path and does nothing else — the asset path for a
    /// prefixed Site.
    /// </summary>
    /// <remarks>
    /// No canonical redirect: <c>/pl-pl/app.css</c> is served, not 301'd. An asset URL is not an
    /// indexable address, nobody links to it from outside, and bouncing it costs a round trip on
    /// the critical path for no benefit. No cookie, no feature, no culture applied either —
    /// nothing downstream of a static file reads any of it.
    /// </remarks>
    private Task SplitPrefixOnly(HttpContext context, string path)
    {
        var match = _policy.Match(path);
        if (match is null)
            return _next(context);

        context.Request.PathBase = context.Request.PathBase.Add("/" + match.Value.Segment);
        context.Request.Path = match.Value.Remainder;

        return _next(context);
    }

    /// <summary>
    /// <see cref="CultureUrlStrategy.None"/>: resolve, apply, and get out of the way. The URL
    /// carries no language, so the response genuinely varies by cookie and
    /// <c>Accept-Language</c> and has to say so — without it any shared cache in front of the
    /// app will hand one visitor's language to the next.
    /// </summary>
    private Task InvokeUnprefixed(
        HttpContext context, string path, CultureOption options, ICultureManager cultureManager, ITenantProvider tenant)
    {
        var culture = ResolveFromRequest(context, options, tenant);

        Apply(culture, cultureManager);
        PersistCookieIfChanged(context, culture);
        AddVary(context);

        InstallFeature(context, culture, segment: string.Empty, routePath: path, localizedPath: path);
        return _next(context);
    }

    private Task InvokePrefixed(
        HttpContext context, string path, CultureOption options, ICultureManager cultureManager, ITenantProvider tenant)
    {
        var match = _policy.Match(path);

        if (match is null)
            return InvokeWithoutPrefix(context, path, options, cultureManager, tenant);

        var found = match.Value;
        var routePath = _routes.ToRoute(found.Culture, found.Remainder);
        var localizedPath = _routes.ToLocalized(found.Culture, routePath);

        // One comparison covers the culture spelling, the language-root trailing slash and an
        // untranslated route spelling. BuildPath cannot return null here: Match only ever yields
        // a culture that is in the allow-list, which is the same set SegmentFor is built from.
        var canonicalPath = _policy.BuildPath(found.Culture, localizedPath)!;
        if (!string.Equals(canonicalPath, path, StringComparison.Ordinal))
            return RedirectPermanent(context, canonicalPath);

        // Move the segment from Path into PathBase. Order matters: read the site's own path base
        // BEFORE appending, because that is what mount resolution needs and it is unrecoverable
        // afterwards.
        var sitePathBase = context.Request.PathBase.Value ?? string.Empty;
        context.Request.PathBase = context.Request.PathBase.Add("/" + found.Segment);
        context.Request.Path = routePath;

        Apply(found.Culture, cultureManager);
        PersistCookieIfChanged(context, found.Culture);

        InstallFeature(context, found.Culture, found.Segment, routePath, localizedPath, sitePathBase);
        return _next(context);
    }

    /// <summary>
    /// A prefixed Site reached through an unprefixed path. Normally a redirect into the
    /// visitor's language; falls through to rendering when redirects are disabled, when the
    /// request is not a browser navigation, or when the pipeline is re-executing an error page.
    /// </summary>
    private Task InvokeWithoutPrefix(
        HttpContext context, string path, CultureOption options, ICultureManager cultureManager, ITenantProvider tenant)
    {
        var culture = ResolveFromRequest(context, options, tenant);

        if (ShouldRedirect(context, path))
        {
            var target = _policy.BuildPath(culture, _routes.ToLocalized(culture, path));
            if (target is not null)
            {
                // Temporary, not permanent: the target depends on who is asking, so a cache or a
                // browser must not pin one language onto this URL forever.
                AddVary(context);
                context.Response.Headers.CacheControl = "private, no-store";
                context.Response.Redirect(context.Request.PathBase + target + context.Request.QueryString, permanent: false);
                return Task.CompletedTask;
            }
        }

        Apply(culture, cultureManager);
        PersistCookieIfChanged(context, culture);
        AddVary(context);

        InstallFeature(context, culture, segment: string.Empty, routePath: path, localizedPath: path);
        return _next(context);
    }

    /// <summary>
    /// Whether an unprefixed request should be answered with a redirect rather than rendered.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately conservative. Requiring an explicit <c>text/html</c> in <c>Accept</c>
    /// admits every browser navigation and every major crawler while excluding API clients,
    /// health probes and anything sending <c>*/*</c> — without a list of endpoint paths that
    /// would silently rot as areas add their own.</para>
    ///
    /// <para>The error-page guard is not theoretical: <c>UseStatusCodePagesWithReExecute</c> and
    /// <c>UseExceptionHandler</c> are registered upstream of this middleware, so a re-executed
    /// error path runs through here again. Redirecting at that point discards the status code
    /// and turns every 404 into a 302.</para>
    /// </remarks>
    private static bool ShouldRedirect(HttpContext context, string path)
    {
        var request = context.Request;

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        // Framework endpoints (/_blazor, /_framework, /_content) are never page navigations.
        // WebsiteRequestFilter already skipped most of them; "/_blazor" has no extension and no
        // trailing slash in its negotiate form, so it needs this.
        if (path.StartsWith("/_", StringComparison.Ordinal))
            return false;

        if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null ||
            context.Features.Get<IExceptionHandlerPathFeature>() is not null)
            return false;

        foreach (var accept in request.Headers.Accept)
        {
            if (accept is not null && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Task RedirectPermanent(HttpContext context, string canonicalPath)
    {
        context.Response.Redirect(
            context.Request.PathBase + canonicalPath + context.Request.QueryString,
            permanent: true);
        return Task.CompletedTask;
    }

    private void InstallFeature(
        HttpContext context,
        string culture,
        string segment,
        string routePath,
        string localizedPath,
        string? sitePathBase = null)
    {
        context.Features.Set<ICultureUrlFeature>(new CultureUrlFeature(() => ResolveOrigin(context))
        {
            Culture = culture,
            Segment = segment,
            SitePathBase = sitePathBase ?? context.Request.PathBase.Value ?? string.Empty,
            RoutePath = routePath,
            LocalizedPath = localizedPath,
            Policy = _policy,
            Routes = _routes,
            Indexable = SiteOptions.ResolveIndexable(_site.Settings!.Current, _site.Permission),
        });
    }

    /// <summary>
    /// Absolute origin for canonical / <c>hreflang</c> / Open Graph URLs.
    /// </summary>
    /// <remarks>
    /// The request host is trusted. It is validated upstream by host filtering and, in the
    /// deployments this framework targets, by TLS itself — a request that reached the app over
    /// HTTPS reached it on a certificate matching the host it claims. The one override is the
    /// tenant's <c>CanonicalUrl</c>, which is where multi-domain belongs and which
    /// <c>SeoDocumentBuilder</c> applies at render time, after tenant hydration has run.
    /// </remarks>
    private static string ResolveOrigin(HttpContext context)
        => $"{context.Request.Scheme}://{context.Request.Host.Value}";

    /// <summary>Cookie → <c>Accept-Language</c> → configured default, first supported wins.</summary>
    private static string ResolveFromRequest(HttpContext context, CultureOption options, ITenantProvider tenant)
    {
        // Trust the cookie when supported; ignore otherwise. Stale or forged values must never
        // select a language the app cannot actually render.
        var cookie = context.Request.Cookies[CookieName];
        if (!string.IsNullOrWhiteSpace(cookie) && TryNormalize(cookie, options, out var fromCookie))
            return fromCookie;

        // First entry only — full quality-factor negotiation belongs in a dedicated middleware
        // if a project ever needs it.
        var preferred = context.Request.GetTypedHeaders()
            .AcceptLanguage?.FirstOrDefault()?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(preferred) && TryNormalize(preferred, options, out var fromHeader))
            return fromHeader;

        // The tenant's own default language. This outranks the framework default because in a
        // multi-site host each brand legitimately speaks a different one, and the tenant is the
        // only layer that knows which brand this request arrived at. Its middleware runs ahead of
        // this one specifically so the value is here to read.
        if (TryNormalize(tenant.Settings.Site.Language, options, out var fromTenant))
            return fromTenant;

        // Re-checked defensively: a typo in appsettings should render English, not crash.
        return TryNormalize(options.DefaultCulture, options, out var defaulted) ? defaulted : "en-us";
    }

    private static void Apply(string culture, ICultureManager cultureManager)
    {
        // GetCultureInfo is cached by the runtime — repeated calls are cheap.
        var info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
        cultureManager.SetCulture(culture);
    }

    private static void PersistCookieIfChanged(HttpContext context, string resolved)
    {
        if (string.Equals(context.Request.Cookies[CookieName], resolved, StringComparison.OrdinalIgnoreCase))
            return;

        context.Response.Cookies.Append(
            CookieName,
            resolved,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,   // i18n is a functional concern, not tracking
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                HttpOnly = false,     // a client-side language switcher reads it
            });
    }

    /// <summary>
    /// Declares that the response body depends on the cookie and the negotiated language.
    /// Appended rather than assigned so an upstream <c>Vary</c> (compression negotiating on
    /// <c>Accept-Encoding</c>, for one) survives.
    /// </summary>
    private static void AddVary(HttpContext context)
        => context.Response.Headers.Append(HeaderNames.Vary, "Cookie, Accept-Language");

    private static bool TryNormalize(string? raw, CultureOption options, out string canonical)
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
        var supported = options.SupportedCultures;
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
