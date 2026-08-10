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
        endpoints.MapGet("/robots.txt", context => options.Robots.Enabled
            ? Write(context, BuildRobots(context, options), "text/plain; charset=utf-8")
            : NotFound(context))
            .AllowAnonymous().ExcludeFromDescription();

        endpoints.MapGet("/llms.txt", context => options.Llms.Enabled
            ? Write(context, BuildLlms(context, options), "text/markdown; charset=utf-8")
            : NotFound(context))
            .AllowAnonymous().ExcludeFromDescription();
    }

    private static Task Write(HttpContext context, string body, string contentType)
    {
        context.Response.ContentType = contentType;
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

        // An agent that reads llms.txt and wants the full URL inventory should not have to guess
        // that a sitemap exists.
        if (options.Sitemap)
            text.Append("\n## Optional\n\n- [Sitemap](")
                .Append(Absolute(IndexingOptions.SitemapPath, origin))
                .Append("): every indexable URL, with per-language alternates.\n");

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
