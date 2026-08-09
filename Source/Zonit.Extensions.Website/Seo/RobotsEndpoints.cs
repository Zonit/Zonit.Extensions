using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website;

/// <summary>
/// Serves the Site's <c>robots.txt</c> and <c>llms.txt</c>, generated from the same configuration
/// the rest of the pipeline obeys.
/// </summary>
internal static class RobotsEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints, SiteOptions site, CultureUrlPolicy policy)
    {
        // The RequestDelegate overload, not the Delegate one: minimal-API parameter binding
        // reflects over the handler signature and is neither trim- nor AOT-safe, and a framework
        // package cannot hand that cost to every consumer for two endpoints that write a string.
        //
        // Both are always mapped and check Enabled per request rather than at startup. The
        // endpoint table is fixed once the host is built, so gating registration would make
        // "Robots.Enabled" the one setting in the section that silently needs a restart.
        endpoints.MapGet("/robots.txt", context =>
        {
            var settings = site.Settings!.Current;
            var robots = site.Robots;
            return robots.Enabled
                ? Write(context, BuildRobots(context, site, settings, policy), "text/plain; charset=utf-8")
                : NotFound(context);
        }).AllowAnonymous().ExcludeFromDescription();

        endpoints.MapGet("/llms.txt", context =>
        {
            var settings = site.Settings!.Current;
            var robots = site.Robots;
            return robots.Llms
                ? Write(context, BuildLlms(context, site, robots), "text/markdown; charset=utf-8")
                : NotFound(context);
        }).AllowAnonymous().ExcludeFromDescription();
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

    private static string BuildRobots(
        HttpContext context, SiteOptions site, SiteSettings settings, CultureUrlPolicy policy)
    {
        var text = new StringBuilder(256);
        text.Append("User-agent: *\n");

        // A Site behind a permission has nothing a crawler can reach and nothing it should
        // advertise. Stating it outright beats relying on the login redirect to look like a
        // dead end — crawlers index redirects too.
        if (!SiteOptions.ResolveIndexable(settings, site.Permission))
        {
            text.Append("Disallow: /\n");
            return text.ToString();
        }

        foreach (var allow in site.Robots.Allowed)
            text.Append("Allow: ").Append(allow).Append('\n');

        // Framework paths first. They serve no page, they are numerous, and a crawler spending
        // its budget on them is budget not spent on content.
        text.Append("Disallow: /_framework/\n");
        text.Append("Disallow: /_blazor\n");

        foreach (var disallow in site.Robots.Disallowed)
            text.Append("Disallow: ").Append(disallow).Append('\n');

        // Languages deliberately kept out of the index. The pages already carry noindex, but a
        // crawl directive saves the fetch that would discover it — at twenty languages that is
        // the difference between one crawl of the site and several.
        if (policy.IsPrefixed)
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

        if (site.Robots.Sitemaps.Count > 0)
        {
            var origin = Origin(context, site);
            text.Append('\n');
            foreach (var sitemap in site.Robots.Sitemaps)
                text.Append("Sitemap: ").Append(Absolute(sitemap, origin)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The <c>llms.txt</c> convention: an H1 with the site name, a blockquote summary, then
    /// sections of annotated links.
    /// </summary>
    private static string BuildLlms(HttpContext context, SiteOptions site, RobotsOptions robots)
    {
        var tenant = context.RequestServices.GetRequiredService<ITenantProvider>();
        var tenantSite = tenant.Settings.Site;
        var origin = Origin(context, site);

        var text = new StringBuilder(256);
        text.Append("# ").Append(tenantSite.Title).Append("\n\n");

        var summary = robots.Summary ?? tenantSite.MetaDescription;
        if (!string.IsNullOrWhiteSpace(summary))
            text.Append("> ").Append(summary.ReplaceLineEndings(" ")).Append("\n\n");

        if (robots.LlmsLinks.Count == 0)
            return text.ToString();

        text.Append("## Resources\n\n");
        foreach (var link in robots.LlmsLinks)
        {
            text.Append("- [").Append(link.Title).Append("](").Append(Absolute(link.Url, origin)).Append(')');
            if (!string.IsNullOrWhiteSpace(link.Description))
                text.Append(": ").Append(link.Description.ReplaceLineEndings(" "));
            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Tenant setting, then the request — the same order the canonical tag uses, so a sitemap
    /// advertised here and a canonical rendered in the page cannot name different hosts.
    /// </summary>
    private static string Origin(HttpContext context, SiteOptions site)
    {
        var tenant = context.RequestServices.GetService<ITenantProvider>();
        var configured = tenant?.Settings.Site.CanonicalUrl;

        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        return $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value}";
    }

    private static string Absolute(string url, string origin)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        return origin.TrimEnd('/') + (url.StartsWith('/') ? url : "/" + url);
    }
}
