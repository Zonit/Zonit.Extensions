using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Website.Schema;

/// <summary>
/// Everything the composer can read without the page saying anything.
/// </summary>
/// <param name="Site">Tenant site identity — name, description, logo.</param>
/// <param name="Title">The page.s own title, already translated. Falls back to the site title.</param>
/// <param name="Description">The page.s description, already translated.</param>
/// <param name="Social">Tenant social profiles, which become the organization's <c>sameAs</c>.</param>
/// <param name="Breadcrumbs">Active breadcrumb trail, or empty.</param>
/// <param name="Origin">Absolute origin, for turning site-relative paths into absolute URLs.</param>
/// <param name="Canonical">Absolute canonical URL of the page being rendered.</param>
/// <param name="Image">Absolute share image — the page.s own, else the tenant default.</param>
/// <param name="IsHome">Whether this is the Site's home page in the active culture.</param>
public readonly record struct SchemaContext(
    SiteSettingsModel Site,
    string? Title,
    string? Description,
    SocialMediaModel? Social,
    IReadOnlyList<BreadcrumbsModel> Breadcrumbs,
    string Origin,
    string? Canonical,
    string? Image,
    bool IsHome);

/// <summary>
/// Builds the structured-data graph for a page out of state the framework already holds, and
/// merges in whatever the page added on top.
/// </summary>
/// <remarks>
/// <para><b>Why derive it.</b> Almost everything a basic graph needs is already known by the time
/// the head renders: the page has a title and a description, the request has a canonical URL, the
/// tenant has a site name and a logo and its social profiles, and the breadcrumb provider has the
/// trail. Asking a page to restate all of that as schema nodes guarantees two things — most pages
/// will not bother, and the ones that do will drift out of step with the <c>&lt;title&gt;</c>
/// above them. Deriving it means every page gets correct structured data by existing, and a page
/// only writes what genuinely cannot be inferred: publication dates, authors, prices.</para>
///
/// <para><b>What is derived.</b></para>
/// <list type="bullet">
///   <item>The page node — <c>Article</c> when <see cref="PageMeta.Type"/> says so, otherwise
///         <c>WebPage</c> — from the title, description, image and canonical.</item>
///   <item><c>BreadcrumbList</c> from the breadcrumb trail, when it has more than one entry. A
///         one-item trail is not a hierarchy and Google ignores it.</item>
///   <item><c>Organization</c> and <c>WebSite</c> from tenant settings, <b>on the home page
///         only</b> — that is where the documented placement is, and repeating the publisher on
///         every page adds bytes without adding meaning.</item>
/// </list>
///
/// <para><b>The page always wins.</b> A node the page supplied replaces the derived one of the
/// same type rather than joining it: two <c>Article</c> nodes describing one page is a
/// contradiction, and the page knows more than the inference does. That is also the escape hatch
/// — supply a fully-formed <c>Article</c> and none of the guessing applies.</para>
/// </remarks>
public static class SchemaComposer
{
    /// <summary>
    /// Produces the final node list: derived nodes, overridden by anything the page supplied.
    /// </summary>
    public static IReadOnlyList<SchemaThing> Compose(PageMeta? meta, in SchemaContext context)
    {
        var supplied = meta?.Schema;
        var auto = meta?.AutoSchema ?? true;

        if (!auto)
            return supplied is { Count: > 0 } ? supplied : [];

        var nodes = new List<SchemaThing>(4);

        var page = BuildPageNode(meta, context);
        if (page is not null)
            nodes.Add(page);

        var breadcrumbs = BuildBreadcrumbs(context);
        if (breadcrumbs is not null)
            nodes.Add(breadcrumbs);

        if (context.IsHome)
        {
            var organization = BuildOrganization(context);
            if (organization is not null)
                nodes.Add(organization);

            var website = BuildWebSite(context);
            if (website is not null)
                nodes.Add(website);
        }

        if (supplied is not { Count: > 0 })
            return nodes;

        // Page-supplied nodes replace derived ones of the same type, then anything left over is
        // appended. Matching on the runtime type keeps this predictable: a page adding a
        // BreadcrumbList silences the derived trail and nothing else.
        foreach (var node in supplied)
            nodes.RemoveAll(existing => existing.GetType() == node.GetType());

        nodes.AddRange(supplied);
        return nodes;
    }

