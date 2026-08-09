namespace Zonit.Extensions.Website.Cultures;

/// <summary>
/// Everything the culture middleware learned about the current request, published on
/// <c>HttpContext.Features</c> for the rest of the pipeline and the document renderer.
/// </summary>
/// <remarks>
/// <para>A request feature rather than an <c>HttpContext.Items</c> entry: the consumers are
/// framework code on a hot path, and a typed feature is one array probe with no string key, no
/// boxing and no chance of two components disagreeing about the spelling of a magic string.</para>
///
/// <para>Installed on <b>every</b> request that reaches a Site branch, including Sites with
/// <see cref="CultureUrlStrategy.None"/>. Those get <see cref="Segment"/> = <c>""</c> and
/// <see cref="RoutePath"/> = <see cref="LocalizedPath"/>, so a renderer reads one shape
/// regardless of strategy instead of branching on whether the feature exists.</para>
/// </remarks>
public interface ICultureUrlFeature
{
    /// <summary>Canonical lowercase BCP-47 tag resolved for this request.</summary>
    string Culture { get; }

    /// <summary>
    /// The culture segment as it appears in the URL, or <c>""</c> when the Site does not prefix.
    /// Always the canonical spelling by the time anything reads this — a non-canonical spelling
    /// is answered with a redirect and never reaches the pipeline.
    /// </summary>
    string Segment { get; }

    /// <summary>
    /// <c>Request.PathBase</c> as it was <em>before</em> the culture segment was appended — the
    /// mount's own path base (<c>""</c> for a root Site, <c>"/admin"</c> for a panel).
    /// </summary>
    /// <remarks>
    /// Mount resolution must use this rather than the live <c>Request.PathBase</c>, which now
    /// carries the language too. Without it <c>WebsiteMountRegistry</c> would be asked to find
    /// the owner of <c>"/pl"</c>, and would answer correctly only by accident of its
    /// longest-prefix fallback — and would answer <em>wrongly</em> for a host that mounts a Site
    /// at a path colliding with a language code.
    /// </remarks>
    string SitePathBase { get; }

    /// <summary>
    /// The path the Blazor router sees: culture segment removed and any localized route folded
    /// back onto its canonical template. <c>/pl/aktualnosci/x</c> yields <c>/news/x</c>.
    /// </summary>
    string RoutePath { get; }

    /// <summary>
    /// The path as the visitor sees it — culture segment removed, localized spelling kept.
    /// <c>/pl/aktualnosci/x</c> yields <c>/aktualnosci/x</c>.
    /// </summary>
    /// <remarks>
    /// This is what a page's own canonical URL is built from. <see cref="RoutePath"/> is for
    /// logic; this is for links, because it is the address that actually resolves in this
    /// language.
    /// </remarks>
    string LocalizedPath { get; }

    /// <summary>URL policy of the Site serving this request.</summary>
    CultureUrlPolicy Policy { get; }

    /// <summary>Localized-route table of the Site serving this request.</summary>
    LocalizedRouteTable Routes { get; }

    /// <summary>
    /// Absolute origin (<c>"https://example.com"</c>, no trailing slash) to prefix onto absolute
    /// URLs emitted into the page.
    /// </summary>
    /// <remarks>
    /// Evaluated on first read, not when the feature is created. The middleware that installs
    /// this runs before tenant hydration, so an eagerly-computed origin could never consult a
    /// per-tenant setting; deferring costs nothing and keeps the canonical tag, the
    /// <c>hreflang</c> cluster and Open Graph reading from one resolved value.
    /// </remarks>
    string Origin { get; }

    /// <summary>Whether pages under this Site may be indexed (see <c>SiteOptions.Indexable</c>).</summary>
    bool Indexable { get; }
}

internal sealed class CultureUrlFeature : ICultureUrlFeature
{
    private readonly Func<string> _origin;
    private string? _resolvedOrigin;

    public CultureUrlFeature(Func<string> origin) => _origin = origin;

    public required string Culture { get; init; }
    public required string Segment { get; init; }
    public required string SitePathBase { get; init; }
    public required string RoutePath { get; init; }
    public required string LocalizedPath { get; init; }
    public required CultureUrlPolicy Policy { get; init; }
    public required LocalizedRouteTable Routes { get; init; }
    public required bool Indexable { get; init; }

    public string Origin => _resolvedOrigin ??= _origin();
}
