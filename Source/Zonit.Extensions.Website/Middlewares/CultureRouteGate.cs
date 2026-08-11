using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// The rule that makes the culture prefix mean something: a language segment is valid for
/// <em>pages</em>, and for nothing else — and only pages are redirected <em>into</em> a language
/// when reached without one. Runs after routing, so "page" is decided by the router, not by this
/// middleware guessing from the path.
/// </summary>
/// <remarks>
/// <para><b>Why the decision sits after routing.</b> The set of things that may carry a language
/// is the set of page routes, and that set already exists — it is the endpoint table. Deciding
/// from the path instead means maintaining a parallel description of it (an extension list, a
/// prefix list, a route-shape heuristic) that starts rotting the day it is written. An extension
/// list in particular inverts the burden: there are thousands of file formats and more every
/// year, and every one it does not name becomes a page-shaped request that splits the culture
/// segment and serves a duplicate. After routing there is nothing to guess: the endpoint either
/// carries <see cref="ComponentTypeMetadata"/> — the marker every Razor Components page endpoint
/// has — or it is not a page.</para>
///
/// <para><b>What answers under a prefix, and why.</b></para>
/// <list type="bullet">
///   <item><b>Pages</b> — the point of the prefix. <c>/pl/pricing</c> is Polish pricing.</item>
///   <item><b><c>/_blazor</c></b> — the circuit plumbing (negotiate, WebSocket, initializers,
///         disconnect). The client resolves it against <c>document.baseURI</c>, which on a
///         prefixed Site deliberately carries the language, so these arrive prefixed by
///         construction and there is no server-side rewrite that could stop them. Robots
///         disallows the path; nothing here is content.</item>
///   <item><b><c>/_framework/</c></b> — boot and runtime files. WebAssembly boot resources and
///         the dev-time hot-reload script are fetched relative to the base URI too. Same
///         reasoning, same robots disallow.</item>
/// </list>
///
/// <para><b>Everything else is 404.</b> A static asset, a minimal-API endpoint, a mapped
/// descriptor — under a language prefix none of them exist, because none of them have a
/// language. This is what keeps a consumer's <c>MapEndpoints</c> additions, and file formats
/// nobody thought to list, from quietly acquiring one address per language. The extension list
/// in <see cref="WebsiteRequestFilter"/> still short-circuits the obvious cases before routing
/// as a fast path; correctness does not depend on it any more.</para>
///
/// <para><b>Cost.</b> Registered only on Sites whose URL policy prefixes at all. Per request it
/// is one feature read; only requests that arrived with a culture segment pay a metadata lookup.
/// The 404 is answered before authentication runs, and with status-code re-execution suppressed —
/// re-rendering a styled error page for a stray asset URL is a page render nothing will read.</para>
/// </remarks>
internal sealed class CultureRouteGate(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        // Installed by CultureMiddleware. Absent means the request skipped culture work entirely
        // (an asset fast-pathed by extension) — nothing to police either way.
        var culture = context.Features.Get<ICultureUrlFeature>();
        if (culture is null)
            return _next(context);

        var endpoint = context.GetEndpoint();
        if (endpoint is null)
            return _next(context); // no match → the pipeline 404s it on its own

        var isPage = endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;

        // The unprefixed side of the same rule: a PAGE reached without its language answers a
        // redirect into the visitor's own; anything else serves, because the unprefixed address
        // is the only one it has. This decision also used to be made before routing, from request
        // shape — and sent browser-navigated consumer endpoints into a prefixed address that the
        // branch below correctly refuses, turning a clicked download link into a 404.
        if (culture.Segment.Length == 0)
        {
            if (isPage && WantsLanguageRedirect(context))
            {
                var target = culture.Policy.BuildPath(
                    culture.Culture,
                    culture.Routes.ToLocalized(
                        culture.Culture,
                        CultureMiddleware.NormalizeTrailingSlash(context.Request.Path.Value ?? "/")));

                if (target is not null)
                {
                    // Temporary, not permanent: the target depends on who is asking, so a cache
                    // or a browser must not pin one language onto this URL forever. Vary was
                    // already appended by CultureMiddleware for exactly this response.
                    context.Response.Headers.CacheControl = "private, no-store";
                    context.Response.Redirect(
                        context.Request.PathBase + target + context.Request.QueryString,
                        permanent: false);
                    return Task.CompletedTask;
                }
            }

            return _next(context);
        }

        if (isPage)
            return _next(context); // a page — exactly what the prefix is for

        if (IsFrameworkPath(context.Request.Path))
            return _next(context);

        // A non-page endpoint under a language prefix: not a duplicate to serve, an address that
        // does not exist. Clearing the endpoint keeps anything downstream from running it.
        context.SetEndpoint(null);

        // Empty body on purpose: these are asset- and API-shaped URLs, and re-executing the
        // styled error page would render HTML for a consumer that wanted bytes.
        var statusPages = context.Features.Get<IStatusCodePagesFeature>();
        if (statusPages is not null)
            statusPages.Enabled = false;

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether this request is a browser navigation that should be answered with the language
    /// redirect rather than an unprefixed render.
    /// </summary>
    /// <remarks>
    /// <para>Requiring an explicit <c>text/html</c> in <c>Accept</c> admits every browser
    /// navigation and every major crawler while excluding API clients, health probes and
    /// anything sending <c>*/*</c> — those still get the page, with a canonical tag pointing at
    /// the prefixed form.</para>
    ///
    /// <para>The error-page guard is not theoretical: <c>UseStatusCodePagesWithReExecute</c> and
    /// <c>UseExceptionHandler</c> re-execute through this middleware with the error page — a
    /// page! — as the endpoint. Redirecting then would discard the status code and turn every
    /// 404 into a 302.</para>
    /// </remarks>
    private static bool WantsLanguageRedirect(HttpContext context)
    {
        var request = context.Request;

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
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

    /// <summary>
    /// Framework plumbing that legitimately resolves against the (prefixed) base URI.
    /// </summary>
    internal static bool IsFrameworkPath(PathString path)
    {
        var value = path.Value;
        if (value is null)
            return false;

        return value.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase);
    }
}
