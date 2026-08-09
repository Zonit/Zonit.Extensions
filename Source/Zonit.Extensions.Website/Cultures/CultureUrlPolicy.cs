using Microsoft.Extensions.Options;
using System.Collections.Frozen;
using System.Collections.Immutable;
using Zonit.Extensions.Cultures.Options;

namespace Zonit.Extensions.Website.Cultures;

/// <summary>
/// Single source of truth for how a Site spells a culture in its URLs: which segment is
/// canonical for a language, which segments are merely accepted, and which cultures are
/// allowed into the index.
/// </summary>
/// <remarks>
/// <para><b>Layering.</b> The list of languages a host speaks belongs to
/// <c>Zonit.Extensions.Cultures</c>, which has no HTTP dependency and is shared with console
/// hosts and workers. What a language looks like in a <em>path</em> is a web decision, so it
/// lives here. This type reads <see cref="CultureOption.SupportedCultures"/> as the allow-list
/// and never widens it.</para>
///
/// <para><b>One instance per Site.</b> Two mounts under one host legitimately disagree — a
/// public site prefixes and indexes twenty languages while the panel next to it does neither —
/// so the policy is constructed per <c>SiteOptions</c> rather than registered once. It still
/// tracks configuration reloads: adding a language to <c>appsettings.json</c> makes it routable
/// in the running process, on every mount, without a restart.</para>
///
/// <para><b>Canonical versus accepted.</b> Every supported culture answers to two spellings —
/// the full tag (<c>pl-pl</c>) and its primary subtag (<c>pl</c>) — but exactly one is
/// canonical. The other still resolves so no inbound link dead-ends; the middleware answers it
/// with a permanent redirect instead of rendering, which is what stops one page from living at
/// two indexable addresses.</para>
///
/// <para><b>The ambiguity rule.</b> <see cref="CultureUrlFormat.Short"/> cannot apply to a
/// primary subtag claimed by more than one supported culture: with <c>pt-pt</c> and
/// <c>pt-br</c> configured, <c>/pt/</c> has no truthful expansion. Those keep their full tags
/// while unambiguous languages stay short. The bare <c>/pt/</c> is still accepted and resolves
/// deterministically — to the tag whose region repeats the language (<c>pt-pt</c>), else to
/// whichever comes first in <see cref="CultureOption.SupportedCultures"/> — so configuration
/// order is the tie-break and it is the author's to control.</para>
/// </remarks>
public sealed class CultureUrlPolicy : IDisposable
{
    private readonly IOptionsMonitor<CultureOption> _cultures;
    private readonly SiteSettingsProvider _settings;
    private readonly SiteCultureOptions _options;
    private volatile Snapshot _snapshot;
    private readonly IDisposable? _reload;

    public CultureUrlPolicy(IOptionsMonitor<CultureOption> cultures, SiteCultureOptions options, SiteSettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentNullException.ThrowIfNull(settings);

        _cultures = cultures;
        _settings = settings;
        _options = options;
        _snapshot = Snapshot.Build(cultures.CurrentValue, options, settings.Current);

        // Two independent sources feed the same lookup: the language allow-list from the Cultures
        // package and the URL policy from this Site's settings. Either can change at runtime, so
        // both are watched and both rebuild the same snapshot.
        _reload = cultures.OnChange(o => _snapshot = Snapshot.Build(o, _options, _settings.Current));
        settings.OnChange += s => _snapshot = Snapshot.Build(_cultures.CurrentValue, _options, s);
    }

    /// <summary>Whether this Site encodes the culture in its paths at all.</summary>
    public bool IsPrefixed => _options.Strategy == CultureUrlStrategy.Prefix;

    /// <summary>Every supported culture, canonical lowercase tags, in configuration order.</summary>
    public ImmutableArray<string> Cultures => _snapshot.Cultures;

    /// <summary>
    /// The subset of <see cref="Cultures"/> allowed into search indexes — the source for the
    /// <c>hreflang</c> cluster and the sitemap. Equals <see cref="Cultures"/> unless
    /// <c>SiteSettings.IndexedCultures</c> narrows it.
    /// </summary>
    public ImmutableArray<string> IndexedCultures => _snapshot.Indexed;

