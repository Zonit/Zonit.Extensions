using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Keeps the machine-readable descriptors — <c>robots.txt</c>, <c>sitemap.xml</c>,
/// <c>llms.txt</c> — at one address per Site instead of one per language.
/// </summary>
/// <remarks>
/// <para><b>The problem.</b> On a prefixed Site the culture middleware moves the language
/// segment out of the path and into <c>PathBase</c> — including for requests it otherwise
/// leaves alone, because assets under a <c>&lt;base href&gt;</c> of <c>/pl/</c> are fetched
/// with the prefix and would 404 without the split. These three files carry skipped
/// extensions, so they take that same route: routing then sees a bare <c>/llms.txt</c> and
/// answers it. The effect is that <c>/pl/llms.txt</c>, <c>/de/llms.txt</c> and every other
/// language served a byte-identical copy of a file that is not translated and has no business
/// existing more than once.</para>
///
/// <para><b>Why a redirect and not a 404.</b> Two of the three are defined by their address:
/// a crawler reads <c>robots.txt</c> from the origin root and nowhere else, so the prefixed
/// forms answer nobody. But a duplicate that is merely unreachable-by-convention is still a
/// duplicate the moment anything links to it, and 301 is what the rest of this middleware
/// already does with every other non-canonical spelling of a URL. It costs one round trip on a
/// path nothing should be taking in the first place.</para>
///
/// <para><b>Why the mount is subtracted rather than the segment matched.</b> Inside a Site's
/// branch, <c>UsePathBase</c> contributes the mount and the culture middleware is the only
/// thing that appends after it. Anything <c>PathBase</c> carries beyond the mount is therefore
/// the language, whatever it is spelled as — no second parse of the segment, and no way for the
/// two to disagree about which spellings count.</para>
/// </remarks>
internal static class SiteRootDescriptor
{
    /// <summary>
    /// Answers a language-prefixed request for a site-root descriptor with a permanent redirect
    /// to its single address, and reports whether it did. <see langword="false"/> means the
    /// caller should serve the file normally.
    /// </summary>
    internal static bool Redirected(HttpContext context)
    {
        var current = context.RequestServices.GetRequiredService<ICurrentSite>();

        // An unprefixed Site never appends anything, so there is nothing here to undo.
        if (current.UrlPolicy is not { IsPrefixed: true })
            return false;

        var mount = Mount(current.Directory);
        var pathBase = context.Request.PathBase.Value ?? string.Empty;

        if (pathBase.Length <= mount.Length)
            return false;

        context.Response.Redirect(
            mount + context.Request.Path + context.Request.QueryString,
            permanent: true);

        return true;
    }

    /// <summary>
    /// The Site's own path base — the same normalisation <c>UsePathBase</c> was given, so the
    /// two cannot drift.
    /// </summary>
    private static string Mount(UrlPath directory)
    {
        if (!directory.HasValue)
            return string.Empty;

        var value = directory.Value.TrimEnd('/');
        if (value.Length == 0)
            return string.Empty;

        return value.StartsWith('/') ? value : "/" + value;
    }
}
