using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Configuration for sitemap generation.
/// </summary>
/// <remarks>
/// There is no list of sources here. A map is registered where it belongs — in the
/// <c>IWebsiteServices.ConfigureServices</c> of the area that owns the content — and the generator
/// receives every registration as an injected sequence. A host that installs a plug-in therefore
/// gets that plug-in's URLs in the sitemap without naming them, and removing the plug-in removes
/// its URLs, which is exactly what a registration list fails to do the first time someone
/// forgets to edit it.
/// </remarks>
public sealed class SitemapOptions
{
    /// <summary>
    /// Hard ceiling on URLs per file. The protocol's own limit is 50 000; the default leaves
    /// headroom so a file cannot be rejected for being one entry over after a late addition.
    /// </summary>
    /// <remarks>
    /// The other limit — 50 MB uncompressed — is enforced separately and independently, because
    /// with twenty <c>hreflang</c> alternates per URL the byte limit is reached at roughly a fifth
    /// of the URL limit. Whichever binds first ends the file.
    /// </remarks>
    public int MaxUrlsPerFile { get; set; } = 45_000;

    /// <summary>
    /// Hard ceiling on the uncompressed size of one file, in bytes. Protocol limit is 50 MB; the
    /// default keeps a margin.
    /// </summary>
    public int MaxBytesPerFile { get; set; } = 45 * 1024 * 1024;

    /// <summary>
    /// How long a generated sitemap is served before being rebuilt.
    /// </summary>
    /// <remarks>
    /// Generation walks every source, which for a large site means real database work. Crawlers
    /// fetch the sitemap far more often than the content behind it changes, and an uncached
    /// endpoint is a free denial-of-service primitive — anyone can ask for it in a loop. An hour
    /// is well inside the interval any crawler actually re-reads at.
    /// </remarks>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// One file per language on a prefixed Site — <c>/sitemap/news-pl-1.xml</c> — instead of one
    /// stream carrying every language. Ignored where the Site does not encode culture in its paths.
    /// </summary>
    /// <remarks>
    /// <para><b>What it buys.</b> Search Console reports index coverage <em>per submitted file</em>.
    /// One combined sitemap answers "20 000 URLs, 14 000 indexed", which names no problem; split by
    /// language it answers "de 400/400, pl 380/400, bg 12/400", which names one. On a site aiming at
    /// twenty markets that is the difference between having the diagnostic and not.</para>
    ///
    /// <para>It also makes file identity stable. Ungrouped, adding one page shifts every later
    /// entry across part boundaries, so every part's contents change and a crawler re-reads the
    /// whole set. Grouped, a page added in Polish rewrites the Polish parts only.</para>
    ///
    /// <para>A language a source contributes nothing to simply produces no file, so a partially
    /// translated source lists what exists and nothing else — see
    /// <see cref="SitemapEntry.Cultures"/>.</para>
    ///
    /// <para><b>Cost.</b> One writer stays open per language while the source streams, so peak
    /// memory is roughly <c>languages × the current part</c> rather than one part. Parts flush at
    /// <see cref="MaxBytesPerFile"/>; a very large multilingual source should lower that limit
    /// rather than raise it.</para>
    /// </remarks>
    public bool GroupByCulture { get; set; } = true;

    /// <summary>
    /// Emit the <c>hreflang</c> cluster as <c>xhtml:link</c> elements inside the sitemap. Off by
    /// default, because the rendered page already carries the same cluster.
    /// </summary>
    /// <remarks>
    /// <para><b>Why off.</b> Sitemap and HTML are alternative ways to declare the same thing, and
    /// this package emits the HTML form on every indexable page — complete, reciprocal by
    /// construction (one policy generates every page's cluster, so no page can disagree with
    /// another), and including <c>x-default</c>, which the sitemap form here does not. Declaring it
    /// twice adds no signal and costs a square: each of N languages gets its own <c>&lt;url&gt;</c>
    /// carrying N alternates. Measured on this generator, 1 000 pages in 20 languages is 400 000
    /// link elements and about 38 MB — against a protocol ceiling of 50 MB. The same sitemap
    /// without them is 1.8 MB.</para>
    ///
    /// <para><b>When to turn it on.</b> When the HTML cluster is not there to be found: a Site
    /// whose shell was replaced with one that does not render <c>PageHead</c>, or content a crawler
    /// is unlikely to fetch soon enough for the cluster to be discovered from the page itself.</para>
    /// </remarks>
    public bool Alternates { get; set; }
}

/// <summary>
/// Registers a sitemap source from inside an area's <c>ConfigureServices</c>.
/// </summary>
public static class SitemapSourceRegistration
{
    /// <summary>
    /// Adds <typeparamref name="TSource"/> to the sitemap. Scoped, so it injects repositories and
    /// units of work exactly as a page would.
    /// </summary>
    /// <remarks>
    /// <code>
    /// public sealed class NewsArea : IWebsiteArea, IWebsiteServices
    /// {
    ///     public string Key => "news";
    ///
    ///     public void ConfigureServices(IServiceCollection services)
    ///         => services.AddSitemapSource&lt;NewsSitemap&gt;();
    /// }
    /// </code>
    ///
    /// <para><c>Add</c>, not <c>TryAdd</c>: several maps per area is normal — pages, articles,
    /// categories — and <c>TryAdd</c> on the <c>ISitemapSource</c> contract would keep only the
    /// first and silently drop the rest.</para>
    /// </remarks>
    public static IServiceCollection AddSitemapSource<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSource>(
        this IServiceCollection services)
        where TSource : class, ISitemapSource
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ISitemapSource, TSource>();
        return services;
    }
}
