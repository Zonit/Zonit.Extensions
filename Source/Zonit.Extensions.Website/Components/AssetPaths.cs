using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website;

/// <summary>
/// Asset lookups that come back rooted at the Site's mount, with no culture segment.
/// </summary>
/// <remarks>
/// <para>Returned by the <c>Assets</c> property on <see cref="ExtensionsBase"/>, so
/// <c>@Assets["_content/acme/logo.png"]</c> in a page or component behaves like every other asset
/// the framework emits. Only the indexer exists because that is the only thing <c>@Assets[…]</c>
/// ever uses.</para>
///
/// <para><b>Why this type exists at all.</b> Blazor's <see cref="ResourceAssetCollection"/> returns
/// a base-relative path by contract — <c>_content/acme/logo.png</c>, no leading slash. Under
/// <c>&lt;base href="/pl/"&gt;</c> the browser resolves that to <c>/pl/_content/acme/logo.png</c>,
/// so one file ends up with one URL per language. The culture never enters the string the
/// collection returns; it is added when the browser resolves it, which is why the fix has to
/// change the string.</para>
/// </remarks>
public readonly struct AssetPaths(ResourceAssetCollection inner, string mount)
{
    /// <summary>
    /// The fingerprinted URL for <paramref name="key"/>, rooted at the Site's mount.
    /// </summary>
    /// <param name="key">
    /// The same key <see cref="ResourceAssetCollection"/> takes — <c>_content/{Assembly}/{file}</c>
    /// for a Razor class library, or a plain <c>wwwroot</c>-relative path.
    /// </param>
    public string this[string key] => AssetBaseResolver.Root(inner[key], mount);
}

/// <summary>
/// One definition of "the path assets are rooted at", shared by the document shell and by
/// components.
/// </summary>
/// <remarks>
/// Two implementations of this would drift, and the failure would be silent: the shell would emit
/// one spelling of a URL and a page another, doubling every cache entry rather than breaking
/// anything a test would notice.
/// </remarks>
internal static class AssetBaseResolver
{
    /// <summary>
    /// The Site's mount — its path base with the culture segment excluded, no trailing slash.
    /// </summary>
    /// <param name="http">The request, when there is one.</param>
    /// <param name="navigation">
    /// Fallback for an interactive circuit, where no <see cref="HttpContext"/> exists.
    /// </param>
    /// <param name="policy">
    /// The Site's culture policy, needed only on the circuit path — <c>BaseUri</c> is the rendered
    /// <c>&lt;base href&gt;</c>, which deliberately includes the language.
    /// </param>
    internal static string Resolve(HttpContext? http, NavigationManager? navigation, CultureUrlPolicy? policy)
    {
        if (http is not null)
        {
            // The feature records the path base from BEFORE the culture segment was appended, so
            // this is the mount exactly. Its absence means nothing was appended.
            var pathBase = http.Features.Get<ICultureUrlFeature>()?.SitePathBase
                ?? http.Request.PathBase.Value
                ?? string.Empty;

            return pathBase.TrimEnd('/');
        }

        if (navigation is null)
            return string.Empty;

        string path;
        try
        {
            path = new Uri(navigation.BaseUri).AbsolutePath.TrimEnd('/');
        }
        catch (InvalidOperationException)
        {
            // BaseUri throws before the circuit has initialised it. Rooting at the host is the
            // honest answer for a component rendering that early, and matches what an unmounted
            // Site would produce anyway.
            return string.Empty;
        }

        if (path.Length == 0 || policy is not { IsPrefixed: true })
            return path;

        // On a prefixed Site BaseUri is mount + language, because that is what <base href> has to
        // be for links. Drop the trailing segment when — and only when — it is a language.
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
            return path;

        return policy.Match(path[lastSlash..]) is null ? path : path[..lastSlash];
    }

    /// <summary>
    /// Roots one asset URL, leaving anything already absolute alone.
    /// </summary>
    /// <remarks>
    /// A leading slash is taken as the author asking for a host-absolute URL, including the
    /// consequence that it bypasses the mount. Off-site URLs, protocol-relative URLs and inline
    /// data are returned untouched.
    /// </remarks>
    internal static string Root(string? url, string mount)
    {
        if (string.IsNullOrEmpty(url)
            || url[0] == '/'
            || url.Contains("://", StringComparison.Ordinal)
            || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return url ?? string.Empty;

        return mount + "/" + url;
    }
}
