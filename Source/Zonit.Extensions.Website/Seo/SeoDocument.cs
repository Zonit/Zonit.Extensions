using System.Globalization;
using Zonit.Extensions.Tenants.Settings;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website;

/// <summary>
/// One <c>hreflang</c> entry — a language and the URL that serves this page in it.
/// </summary>
public readonly record struct SeoAlternate(string Hreflang, string Url);

/// <summary>
/// Everything the document head needs, already composed. Produced by
/// <see cref="SeoDocumentBuilder"/> from the page's <see cref="PageMeta"/>, the tenant's site
/// settings and the request's culture URL feature.
/// </summary>
/// <remarks>
/// A plain value separated from the component that renders it, so the composition rules — title
/// order, canonical resolution, when a page is allowed into the index — can be exercised without
/// a renderer, an <c>HttpContext</c> or a tenant.
/// </remarks>
public sealed record SeoDocument
{
    /// <summary>Composed document title. Never empty — falls back to the website title.</summary>
    public required string Title { get; init; }

    /// <summary>Meta description, falling back to the tenant's. <see langword="null"/> emits no tag.</summary>
    public string? Description { get; init; }

    /// <summary>Absolute canonical URL, or <see langword="null"/> when none can be derived.</summary>
    public string? Canonical { get; init; }

    /// <summary>
    /// The <c>hreflang</c> cluster, self-reference included. Empty when the Site does not prefix
    /// cultures or when this page is not indexable — a cluster on a <c>noindex</c> page is noise.
    /// </summary>
    public IReadOnlyList<SeoAlternate> Alternates { get; init; } = [];

    /// <summary>
    /// The <c>x-default</c> target — the unprefixed URL that routes visitors by their own
    /// language. <see langword="null"/> when the Site does not offer one.
    /// </summary>
    public string? XDefault { get; init; }

    /// <summary>
    /// <c>robots</c> directive, or <see langword="null"/> to emit no tag at all (which is what
    /// "index, follow" means — the tag would be pure noise).
    /// </summary>
    public string? Robots { get; init; }

    /// <summary>Absolute social-preview image URL.</summary>
    public string? Image { get; init; }

    /// <summary><c>og:type</c>.</summary>
    public required string Type { get; init; }

    /// <summary><c>og:site_name</c>.</summary>
    public string? SiteName { get; init; }

    /// <summary><c>og:locale</c>, underscore form (<c>pl_PL</c>).</summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Rendered JSON-LD for the <c>application/ld+json</c> block, or <see langword="null"/> when
    /// the page declared no structured data.
    /// </summary>
    public string? StructuredData { get; init; }

    /// <summary>
    /// Value equality including the <c>hreflang</c> cluster.
    /// </summary>
    /// <remarks>
    /// The compiler-generated record equality would compare <see cref="Alternates"/> by
    /// reference, and the builder allocates a fresh array every time — so every re-composition
    /// would look like a change. The head component uses this to decide whether to re-render,
    /// and a page that mutates its <see cref="PageMeta"/> notifies after each render; without a
    /// truthful comparison here that pair is an infinite render loop, not a micro-optimisation.
    /// </remarks>
    public bool Equals(SeoDocument? other)
        => other is not null
        && Title == other.Title
        && Description == other.Description
        && Canonical == other.Canonical
        && XDefault == other.XDefault
        && Robots == other.Robots
        && Image == other.Image
        && Type == other.Type
        && SiteName == other.SiteName
        && Locale == other.Locale
        && StructuredData == other.StructuredData
        && Alternates.SequenceEqual(other.Alternates);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Title, Description, Canonical, Robots, Image, Type, Locale, Alternates.Count);
}

