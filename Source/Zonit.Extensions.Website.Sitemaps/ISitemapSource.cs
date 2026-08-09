namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// How often a page's content is expected to change. A hint, not a contract — crawlers weigh it
/// against what they observe, and a page that claims <see cref="Hourly"/> and never changes is
/// simply believed less next time.
/// </summary>
public enum ChangeFrequency
{
    /// <summary>Changes on every access.</summary>
    Always,
    /// <summary>Hourly.</summary>
    Hourly,
    /// <summary>Daily.</summary>
    Daily,
    /// <summary>Weekly.</summary>
    Weekly,
    /// <summary>Monthly.</summary>
    Monthly,
    /// <summary>Yearly.</summary>
    Yearly,
    /// <summary>Archived — will not change again.</summary>
    Never,
}

/// <summary>
/// One entry a source contributes to the sitemap.
/// </summary>
/// <param name="Path">
/// Site-relative path <b>without</b> the culture segment and without the mount path base —
/// <c>"/news/first-post"</c>. The package adds the origin, the mount and the language.
/// </param>
/// <param name="LastModified">
/// When the content last changed. Worth supplying: it is the one field crawlers use to decide
/// whether re-fetching is worth their time, and omitting it on a large site means everything gets
/// re-crawled at the same lazy cadence.
/// </param>
/// <param name="ChangeFrequency">Expected update cadence. Omit rather than guessing.</param>
/// <param name="Priority">
/// Relative importance within <em>this site</em>, 0.0–1.0. Has no effect between sites and very
/// little within one; leave it unset unless a section genuinely outranks the rest.
/// </param>
/// <param name="Cultures">
/// Cultures this entry exists in, as BCP-47 tags. <see langword="null"/> — the usual case — means
/// every indexed culture, and the package emits the whole <c>hreflang</c> cluster from the Site's
/// culture policy. Supply a subset for content that is not translated everywhere.
/// </param>
/// <param name="PathsByCulture">
/// Per-culture paths, for content whose <em>slug</em> is translated. Cultures absent from the map
/// use <paramref name="Path"/>. Routes whose static segment is translated need nothing here —
/// the package resolves those from the area's own <c>Routes</c>.
/// </param>
public readonly record struct SitemapEntry(
    string Path,
    DateTimeOffset? LastModified = null,
    ChangeFrequency? ChangeFrequency = null,
    double? Priority = null,
    IReadOnlyList<string>? Cultures = null,
    IReadOnlyDictionary<string, string>? PathsByCulture = null);

/// <summary>
/// Contributes URLs to the sitemap. Implement one per content type, register it, and the package
/// does the rest.
/// </summary>
/// <remarks>
/// <para><b>What you write:</b> a class with your own dependencies injected and one method that
/// returns records. <b>What the package does:</b> resolves the origin and mount, expands every
/// entry across the indexed cultures with correct <c>hreflang</c> alternates, splits the result
/// into files that respect the 50 000-URL and 50 MB limits, generates the index at
/// <c>/sitemap.xml</c>, serves each part at <c>/sitemap/{name}-{n}.xml</c>, and caches the lot.</para>
///
/// <code>
/// internal sealed class NewsSitemap(IArticleRepository articles) : ISitemapSource
/// {
///     public string Name => "news";
///
///     public async IAsyncEnumerable&lt;SitemapEntry&gt; GetAsync(
///         [EnumeratorCancellation] CancellationToken token)
///     {
///         await foreach (var article in articles.StreamPublishedAsync(token))
///             yield return new SitemapEntry(
///                 $"/news/{article.Slug}",
///                 LastModified: article.UpdatedAt,
///                 ChangeFrequency: ChangeFrequency.Weekly);
///     }
/// }
///
/// // registered by the area that owns the content, not by the host:
/// public void ConfigureServices(IServiceCollection services)
///     => services.AddSitemapSource&lt;NewsSitemap&gt;();
/// </code>
///
/// <para><b>Stream, do not materialise.</b> The return type is an async sequence on purpose: a
/// source with two million rows should hand them over as the database yields them, not build a
/// list first. The package consumes the sequence once per generation and writes as it goes.</para>
/// </remarks>
public interface ISitemapSource
{
    /// <summary>
    /// Stable, URL-safe name identifying this source. Becomes part of the file name
    /// (<c>/sitemap/news-1.xml</c>), so it must be unique among registered sources and should not
    /// change once search engines have seen it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this source contributes to the sitemap at all. Lets a plug-in stay registered
    /// while its feature is switched off, instead of the host having to conditionally register it.
    /// </summary>
    bool IsEnabled => true;

    /// <summary>Yields this source's entries. Called once per generation.</summary>
    IAsyncEnumerable<SitemapEntry> GetAsync(CancellationToken cancellationToken);
}
