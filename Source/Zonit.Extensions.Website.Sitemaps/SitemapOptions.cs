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