/// <summary>
/// Composes a <see cref="SeoDocument"/>. Pure — no DI, no rendering, no side effects.
/// </summary>
public static class SeoDocumentBuilder
{
    /// <param name="meta">The routed page's metadata, or <see langword="null"/> if it published none.</param>
    /// <param name="site">Tenant site settings — website title, fallback description, title composition.</param>
    /// <param name="url">
    /// The request's culture URL feature. <see langword="null"/> outside an HTTP request (an
    /// interactive circuit), in which case URLs are omitted and only the title and description
    /// are composed — those still matter for the browser tab, and search engines only ever see
    /// the server-rendered pass anyway.
    /// </param>
    /// <param name="culture">Canonical BCP-47 tag of the active culture.</param>
    /// <param name="social">Tenant social profiles, feeding the organization's <c>sameAs</c>.</param>
    /// <param name="breadcrumbs">Active breadcrumb trail, feeding the <c>BreadcrumbList</c> node.</param>
    /// <param name="translate">
    /// Translation lookup applied to the page's title and description, unless the page set
    /// <see cref="PageMeta.Translate"/> to <see langword="false"/>. <see langword="null"/> leaves
    /// the text alone — which is what a caller outside a request scope should pass.
    /// </param>
    /// <param name="gated">
    /// Whether reaching the routed page needs an authenticated principal. Such a page is kept out
    /// of the index without being disallowed — see <see cref="PageIndexing"/>.
    /// </param>
    /// <param name="error">
    /// Whether this render is the pipeline replaying a failed request with an error route. Such a
    /// page emits no canonical and no cluster: it is not content, and the address it is standing
    /// in for does not exist.
    /// </param>
    /// <param name="declaredCultures">
    /// Languages from the page's <c>[WebsiteSitemap]</c> — the static declaration, and the same one
    /// the sitemap reads. <see cref="PageMeta.Cultures"/> overrides it, because a page that loaded
    /// its own data knows things the attribute could not.
    /// </param>
    public static SeoDocument Build(
        PageMeta? meta,
        SiteSettingsModel site,
        ICultureUrlFeature? url,
        string culture,
        SocialMediaModel? social = null,
        IReadOnlyList<BreadcrumbsModel>? breadcrumbs = null,
        Func<string, string>? translate = null,
        bool gated = false,
        bool error = false,
        IReadOnlyList<Culture>? declaredCultures = null)
    {
        ArgumentNullException.ThrowIfNull(site);

        // Author text goes through the translation registry unless the page opted out. The key in
        // this framework IS the English source string, so a literal Description is already a
        // valid key; text with no rendition falls through to itself, which is what makes this
        // safe for dynamic values too.
        var localize = meta?.Translate != false && translate is not null ? translate : static s => s;

        var title = ComposeTitle(Apply(localize, meta?.Title), site);
        var description = Coalesce(Apply(localize, meta?.Description), site.MetaDescription);

        if (url is null)
        {
            return new SeoDocument
            {
                Title = title,
                Description = description,
                Type = meta?.Type ?? "website",
                SiteName = site.Title,
                Locale = ToOpenGraphLocale(culture),
            };
        }

        var origin = ResolveOrigin(site, url);
        var prefix = origin + url.SitePathBase;

        // An error page is the same component rendered under whatever address failed, so every
        // signal that says "this is a page" has to be withheld. A canonical is the sharpest of
        // them: it asserts that content lives at a URL, and on a 404 there is no content and no
        // URL it lives at. The status code alone does not cover this — a canonical pointing at a
        // real address invites consolidation onto it, and one pointing at the error route
        // advertises the error route as a page.
        var canonical = error ? null : ResolveCanonical(meta, url, culture, prefix);
        var robots = error ? "noindex, follow" : ResolveRobots(meta, declaredCultures, url, culture, gated);

        // A cluster is only meaningful when the page is actually a candidate for the index and
        // the languages live at distinct addresses. On a noindex page it is ignored at best and
        // contradictory at worst.
        //
        // Test the directive's CONTENT, not its presence. This used to read `robots is null`,
        // which was true exactly while the indexable case emitted no tag — the moment that case
        // started emitting `index, follow, ...`, every page would have looked non-indexable and
        // the whole hreflang cluster plus x-default would have vanished from the site. Silently:
        // valid HTML, no error, just no alternates.
        var indexable = robots is null
            || !robots.Contains("noindex", StringComparison.OrdinalIgnoreCase);
        var alternates = indexable && url.Policy.IsPrefixed
            ? BuildAlternates(meta, declaredCultures, url, prefix)
            : [];

        return new SeoDocument
        {
            Title = title,
            Description = description,
            Canonical = canonical,
            Alternates = alternates,
            XDefault = indexable && url.Policy.IsPrefixed
                ? prefix + url.RoutePath
                : null,
            Robots = robots,
            // Page image, else the tenant.s default share image. A site with one good 1200x630
            // banner gets a correct social preview on every page without a single page saying so,
            // and a page with its own still wins.
            Image = ResolveImage(Coalesce(meta?.Image, site.SocialImageUrl), origin),
            Type = meta?.Type ?? "website",
            SiteName = site.Title,
            Locale = ToOpenGraphLocale(culture),

            // Structured data is only emitted on a page that may be indexed. On a noindex page
            // it is at best ignored and at worst read as a contradiction — a page asserting it
            // is an Article while asking not to be listed as one.
            StructuredData = indexable
                ? Schema.SchemaWriter.Write(Schema.SchemaComposer.Compose(meta, new Schema.SchemaContext(
                    Site: site,
                    Title: Apply(localize, meta?.Title),
                    Description: Apply(localize, meta?.Description),
                    Social: social,
                    Breadcrumbs: breadcrumbs ?? [],
                    Origin: origin,
                    Canonical: canonical,
                    Image: ResolveImage(Coalesce(meta?.Image, site.SocialImageUrl), origin),
                    IsHome: url.RoutePath is "/" or "")), canonical)
                : null,
        };
    }

