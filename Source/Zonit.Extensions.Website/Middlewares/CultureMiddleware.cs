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
/// <para><b>The unprefixed form of a page redirects, it does not render.</b> <c>/pricing</c>
/// answers 302 to the visitor's language rather than serving a second copy of the page. That
/// keeps the short link usable between people while giving search engines exactly one indexable
/// URL per language, and it is the documented target for <c>hreflang="x-default"</c>. The
/// redirect carries <c>Vary: Cookie, Accept-Language</c> and is marked uncacheable, because its
/// target legitimately differs per visitor. The decision is made by
/// <see cref="CultureRouteGate"/> after routing — a page redirects; an asset, a descriptor or a
/// consumer endpoint serves, because its unprefixed address is the only one it has — and only
/// for <c>GET</c>/<c>HEAD</c> requests that explicitly accept <c>text/html</c>, so an API client
/// sending <c>*/*</c> still gets the page rather than a surprise redirect.</para>
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

        // Status-code re-execution and the exception handler re-run this pipeline with the error
        // route in Request.Path — but with Request.PathBase still carrying whatever the first pass
        // moved into it. Resolving from scratch is wrong twice over, and both ways silently:
        //
        //   the culture segment is no longer in Path, so /pl/missing looks unprefixed and a Polish
        //   visitor's 404 renders in English;
        //
        //   PathBase is then read as the mount, so SitePathBase becomes "/pl" and every URL the
        //   SEO layer builds gains a SECOND language segment — canonical came out as
        //   /pl/en/not-found/404, an address that cannot exist.
        //
        // The first pass already resolved all of this correctly. Reuse it: keep the culture, keep
        // PathBase, and re-point the feature at the error route.
        if (IsReExecuting(context) && context.Features.Get<ICultureUrlFeature>() is { } resolved)
        {
            Apply(resolved.Culture, cultureManager);
            InstallFeature(context, resolved.Culture, resolved.Segment, path, path, resolved.SitePathBase);
            return _next(context);
        }

        // A file is not a page: it has no language, so it has no language segment. Static and
        // framework traffic therefore skips every piece of culture work — resolution, the cookie,
        // the feature — and a prefixed spelling is not an address at all.
        if (WebsiteRequestFilter.ShouldSkip(context))
            return _policy.IsPrefixed ? NotFoundUnlocalized(context, path) : _next(context);

        var options = _settings.CurrentValue;

        return _policy.IsPrefixed
            ? InvokePrefixed(context, path, options, cultureManager, tenant)
            : InvokeUnprefixed(context, path, options, cultureManager, tenant);
    }

    /// <summary>
    /// Answers a language-prefixed request for a file with <c>404</c>: a file has no language, so
    /// that address does not exist. Fast path only — <see cref="CultureRouteGate"/> enforces the
    /// same rule after routing for everything this extension list does not recognise.
    /// </summary>
    /// <remarks>
    /// <para><b>This used to split the segment and serve.</b> It had to: <c>&lt;base href&gt;</c> is
    /// <c>/pl/</c>, every asset URL the shell emitted was relative, so the browser asked for
    /// <c>/pl/_framework/blazor.web.js</c> and anything but a split broke the framework script on
    /// every prefixed page. The cost was that one file answered at as many addresses as the Site
    /// had languages. Nothing emits those URLs any more — the shell, <c>@Assets[…]</c> and the
    /// import map all root at the mount — so the prefixed form is not a second address for a
    /// file; it is an address that was never real.</para>
    ///
    /// <para><b>The <c>_framework</c> carve-out is not optional.</b> WebAssembly boot resources
    /// and the dev-time hot-reload script are fetched by the runtime relative to
    /// <c>document.baseURI</c>, which on a prefixed Site carries the language. No server-side
    /// rooting can reach those fetches, so <c>/pl/_framework/…</c> must split and serve or WASM
    /// mode and hot reload break under a prefix. Robots disallows the path; nothing under it is
    /// content.</para>
    ///
    /// <para><b>Why not a redirect.</b> A 301 would also keep the duplicate out of an index, and it
    /// would quietly paper over the one thing worth finding: markup still emitting a relative asset
    /// URL, which happens whenever a project is upgraded without being rebuilt — <c>@Assets</c>
    /// rooting is a compile-time binding. Under a redirect that ships and works; under 404 it fails
    /// on the first page load, in development, where it is a one-line fix.</para>
    ///
    /// <para>The body stays empty on purpose: status-code re-execution would render the styled
    /// error page for a URL only ever fetched as bytes, so it is suppressed here the same way
    /// the gate suppresses it.</para>
    /// </remarks>
    private Task NotFoundUnlocalized(HttpContext context, string path)
    {
        var match = _policy.Match(path);
        if (match is null)
            return _next(context);

        if (match.Value.Remainder.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.PathBase = context.Request.PathBase.Add("/" + match.Value.Segment);
            context.Request.Path = match.Value.Remainder;
            return _next(context);
        }

        // Same rule as the gate: keep the styled page for a caller that would read it. These paths
        // carry a static extension, so nearly all of them are subresource fetches — but "nearly"
        // is why the test is on the request rather than on the path.
        if (!CultureRouteGate.WantsHtml(context))
            CultureRouteGate.SuppressStatusPage(context);

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
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
        // The same one-address rule the prefixed flow gets from its canonical comparison. Guarded
        // to browser-shaped requests: framework endpoints are never spelled with a trailing slash
        // by anything this stack generates, and a re-executed error page must keep its status.
        var normalized = NormalizeTrailingSlash(path);
        if (!ReferenceEquals(normalized, path)
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && !path.StartsWith("/_", StringComparison.Ordinal)
            && context.Features.Get<IStatusCodeReExecuteFeature>() is null
            && context.Features.Get<IExceptionHandlerPathFeature>() is null)
        {
            return RedirectPermanent(context, normalized);
        }

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

        // Trailing slashes fold into the same canonical comparison as the culture spelling.
        // Without this, /pl/signals/ rendered next to /pl/signals — and worse than rendering, each
        // spelling emitted ITSELF as canonical, so the two were not even competing for one entry
        // in the index; they were both claiming to be it.
        var remainder = NormalizeTrailingSlash(found.Remainder);
        var routePath = _routes.ToRoute(found.Culture, remainder);
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
    /// A prefixed Site reached through an unprefixed path. Culture is resolved and published, and
    /// the request proceeds to routing — whether it then <em>redirects</em> into the visitor's
    /// language is <see cref="CultureRouteGate"/>'s call, because only the router knows whether
    /// the path is a page.
    /// </summary>
    /// <remarks>
    /// The redirect used to be decided here, before routing, from request shape alone — method,
    /// <c>Accept</c>, a <c>/_</c> prefix. That heuristic sent every browser-shaped GET into the
    /// language, including consumer endpoints mapped via <c>MapEndpoints</c>, whose prefixed
    /// address then correctly answered 404: a download link clicked in a browser bounced to a
    /// dead URL. The gate decides from the matched endpoint instead, so a page redirects and
    /// everything else serves at the one address it has.
    /// </remarks>
    private Task InvokeWithoutPrefix(
        HttpContext context, string path, CultureOption options, ICultureManager cultureManager, ITenantProvider tenant)
    {
        var culture = ResolveFromRequest(context, options, tenant);

        Apply(culture, cultureManager);
        PersistCookieIfChanged(context, culture);
        AddVary(context);

        InstallFeature(context, culture, segment: string.Empty, routePath: path, localizedPath: path);
        return _next(context);
    }

    /// <summary>
    /// Whether the pipeline is replaying this request with an error route
    /// (<c>UseStatusCodePagesWithReExecute</c> / <c>UseExceptionHandler</c>), both of which are
    /// registered upstream of this middleware and therefore run it a second time.
    /// </summary>
    internal static bool IsReExecuting(HttpContext context)
        => context.Features.Get<IStatusCodeReExecuteFeature>() is not null
        || context.Features.Get<IExceptionHandlerPathFeature>() is not null;

    /// <summary>
    /// <c>/signals/</c> and <c>/signals///</c> become <c>/signals</c>; the root stays <c>/</c>.
    /// Returns the same instance when nothing changes, so callers can test with
    /// <see cref="object.ReferenceEquals(object?, object?)"/> instead of comparing content.
    /// Internal because <see cref="CultureRouteGate"/> normalizes its redirect target with the
    /// same rule — two spellings of "canonical" would fight each other with redirects.
    /// </summary>
    internal static string NormalizeTrailingSlash(string path)
    {
        if (path.Length <= 1 || path[^1] != '/')
            return path;

        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
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
