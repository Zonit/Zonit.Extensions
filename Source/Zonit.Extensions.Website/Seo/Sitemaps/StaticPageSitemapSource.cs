using System.Runtime.CompilerServices;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Feeds the pages declared with <c>[Sitemap]</c> into the generator, scoped to the areas the
/// active Site actually mounts.
/// </summary>
/// <remarks>
/// <para>Registered by <c>AddWebsite</c>, so a host writes no source for its static pages at all —
/// the three-entry <c>ISitemapSource</c> every project used to hand-write is now the attributes
/// themselves.</para>
///
/// <para>The scoping matters in a host running more than one Site. <see cref="ICurrentSite.Areas"/>
/// names the areas of the mount being served, and each area names its components assembly; only
/// declarations from those assemblies are emitted. A public site and an admin panel in one process
/// therefore publish different sitemaps, which is the whole point of them being separate mounts.
/// The Site's own entry assembly is included too, since a host's own pages carry the attribute
/// just as an area's do.</para>
/// </remarks>
internal sealed class StaticPageSitemapSource(ICurrentSite site) : ISitemapSource
{
    public string Name => "pages";

    /// <summary>
    /// Nothing declared means nothing to publish — and an empty part in the index is a file a
    /// crawler fetches to learn nothing.
    /// </summary>
    public bool IsEnabled => Declarations().Any();

    public async IAsyncEnumerable<SitemapEntry> GetAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var page in Declarations())
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new SitemapEntry(
                page.Path,
                ChangeFrequency: page.Change == ChangeFrequency.Unset ? null : page.Change,
                Priority: page.Priority);
        }

        await Task.CompletedTask;
    }

    private IEnumerable<StaticPage> Declarations()
        => StaticPageRegistry
            .For(Assemblies())
            .Where(static p => p.InSitemap);

    private IEnumerable<System.Reflection.Assembly> Assemblies()
    {
        foreach (var area in site.Areas)
            yield return area.ComponentsAssembly;

        if (System.Reflection.Assembly.GetEntryAssembly() is { } entry)
            yield return entry;
    }
}