    /// <summary>
    /// <c>"Pricing"</c> + <c>"Zonit"</c> → <c>"Pricing - Zonit"</c>. A page with no title of its
    /// own renders the website title alone rather than a separator with nothing on one side.
    /// </summary>
    private static string ComposeTitle(string? pageTitle, SiteSettingsModel site)
    {
        var page = Trim(pageTitle);
        var website = Trim(site.Title);

        if (page is null)
            return website ?? string.Empty;

        if (website is null || site.TitlePosition == SiteTitlePosition.None)
            return page;

        var separator = site.TitleSeparator;
        if (string.IsNullOrEmpty(separator))
            return page;

        return site.TitlePosition == SiteTitlePosition.Prefix
            ? website + separator + page
            : page + separator + website;
    }

    /// <summary>
    /// Tenant setting first, then the request. The tenant wins
    /// because it is the only layer that knows which of several domains this request arrived on
    /// is the one that should appear in search results.
    /// </summary>
    private static string ResolveOrigin(SiteSettingsModel site, ICultureUrlFeature url)
        => Trim(site.CanonicalUrl)?.TrimEnd('/') ?? url.Origin;

    private static string? ResolveCanonical(
        PageMeta? meta, ICultureUrlFeature url, string culture, string prefix)
    {
        if (Trim(meta?.Canonical) is { } explicitCanonical)
        {
            return IsAbsolute(explicitCanonical)
                ? explicitCanonical
                : prefix + WithCulture(url, culture, Rooted(explicitCanonical));
        }

        return prefix + WithCulture(url, culture, url.LocalizedPath);
    }

    /// <summary>
    /// Default directive for a page that is free to be indexed.
    /// </summary>
    /// <remarks>
    /// <para><c>index, follow</c> on its own would be noise — it is what a crawler assumes from an
    /// absent tag. The three limits after it are not: <c>max-image-preview:large</c> is what makes
    /// a result eligible for the large image thumbnail, and the two <c>-1</c>s lift the default
    /// caps on snippet length and video preview. All three default to a conservative value, so a
    /// site that says nothing is opting into the smaller presentation without meaning to.</para>
    ///
    /// <para>Emitting the positive form also makes indexability visible in the page itself. "No
    /// tag" and "the head renderer never ran" look identical in view-source, and that ambiguity
    /// costs an afternoon every time someone audits why a page is missing from the index.</para>
    /// </remarks>
    public const string DefaultRobots = "index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1";

    /// <summary>
    /// Resolves the <c>robots</c> directive. <see langword="null"/> means "emit no tag".
    /// </summary>
    private static string? ResolveRobots(PageMeta? meta, IReadOnlyList<Culture>? declared, ICultureUrlFeature url, string culture, bool gated)
    {
        // A closed Site is closed outright: its links lead to more of the same, so there is
        // nothing to gain from letting them be followed. Checked before the page override —
        // withholding is a Site-level decision a page must not be able to reverse.
        if (!url.Indexable)
            return "noindex, nofollow";

        // A language withheld from the index still wants its links crawled — that is how the
        // indexed languages are discovered from it. Same reasoning: not a page's call.
        if (url.Policy.IsPrefixed && !url.Policy.IsIndexed(culture))
            return "noindex, follow";

        // Explicit page directive wins over NoIndex; empty means "emit nothing".
        if (meta?.Robots is { } robots)
            return robots.Length == 0 ? null : robots;

        if (meta?.NoIndex == true)
            return "noindex, follow";

        // Rendering outside the languages the content exists in: the page is showing its fallback
        // rendition, so this address holds a copy of another language's text. Reachable and
        // crawlable, not a search result.
        if (!Exists(meta, declared, culture))
            return "noindex, follow";

        // Behind authorization: keep it out of the index, keep its links followable.
        if (gated)
            return "noindex, follow";

        return DefaultRobots;
    }