    private static SchemaThing? BuildPageNode(PageMeta? meta, in SchemaContext context)
    {
        var name = Trim(context.Title) ?? Trim(context.Site.Title);
        var description = Trim(context.Description) ?? Trim(context.Site.MetaDescription);
        var image = context.Image;

        if (name is null && description is null)
            return null;

        // "article" is the only og:type that maps onto a distinct rich result worth emitting from
        // inference alone. Anything else is described honestly as a WebPage rather than guessed.
        if (string.Equals(meta?.Type, "article", StringComparison.OrdinalIgnoreCase))
        {
            return new SchemaArticle
            {
                Headline = name,
                Description = description,
                Images = image is null ? null : [image],
                Url = context.Canonical,
                Publisher = BuildOrganization(context),
            };
        }

        return new SchemaWebPage
        {
            Name = name,
            Description = description,
            PrimaryImage = image,
            Url = context.Canonical,
        };
    }

    /// <summary>
    /// Turns the breadcrumb trail into a <c>BreadcrumbList</c>. The last item deliberately carries
    /// no <c>item</c> URL — it is the page the visitor is already on, and schema.org marks the
    /// terminal entry that way.
    /// </summary>
    private static SchemaBreadcrumbList? BuildBreadcrumbs(in SchemaContext context)
    {
        var trail = context.Breadcrumbs;
        if (trail.Count < 2)
            return null;

        var items = new List<SchemaBreadcrumbItem>(trail.Count);
        for (var i = 0; i < trail.Count; i++)
        {
            var crumb = trail[i];
            var label = crumb.Text.Value;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var isLast = i == trail.Count - 1;
            var href = isLast || !crumb.Href.HasValue
                ? null
                : Absolute(crumb.Href.Value, context.Origin);

            items.Add(new SchemaBreadcrumbItem(items.Count + 1, label, href));
        }

        return items.Count < 2 ? null : new SchemaBreadcrumbList { Items = items };
    }

    private static SchemaOrganization? BuildOrganization(in SchemaContext context)
    {
        var name = Trim(context.Site.Title);
        if (name is null)
            return null;

        return new SchemaOrganization
        {
            Name = name,
            Url = context.Origin,
            Logo = Absolute(context.Site.LogoUrl, context.Origin),
            SameAs = BuildSameAs(context.Social),
        };
    }

    private static SchemaWebSite? BuildWebSite(in SchemaContext context)
    {
        var name = Trim(context.Site.Title);
        if (name is null)
            return null;

        return new SchemaWebSite
        {
            Name = name,
            Description = Trim(context.Site.MetaDescription),
            Url = context.Origin,
        };
    }

    /// <summary>
    /// Social profiles as <c>sameAs</c>. This is the single highest-value property on an
    /// organization node: it is how a search engine connects a name on a page to an entity it
    /// already knows, instead of inferring one from the string.
    /// </summary>
    /// <summary>
    /// <c>sameAs</c> from the tenant's profiles. Custom links are excluded: <c>sameAs</c> asserts
    /// "this page unambiguously identifies the same organisation", which an official profile does
    /// and an arbitrary link may not — a partner site or a status page would make the claim false.
    /// </summary>
    private static List<string>? BuildSameAs(SocialMediaModel? social)
    {
        if (social is null)
            return null;

        var links = social.All(includeCustom: false).Select(static p => p.Url).ToList();
        return links.Count == 0 ? null : links;
    }

    private static string? Absolute(string? value, string origin)
    {
        var trimmed = Trim(value);
        if (trimmed is null)
            return null;

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return origin.TrimEnd('/') + (trimmed.StartsWith('/') ? trimmed : "/" + trimmed);
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
