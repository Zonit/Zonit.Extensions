using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Zonit.Extensions.Cultures.Options;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Resolves a culture tag embedded in the URL path (e.g. <c>/en-us/home</c> or <c>/pl/home</c>)
/// and rewrites the path so downstream components see <c>/home</c>.
/// </summary>
/// <remarks>
/// <para>Pure regex + options — no <c>HttpContext</c> reference. The web pipeline glue lives
/// in <c>Zonit.Extensions.Website.Middlewares.CultureMiddleware</c>; non-web consumers
/// can still call this service directly to translate a path string.</para>
///
/// <para>Matching rules, in order:</para>
/// <list type="number">
///   <item>Exact match against <c>CultureOption.SupportedCultures</c> (BCP-47, lowercased,
///         e.g. <c>"en-us"</c>).</item>
///   <item>Primary-subtag fallback: <c>"pl"</c> matches when at least one supported tag
///         starts with <c>"pl-"</c>. The first such tag (typically the canonical one,
///         <c>"pl-pl"</c>) is returned. This lets visitors land on <c>/pl/home</c> without
///         enumerating every regional flavor.</item>
/// </list>
/// </remarks>
public sealed partial class DetectCultureService : IDisposable
{
    // Rebuilt on configuration reload, never mutated in place: readers take a single local
    // copy of the reference and work on an immutable snapshot, so a language added to
    // appsettings.json becomes routable without a restart and without locking the hot path.
    // Building the set here rather than per call keeps URL detection allocation-free.
    private volatile HashSet<string> _supportedCultures;
    private readonly IDisposable? _reload;

    public DetectCultureService(IOptionsMonitor<CultureOption> options)
    {
        _supportedCultures = Build(options.CurrentValue);
        _reload = options.OnChange(o => _supportedCultures = Build(o));
    }

    private static HashSet<string> Build(CultureOption options)
        => options.SupportedCultures
            .Select(c => c.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    public record PathCulture(string Url, string Culture);

    public PathCulture? GetUrl(string adres)
    {
        var match = GetUrlRegex().Match(adres);
        if (!match.Success)
            return null;

        var culture = match.Groups["culture"].Value.ToLowerInvariant();
        var url = match.Groups["url"].Value;

        // One read of the volatile field: a reload swapping the set mid-method must not make
        // the exact-match test and the subtag fallback below disagree about what is supported.
        var supportedCultures = _supportedCultures;

        // 1. Exact match (en-us, pl-pl).
        if (supportedCultures.Contains(culture))
            return new PathCulture(url, culture);

        // 2. Primary-subtag fallback: "en" → first supported "en-*".
        // Only triggers when the segment has no region (no hyphen). Otherwise an unknown
        // regional flavor (e.g. "en-au" when we only support "en-us") would silently fold
        // into "en-us", which is the wrong behavior — the URL should 404 instead.
        if (!culture.Contains('-'))
        {
            var prefix = culture + "-";
            foreach (var supported in supportedCultures)
                if (supported.StartsWith(prefix, StringComparison.Ordinal))
                    return new PathCulture(url, supported);
        }

        return null;
    }

    /// <summary>Drops the configuration-reload subscription.</summary>
    public void Dispose() => _reload?.Dispose();

    [GeneratedRegex(@"^/(?<culture>[a-z]{2}(?:-[a-z]{2})?)/(?<url>.+)", options: RegexOptions.Compiled | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GetUrlRegex();
}