    /// <summary>
    /// Whether the page's content exists in <paramref name="culture"/>. No declaration means every
    /// language, which is the truth for anything translated from resource files.
    /// </summary>
    private static bool Exists(PageMeta? meta, IReadOnlyList<Culture>? declared, string culture)
    {
        // PageMeta first: it is set from data the page loaded, so it knows things the attribute
        // could not. The attribute is the static declaration and the answer for every page whose
        // languages are fixed at compile time.
        var cultures = meta?.Cultures ?? declared;
        if (cultures is null)
            return true;

        foreach (var candidate in cultures)
        {
            if (string.Equals(candidate.Value, culture, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static SeoAlternate[] BuildAlternates(PageMeta? meta, IReadOnlyList<Culture>? declared, ICultureUrlFeature url, string prefix)
    {
        var indexed = url.Policy.IndexedCultures;
        var result = new List<SeoAlternate>(indexed.Length);

        foreach (var culture in indexed)
        {
            // A version that does not exist is not an alternate. Listing it points the crawler at
            // a page serving another language's text under a noindex directive, and a cluster with
            // a member like that is dropped whole — so this filter is what keeps the translations
            // that DO exist clustered together.
            if (!Exists(meta, declared, culture))
                continue;

            // A page-supplied alternate wins: it is the only source that can know a translated
            // slug, because the slug lives in the content store rather than in configuration.
            var path = meta is not null && meta.Alternates.TryGetValue(culture, out var custom)
                ? Rooted(custom)
                : url.Routes.ToLocalized(culture, url.RoutePath);

            var built = url.Policy.BuildPath(culture, path);
            if (built is null)
                continue;

            result.Add(new SeoAlternate(ToHreflang(url, culture), prefix + built));
        }

        return [.. result];
    }

    /// <summary>
    /// The <c>hreflang</c> value mirrors the URL segment: a site serving <c>/pl/</c> declares
    /// <c>pl</c>, one serving <c>/pt-br/</c> declares <c>pt-BR</c>. Claiming a regional target
    /// the URLs do not actually distinguish would split signals the site never meant to split.
    /// </summary>
    private static string ToHreflang(ICultureUrlFeature url, string culture)
    {
        var segment = url.Policy.SegmentFor(culture) ?? culture;
        var dash = segment.IndexOf('-');
        return dash < 0
            ? segment
            : string.Concat(segment.AsSpan(0, dash + 1), segment.AsSpan(dash + 1).ToString().ToUpperInvariant());
    }

    private static string WithCulture(ICultureUrlFeature url, string culture, string path)
        => url.Policy.IsPrefixed ? url.Policy.BuildPath(culture, path) ?? path : path;

    private static string? ResolveImage(string? image, string origin)
    {
        var trimmed = Trim(image);
        if (trimmed is null)
            return null;

        return IsAbsolute(trimmed) ? trimmed : origin + Rooted(trimmed);
    }

    /// <summary><c>pl-pl</c> → <c>pl_PL</c>. Falls back to the raw tag for anything ICU cannot parse.</summary>
    private static string? ToOpenGraphLocale(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(culture).Name.Replace('-', '_');
        }
        catch (CultureNotFoundException)
        {
            return culture.Replace('-', '_');
        }
    }

    private static bool IsAbsolute(string value)
        => value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("//", StringComparison.Ordinal);

    private static string Rooted(string path)
        => path.Length == 0 || path[0] == '/' ? path : "/" + path;

    private static string? Apply(Func<string, string> localize, string? value)
        => string.IsNullOrWhiteSpace(value) ? value : localize(value);

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Coalesce(string? first, string? second)
        => Trim(first) ?? Trim(second);
}
