namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Mounts the machine-readable descriptors — <c>robots.txt</c>, <c>sitemap.xml</c>,
/// <c>llms.txt</c> — on a Site.
/// </summary>
public static class IndexingExtensions
{
    /// <summary>
    /// Registers all three endpoints inside this Site's branch, so each inherits its path base and
    /// its culture policy.
    /// </summary>
    /// <remarks>
    /// <para>Per Site, not global: which mounts publish crawl directives is a per-mount decision,
    /// and a panel publishing a sitemap would advertise exactly the URLs it is trying to keep out
    /// of search. A Site that never calls this serves none of the three.</para>
    ///
    /// <code>
    /// builder.Services.AddSitemap();               // generator + cache, once per process
    ///
    /// app.UseWebsite("/", o =>
    /// {
    ///     o.Indexing(x =>
    ///     {
    ///         x.Robots.Disallow("/search");
    ///         x.Llms.Summary = "Commodity, forex and macro signals with a public track record.";
    ///         x.Llms.AddLink("Signal history", "/signals", "Settled outcomes — the hit-rate source.");
    ///     });
    /// });
    /// </code>
    ///
    /// <para>Sitemap <em>content</em> is not configured here. Each area registers its own
    /// <see cref="ISitemapSource"/> in <c>ConfigureServices</c>, one per content type, and each
    /// becomes its own file under the index — so installing a plug-in adds its URLs and removing
    /// it removes them, with no list in the host to keep in step.</para>
    /// </remarks>
    public static SiteOptions Indexing(this SiteOptions site, Action<IndexingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(site);

        var options = new IndexingOptions();
        configure?.Invoke(options);

        return site.MapEndpoints(endpoints =>
        {
            IndexingEndpoints.Map(endpoints, options);

            if (options.Sitemap)
                endpoints.MapSitemap();
        });
    }
}
