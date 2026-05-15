using Microsoft.AspNetCore.Http;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Shared fast-path filter used by every Zonit middleware to opt out of work for
/// requests that do not need scoped Zonit state (auth / culture / workspace /
/// catalog hydration).
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> A modern Razor Components page typically pulls 50–200
/// auxiliary HTTP requests after the initial document: <c>/_framework/blazor.web.js</c>,
/// CSS / JS bundles, MudBlazor assets in <c>/_content/MudBlazor/...</c>, fonts, images,
/// favicon. ASP.NET Core processes each as an independent request with its own DI scope.
/// Without filtering, the four Zonit middlewares ran for every one of those — populating
/// the auth repository, hitting the workspace / catalog providers (database!) and writing
/// the Culture cookie hundreds of times per page navigation.</para>
///
/// <para><b>What is skipped.</b> Two-stage check, ordered cheapest first:</para>
/// <list type="number">
///   <item>Path prefix: <c>/_framework/</c>, <c>/_content/</c>, <c>/lib/</c> — Blazor
///         framework files and library assets shipped via static-files / static-assets
///         endpoints. Never represent an "app request" on which to hydrate user state.</item>
///   <item>Static-file extension lookup. The set is intentionally conservative: only
///         the extensions we ship or know to be served by <c>UseStaticFiles</c> /
///         <c>MapStaticAssets</c>. URLs without an extension always fall through to the
///         page pipeline (could be a user-friendly route).</item>
/// </list>
///
/// <para><b>What is NOT skipped.</b></para>
/// <list type="bullet">
///   <item>Razor pages and Blazor SignalR (<c>/_blazor/...</c>) — first connection HTTP
///         upgrade still wants the auth + culture context populated.</item>
///   <item>API endpoints (no extension match).</item>
///   <item>Form posts and antiforgery callbacks.</item>
/// </list>
///
/// <para><b>Performance.</b> Two ordinal <c>StartsWith</c> tests + at most one
/// <see cref="System.MemoryExtensions.Equals(System.ReadOnlySpan{char},System.ReadOnlySpan{char},System.StringComparison)"/>
/// per known extension. No allocations on the hot path.</para>
/// </remarks>
internal static class WebsiteRequestFilter
{
    private static readonly string[] StaticPathPrefixes =
    {
        "/_framework/",
        "/_content/",
        "/lib/",
    };

    // Conservative whitelist: a file with one of these extensions is, in practice, never
    // a Razor page route in this stack. Keep ordered roughly by frequency to short-circuit.
    private static readonly string[] StaticExtensions =
    {
        ".css", ".js", ".map",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".mp4", ".webm", ".ogg", ".mp3", ".wav",
        ".pdf", ".zip", ".wasm",
        ".txt", ".xml", ".json",
    };

    /// <summary>
    /// Returns <see langword="true"/> if the current request is a static asset / framework
    /// file that should bypass Zonit middleware. Cheap O(1)-ish check, safe to call
    /// from every middleware in the pipeline.
    /// </summary>
    public static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
            return false;

        // 1. Path prefix check — catches the bulk of framework/library traffic.
        for (int i = 0; i < StaticPathPrefixes.Length; i++)
        {
            if (path.StartsWith(StaticPathPrefixes[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 2. Extension check. We only look at the very last segment to avoid mis-firing
        //    on extension-looking substrings inside the path (e.g. "/api/v1.2/foo").
        var lastSlash = path.LastIndexOf('/');
        var lastSegment = lastSlash >= 0 ? path.AsSpan(lastSlash + 1) : path.AsSpan();
        var lastDot = lastSegment.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == lastSegment.Length - 1)
            return false;

        var ext = lastSegment[lastDot..];
        for (int i = 0; i < StaticExtensions.Length; i++)
        {
            if (ext.Equals(StaticExtensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