    /// <summary>
    /// Whether <paramref name="culture"/> may be indexed. A supported-but-unindexed culture
    /// renders normally; it is served <c>noindex, follow</c> and left out of the
    /// <c>hreflang</c> cluster, so the language works for visitors while staying out of search.
    /// </summary>
    public bool IsIndexed(string culture) => _snapshot.IndexedLookup.Contains(culture);

    /// <summary>
    /// The canonical URL segment for <paramref name="culture"/> — <c>"pl"</c> or <c>"pl-pl"</c>
    /// depending on <see cref="SiteCultureOptions.Format"/> and ambiguity. Returns
    /// <see langword="null"/> for a culture outside the allow-list, which is the caller's cue
    /// that there is no URL to build rather than a licence to invent one.
    /// </summary>
    public string? SegmentFor(string culture)
        => _snapshot.CultureToSegment.TryGetValue(culture, out var segment) ? segment : null;

    /// <summary>
    /// Splits a culture segment off the front of <paramref name="path"/>.
    /// </summary>
    /// <param name="path">
    /// An absolute request path (<c>"/pl/pricing"</c>, <c>"/pl"</c>, <c>"/pricing"</c>), already
    /// stripped of the mount path-base.
    /// </param>
    /// <returns>
    /// The match, or <see langword="null"/> when the first segment is not a recognised culture —
    /// including an unsupported regional flavour such as <c>/en-gb/</c> when only <c>en-us</c> is
    /// configured. Falling through is deliberate: folding an unknown region into a neighbouring
    /// one would serve a language the visitor did not ask for, under a URL claiming otherwise.
    /// </returns>
    /// <remarks>
    /// No allocation on the miss path — the segment is compared as a span against a
    /// case-insensitive frozen lookup, so the overwhelmingly common "no prefix here" request
    /// pays one hash and one comparison. Only a hit allocates, and only the remainder string the
    /// caller is about to assign to the request path anyway.
    /// </remarks>
    public CultureSegment? Match(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return null;

        // The segment runs from after the leading slash to the next slash, or to the end for a
        // bare "/pl". Both shapes must match: the language home page is the most valuable URL in
        // the cluster, and an implementation handling only "/pl/<rest>" silently 404s it.
        var afterSlash = path.AsSpan(1);
        var end = afterSlash.IndexOf('/');
        var segment = end < 0 ? afterSlash : afterSlash[..end];

        if (segment.IsEmpty)
            return null;

        var snapshot = _snapshot;
        if (!snapshot.SegmentLookup.TryGetValue(segment, out var culture))
            return null;

        // "/pl" and "/pl/" both mean the language root, so both reduce to "/". The
        // trailing-slash difference survives only in the ORIGINAL path, where the caller
        // compares it against the canonical build and redirects — rather than leaking into
        // routing as two distinct request paths for one page.
        var remainder = end < 0 || end + 1 >= afterSlash.Length
            ? "/"
            : path[(end + 1)..];

        return new CultureSegment(
            Culture: culture,
            Segment: segment.ToString(),
            IsCanonical: segment.Equals(snapshot.CultureToSegment[culture], StringComparison.OrdinalIgnoreCase),
            Remainder: remainder);
    }

    /// <summary>
    /// Composes the canonical prefixed path for <paramref name="culture"/> —
    /// <c>("pl-pl", "/pricing")</c> becomes <c>"/pl/pricing"</c> and <c>("pl-pl", "/")</c>
    /// becomes <c>"/pl/"</c>. Returns <see langword="null"/> for an unsupported culture.
    /// </summary>
    /// <remarks>
    /// The language root keeps its trailing slash on purpose. <c>/pl</c> and <c>/pl/</c> are two
    /// URLs to a search engine, so one has to be the answer, and the slashed form is what
    /// <c>PathBase</c> + <c>Path</c> reconstruct to once the request has been split. Emitting the
    /// other would guarantee a redirect on the busiest page of every language.
    /// </remarks>
    public string? BuildPath(string culture, string? remainder)
    {
        var segment = SegmentFor(culture);
        if (segment is null)
            return null;

        if (string.IsNullOrEmpty(remainder) || remainder == "/")
            return "/" + segment + "/";

        return remainder[0] == '/'
            ? "/" + segment + remainder
            : "/" + segment + "/" + remainder;
    }

    /// <summary>Drops the configuration-reload subscription.</summary>
    public void Dispose() => _reload?.Dispose();

