using System.Collections.Frozen;

namespace Zonit.Extensions.Website.Cultures;

/// <summary>
/// Translates request paths between the shape a culture uses (<c>/aktualnosci/nowa-wersja</c>)
/// and the shape the Blazor router knows (<c>/news/nowa-wersja</c>), in both directions.
/// </summary>
/// <remarks>
/// <para><b>Forward</b> (<see cref="ToRoute"/>) runs in the request pipeline before routing, so a
/// component keeps one <c>@page</c> template no matter how many languages rename its path.
/// <b>Reverse</b> (<see cref="ToLocalized"/>) runs when links are generated — the
/// <c>hreflang</c> cluster, the language switcher, the sitemap — so the URL advertised for
/// German is the one German visitors actually get.</para>
///
/// <para><b>Only the static head is mapped.</b> <c>/aktualnosci/{slug}</c> contributes the prefix
/// <c>/aktualnosci</c>; everything after it rides along untouched. A translated <em>slug</em> is
/// a different problem and cannot be solved here, because the slug lives in the content store —
/// the page supplies those alternates itself.</para>
///
/// <para><b>Immutable and built once.</b> The table depends only on what the Site declared, not
/// on the language allow-list, so it needs no reload subscription. Filtering by supported
/// cultures would be redundant anyway: a lookup is only ever performed with a culture that
/// <see cref="CultureUrlPolicy"/> already resolved, and that only ever yields supported ones.</para>
///
/// <para><b>Cost.</b> One frozen-dictionary hit on the culture, then a linear scan of that
/// culture's prefixes, ordered longest-first so the most specific route wins. Sites declare tens
/// of these; the scan beats a hash lookup that would first have to guess how many segments of
/// the path to slice off. Sites that declare none pay a single <see cref="IsEmpty"/> test.</para>
/// </remarks>
public sealed class LocalizedRouteTable
{
    /// <summary>A table with nothing in it — the common case, and the cheapest possible one.</summary>
    public static readonly LocalizedRouteTable Empty = new([]);

    private readonly FrozenDictionary<string, Mapping[]> _toRoute;
    private readonly FrozenDictionary<string, Mapping[]> _toLocalized;

    public LocalizedRouteTable(IReadOnlyList<AreaRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var forward = new Dictionary<string, List<Mapping>>(StringComparer.OrdinalIgnoreCase);
        var reverse = new Dictionary<string, List<Mapping>>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in routes)
        {
            var canonical = StaticPrefix(route.Template);
            if (canonical.Length == 0)
                continue;

            if (route.Localized is null) continue;

            foreach (var (culture, localizedTemplate) in route.Localized)
            {
                var localized = StaticPrefix(localizedTemplate);

                // A culture that spells the route the same way needs no entry: the identity
                // mapping is what both directions already do when they find no match.
                if (localized.Length == 0 || string.Equals(localized, canonical, StringComparison.OrdinalIgnoreCase))
                    continue;

                Add(forward, culture, new Mapping(localized, canonical));
                Add(reverse, culture, new Mapping(canonical, localized));
            }
        }

        _toRoute = Freeze(forward);
        _toLocalized = Freeze(reverse);
    }

    /// <summary>Whether any route is localized at all. Lets callers skip the lookup entirely.</summary>
    public bool IsEmpty => _toRoute.Count == 0;

    /// <summary>
    /// Rewrites a culture-specific path onto its canonical route —
    /// <c>("pl-pl", "/aktualnosci/nowa-wersja")</c> becomes <c>"/news/nowa-wersja"</c>.
    /// Returns <paramref name="path"/> unchanged when nothing matches, which is the correct
    /// answer for the overwhelming majority of paths.
    /// </summary>
    public string ToRoute(string culture, string path) => Apply(_toRoute, culture, path);

    /// <summary>
    /// Rewrites a canonical route onto its culture-specific path —
    /// <c>("de-de", "/news/x")</c> becomes <c>"/nachrichten/x"</c>. The inverse of
    /// <see cref="ToRoute"/>, and the form that must appear in <c>hreflang</c>, the language
    /// switcher and the sitemap.
    /// </summary>
    public string ToLocalized(string culture, string path) => Apply(_toLocalized, culture, path);

    private static string Apply(FrozenDictionary<string, Mapping[]> table, string culture, string path)
    {
        if (table.Count == 0 || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(culture))
            return path;

        if (!table.TryGetValue(culture, out var mappings))
            return path;

        foreach (var mapping in mappings)
        {
            if (!path.StartsWith(mapping.From, StringComparison.OrdinalIgnoreCase))
                continue;

            // The prefix must end on a segment boundary. Without this "/news" would also claim
            // "/newsletter" and quietly rewrite an unrelated page onto the wrong route.
            if (path.Length != mapping.From.Length && path[mapping.From.Length] != '/')
                continue;

            return mapping.To + path[mapping.From.Length..];
        }

        return path;
    }

    /// <summary>
    /// The literal head of a route template — everything before the first parameter, with any
    /// trailing slash removed so the segment-boundary test below is uniform. <c>"/news/{slug}"</c>
    /// yields <c>"/news"</c>; <c>"/about"</c> yields itself.
    /// </summary>
    private static string StaticPrefix(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var brace = template.IndexOf('{');
        var head = brace < 0 ? template : template[..brace];

        head = head.TrimEnd('/');
        if (head.Length == 0)
            return string.Empty;

        return head[0] == '/' ? head : "/" + head;
    }

    private static void Add(Dictionary<string, List<Mapping>> target, string culture, Mapping mapping)
    {
        if (!target.TryGetValue(culture, out var list))
            target[culture] = list = [];
        list.Add(mapping);
    }

    /// <summary>
    /// Longest prefix first, so <c>/news/archive</c> wins over <c>/news</c> when both are
    /// declared. <see cref="Enumerable.OrderByDescending{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey})"/>
    /// is stable, so equal-length prefixes keep registration order and the outcome stays the
    /// author's to control.
    /// </summary>
    private static FrozenDictionary<string, Mapping[]> Freeze(Dictionary<string, List<Mapping>> source)
        => source.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderByDescending(m => m.From.Length).ToArray(),
            StringComparer.OrdinalIgnoreCase);

    private readonly record struct Mapping(string From, string To);
}
