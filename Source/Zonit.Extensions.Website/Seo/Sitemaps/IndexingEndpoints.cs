using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Zonit.Extensions.Tenants;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Serves the Site's <c>robots.txt</c> and <c>llms.txt</c>, generated from the same configuration
/// the rest of the pipeline obeys.
/// </summary>
internal static class IndexingEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints, IndexingOptions options)
    {
        // The RequestDelegate overload, not the Delegate one: minimal-API parameter binding
        // reflects over the handler signature and is neither trim- nor AOT-safe, and a framework
        // package cannot hand that cost to every consumer for two endpoints that write a string.
        //
        // Both are always mapped and check Enabled per request rather than at startup. The
        // endpoint table is fixed once the host is built, so gating registration would make
        // "Enabled" the one setting in the section that silently needs a restart.
        //
        // Nothing here guards against a language prefix. It cannot arrive: these paths carry a
        // skipped extension, so CultureMiddleware answers /pl/robots.txt with 404 before routing
        // ever runs. One rule, in one place, for every file the Site serves.
        //
        // GET and HEAD both, explicitly — MapGet registers GET alone, and an unmatched HEAD does
        // not become 405, it falls off the endpoint table entirely and reads as "this site has no
        // robots.txt" to every link checker and uptime probe that HEADs before it GETs. Kestrel
        // discards the body on a HEAD by itself; the handlers need no second code path.
        endpoints.MapMethods("/robots.txt", GetAndHead, context => options.Robots.Enabled
            ? Write(context, BuildRobots(context, options), "text/plain; charset=utf-8")
            : NotFound(context))
            .AllowAnonymous().ExcludeFromDescription();

        // Enabled is not the whole story: a page carrying [WebsiteLlms] is content declared for
        // this file just as much as an AddLink call is, and the flag only ever tracked the latter.
        // Gating on it alone made a Site whose pages all declare themselves — the shape the
        // attribute exists to encourage — answer 404 on a file it had plenty to say in.
        endpoints.MapMethods("/llms.txt", GetAndHead, context => HasLlmsContent(context, options)
            ? Write(context, BuildLlms(context, options), "text/markdown; charset=utf-8")
            : NotFound(context))
            .AllowAnonymous().ExcludeFromDescription();
    }

    internal static readonly string[] GetAndHead = ["GET", "HEAD"];

    private static bool HasLlmsContent(HttpContext context, IndexingOptions options)
        => options.Llms.Enabled
        || StaticPageRegistry.For(Assemblies(context)).Any(static p => p.LlmsDescription is not null);

    private static Task Write(HttpContext context, string body, string contentType)
    {
        context.Response.ContentType = contentType;

        // Generation is a StringBuilder over an in-memory registry, so there is nothing to cache
        // server-side. The header is for the FETCHER: llms.txt is pulled on demand by agent
        // tooling — an IDE assistant, an MCP server — which may re-read it per session or per
        // question, and this is the only way to tell it not to. One hour matches the sitemap's own
        // regeneration window, so the two never disagree about how fresh the site claims to be.
        context.Response.Headers.CacheControl = "public, max-age=3600";
        return context.Response.WriteAsync(body, context.RequestAborted);
    }

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    private static string BuildRobots(HttpContext context, IndexingOptions options)
    {
        var site = context.RequestServices.GetRequiredService<ICurrentSite>();
        var robots = options.Robots;

        var text = new StringBuilder(256);
        text.Append("User-agent: *\n");

        // A Site behind a permission has nothing a crawler can reach and nothing it should
        // advertise. Stating it outright beats relying on the login redirect to look like a
        // dead end — crawlers index redirects too.
        if (!site.Indexable)
        {
            text.Append("Disallow: /\n");
            return text.ToString();
        }

        foreach (var allow in robots.Allowed)
            text.Append("Allow: ").Append(allow).Append('\n');

        // Framework paths first. They serve no page, they are numerous, and a crawler spending
        // its budget on them is budget not spent on content.
        text.Append("Disallow: /_framework/\n");
        text.Append("Disallow: /_blazor\n");

        foreach (var disallow in robots.Disallowed)
            text.Append("Disallow: ").Append(disallow).Append('\n');

        // Languages deliberately kept out of the index. The pages already carry noindex, but a
        // crawl directive saves the fetch that would discover it — at twenty languages that is
        // the difference between one crawl of the site and several.
        if (site.UrlPolicy is { IsPrefixed: true } policy)
        {
            foreach (var culture in policy.Cultures)
            {
                if (policy.IsIndexed(culture))
                    continue;

                var segment = policy.SegmentFor(culture);
                if (segment is not null)
                    text.Append("Disallow: /").Append(segment).Append("/\n");
            }
        }

        // The sitemap address is not restated by the host — it is the path this same Indexing()
        // call mapped. That is the whole point of the two living in one options tree: a robots.txt
        // naming a sitemap that moved is a valid file, so the mistake never surfaces as an error.
        var sitemaps = Sitemaps(options);
        if (sitemaps.Count > 0)
        {
            var origin = Origin(context);
            text.Append('\n');
            foreach (var sitemap in sitemaps)
                text.Append("Sitemap: ").Append(Absolute(sitemap, origin)).Append('\n');
        }

        return text.ToString();
    }

    private static List<string> Sitemaps(IndexingOptions options)
    {
        var result = new List<string>(options.AdditionalSitemaps.Count + 1);
        if (options.Sitemap)
            result.Add(IndexingOptions.SitemapPath);
        result.AddRange(options.AdditionalSitemaps);
        return result;
    }

    /// <summary>
    /// The <c>llms.txt</c> convention: an H1 with the site name, a blockquote summary, then
    /// sections of annotated links.
    /// </summary>
    private static string BuildLlms(HttpContext context, IndexingOptions options)
    {
        var tenant = context.RequestServices.GetRequiredService<ITenantProvider>();
        var tenantSite = tenant.Settings.Site;
        var llms = options.Llms;
        var origin = Origin(context);

        var text = new StringBuilder(256);
        text.Append("# ").Append(tenantSite.Title).Append("\n\n");

        // Site.About before Site.MetaDescription: the meta description is a 160-character snippet
        // written to earn a click, which is the wrong text for an agent deciding whether this site
        // can answer a question at all.
        var summary = llms.Summary ?? Coalesce(tenantSite.About, tenantSite.MetaDescription);
        if (!string.IsNullOrWhiteSpace(summary))
            text.Append("> ").Append(summary.ReplaceLineEndings(" ")).Append("\n\n");

        // Which languages exist, and how to ask for one. An agent cannot work this out by reading a
        // page: the hreflang cluster sits in the head of pages it has not fetched, and the prefix
        // shape is a policy rather than a link. One line saves it guessing or crawling to find out.
        var site = context.RequestServices.GetRequiredService<ICurrentSite>();
        if (site.UrlPolicy is { IsPrefixed: true } policy)
        {
            var segments = policy.IndexedCultures
                .Select(policy.SegmentFor)
                .Where(static segment => segment is not null)
                .ToArray();

            if (segments.Length > 1)
                text.Append("Available in ").Append(segments.Length)
                    .Append(" languages — prefix any path with the language segment: ")
                    .Append(string.Join(", ", segments.Select(static s => "/" + s + "/")))
                    .Append(". An unprefixed path serves the reader's own language.\n\n");
        }

        // Two inputs, one list. Pages carrying [Llms] arrive from the build-time registry, scoped
        // to the areas this Site mounts; x.Llms.AddLink(...) covers what no page can declare — an
        // external doc, a downloadable dataset, a section that is not a single route. Declared
        // links come first within a section: the host describes the site, a page only itself.
        var sections = new List<(string Name, List<LlmsLink> Items)>();

        foreach (var link in llms.Links)
            Section(sections, "Resources").Add(link);

        foreach (var page in StaticPageRegistry.For(Assemblies(context)))
        {
            if (page.LlmsDescription is null)
                continue;

            Section(sections, page.LlmsSection ?? "Resources")
                .Add(new LlmsLink(page.LlmsTitle ?? page.Path, page.Path, page.LlmsDescription));
        }

        foreach (var (name, items) in sections)
        {
            text.Append("## ").Append(name).Append("\n\n");
            foreach (var link in items)
            {
                text.Append("- [").Append(link.Title).Append("](").Append(Absolute(link.Url, origin)).Append(')');
                if (!string.IsNullOrWhiteSpace(link.Description))
                    text.Append(": ").Append(link.Description!.ReplaceLineEndings(" "));
                text.Append('\n');
            }
            text.Append('\n');
        }

        // "## Optional" is the convention's marker for material an agent may skip when short of
        // context. Both entries below belong there: neither answers a question about the product,
        // and both are worth having when the question is about identity or coverage.
        var optional = new StringBuilder(128);

        if (options.Sitemap)
            optional.Append("- [Sitemap](")
                .Append(Absolute(IndexingOptions.SitemapPath, origin))
                .Append("): every indexable URL, with per-language alternates.\n");

        // Social profiles, custom entries included — this is the one place they are described
        // rather than asserted as identity, so a "Facebook group" or a community forum can appear
        // without becoming a schema.org sameAs claim.
        foreach (var (label, url) in tenant.Settings.SocialMedia?.All() ?? [])
            optional.Append("- [").Append(label).Append("](").Append(url).Append(")\n");

        if (optional.Length > 0)
            text.Append("## Optional\n\n").Append(optional);

        return text.ToString();
    }

    /// <summary>
    /// Tenant setting, then the request — the same order the canonical tag uses, so a sitemap
    /// advertised here and a canonical rendered in the page cannot name different hosts.
    /// </summary>
    private static string Origin(HttpContext context)
    {
        var tenant = context.RequestServices.GetService<ITenantProvider>();
        var configured = tenant?.Settings.Site.CanonicalUrl;

        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        return $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value}";
    }

    private static List<LlmsLink> Section(
        List<(string Name, List<LlmsLink> Items)> sections, string name)
    {
        foreach (var section in sections)
        {
            if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                return section.Items;
        }

        var items = new List<LlmsLink>();
        sections.Add((name, items));
        return items;
    }

    /// <summary>
    /// Assemblies whose page declarations belong to the Site being served — its mounted areas plus
    /// the host itself. Without the filter, a process running a public site and an admin panel
    /// would brief an agent about both from either address.
    /// </summary>
    private static IEnumerable<System.Reflection.Assembly> Assemblies(HttpContext context)
    {
        var site = context.RequestServices.GetRequiredService<ICurrentSite>();

        foreach (var area in site.Areas)
            yield return area.ComponentsAssembly;

        if (System.Reflection.Assembly.GetEntryAssembly() is { } entry)
            yield return entry;
    }

    private static string? Coalesce(string? first, string? second)
        => string.IsNullOrWhiteSpace(first) ? second : first;

    private static string Absolute(string url, string origin)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        return origin.TrimEnd('/') + (url.StartsWith('/') ? url : "/" + url);
    }
}