    private sealed class Snapshot
    {
        public required FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> SegmentLookup { get; init; }
        public required FrozenDictionary<string, string> CultureToSegment { get; init; }
        public required FrozenSet<string> IndexedLookup { get; init; }
        public required ImmutableArray<string> Cultures { get; init; }
        public required ImmutableArray<string> Indexed { get; init; }

        public static Snapshot Build(CultureOption cultures, SiteCultureOptions options, SiteSettings settings)
        {
            // Distinct, but ORDER-PRESERVING: configuration order is the documented tie-break for
            // an ambiguous primary subtag, so a hash-based dedupe would quietly make the outcome
            // depend on hash order rather than on what the author wrote.
            var supported = cultures.SupportedCultures
                .Select(c => c.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();

            var byPrimary = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var culture in supported)
            {
                var primary = PrimarySubtag(culture);
                if (!byPrimary.TryGetValue(primary, out var group))
                    byPrimary[primary] = group = [];
                group.Add(culture);
            }

            var cultureToSegment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var culture in supported)
            {
                var primary = PrimarySubtag(culture);

                // The short form only survives where it is unambiguous, and the ambiguity is per
                // language — one shared subtag must not lengthen the other nineteen URLs.
                cultureToSegment[culture] =
                    options.Format == CultureUrlFormat.Full || byPrimary[primary].Count > 1
                        ? culture
                        : primary;
            }

            var segmentToCulture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Full tags first: unambiguous by construction, and they must never be shadowed by
            // the primary-subtag pass below (which can legitimately map "en" -> "en-us").
            foreach (var culture in supported)
                segmentToCulture[culture] = culture;

            foreach (var (primary, group) in byPrimary)
            {
                if (segmentToCulture.ContainsKey(primary))
                    continue; // a culture configured literally as "en" already claimed it

                segmentToCulture[primary] = group.Count == 1 ? group[0] : PreferredOf(primary, group);
            }

            // null means "everything", the intended default for a site whose translations are
            // complete. An explicit list is intersected with the allow-list rather than trusted:
            // a culture that is indexable but not supported cannot be rendered, and putting it in
            // a hreflang cluster would advertise a 404.
            var indexed = settings.IndexedCultures is { Length: > 0 } configured
                ? supported.Where(c => configured.Contains(c, StringComparer.OrdinalIgnoreCase)).ToImmutableArray()
                : supported;

            var frozenSegments = segmentToCulture.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            return new Snapshot
            {
                SegmentLookup = frozenSegments.GetAlternateLookup<ReadOnlySpan<char>>(),
                CultureToSegment = cultureToSegment.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                IndexedLookup = indexed.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                Cultures = supported,
                Indexed = indexed,
            };
        }

        private static string PrimarySubtag(string culture)
        {
            var dash = culture.IndexOf('-');
            return dash < 0 ? culture : culture[..dash];
        }

        /// <summary>
        /// Deterministic expansion of an ambiguous bare subtag. Prefers the tag whose region
        /// repeats the language (<c>pt</c> → <c>pt-pt</c>) because that is what a visitor typing
        /// <c>/pt/</c> almost always means; failing that, configuration order decides.
        /// </summary>
        private static string PreferredOf(string primary, List<string> group)
        {
            var selfRegion = primary + "-" + primary;
            foreach (var candidate in group)
                if (string.Equals(candidate, selfRegion, StringComparison.Ordinal))
                    return candidate;

            return group[0];
        }
    }
}

/// <summary>
/// A culture segment split off the front of a request path.
/// </summary>
/// <param name="Culture">Canonical lowercase BCP-47 tag, guaranteed to be in the allow-list.</param>
/// <param name="Segment">The segment exactly as it appeared in the URL.</param>
/// <param name="IsCanonical">
/// <see langword="false"/> when the visitor used an accepted-but-not-canonical spelling
/// (<c>/pl-pl/</c> where <c>/pl/</c> is canonical). The caller answers with a permanent redirect
/// rather than rendering, so the page keeps a single indexable address.
/// </param>
/// <param name="Remainder">
/// The path with the segment removed, always rooted. <c>"/pl"</c>, <c>"/pl/"</c> and the language
/// home page all yield <c>"/"</c>.
/// </param>
public readonly record struct CultureSegment(
    string Culture,
    string Segment,
    bool IsCanonical,
    string Remainder);
