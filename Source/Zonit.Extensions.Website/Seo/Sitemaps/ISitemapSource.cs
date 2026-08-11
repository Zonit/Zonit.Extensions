namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// How often a page's content is expected to change. A hint, not a contract — crawlers weigh it
/// against what they observe, and a page that claims <see cref="Hourly"/> and never changes is
/// simply believed less next time.
/// </summary>
public enum ChangeFrequency
{
    /// <summary>
    /// Not stated. The default, and the honest answer for most pages — the element is omitted
    /// rather than filled with a guess.
    /// </summary>
    /// <remarks>
    /// Zero on purpose, so <c>[Seo]</c> and <c>SitemapEntry</c> both default to "say nothing"
    /// without either of them special-casing it.
    /// </remarks>
    Unset = 0,

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
    IReadOnlyDictionary<string, string>? PathsByCulture = null)
{
    /// <summary>
    /// The languages this entry exists in, first, because they scope everything after them.
    /// </summary>
    /// <remarks>
    /// <para>Content whose translations live in a row rather than a resource file is translated
    /// per item and unevenly: a signal published in English and Polish while the German rendition
    /// is still missing exists in two languages. Listing the third sends a crawler to a page that
    /// can only serve another language's text, and — where the sitemap carries the cluster — it
    /// breaks the cluster the other two belong to, because a cluster naming a version that does
    /// not answer is discarded whole.</para>
    ///
    /// <code>
    /// yield return new SitemapEntry(
    ///     signal.Translations.Keys.Select(c => new Culture(c)).ToArray(),
    ///     $"/signals/{day:yyyy-MM-dd}/{signal.Id}",
    ///     LastModified: signal.ClosedAt ?? signal.CreatedAt);
    ///
    /// yield return new SitemapEntry(["en-us", "pl-pl"], "/about/press-kit");
    /// </code>
    ///
    /// <para>Omit the whole overload for content that exists everywhere — that is the common case
    /// and it needs no ceremony. An empty list drops the entry entirely, the honest answer for an
    /// item translated into nothing. Tags outside the Site's indexed set are ignored rather than
    /// trusted, so a stale value in a row cannot conjure a language the Site does not serve.</para>
    ///
    /// <para><see cref="Culture"/> rather than <c>string</c>: <c>PageMeta.Cultures</c> answers the
    /// same question for the rendered page, and the two must be fed from the same place — one
    /// typed and one not is how they drift. The value object converts from a string literal
    /// implicitly, so a collection expression needs no ceremony.</para>
    /// </remarks>
    // Parameter names match the primary constructor's, capital letters and all. They are the
    // record's property names, so a caller writing LastModified: must not have to know which
    // overload it landed on — that difference is invisible at the call site and shows up as an
    // overload-resolution error two arguments away from the cause.
#pragma warning disable IDE1006 // naming rule: parameters are camelCase
    public SitemapEntry(
        IReadOnlyList<Culture> Cultures,
        string Path,
        DateTimeOffset? LastModified = null,
        ChangeFrequency? ChangeFrequency = null,
        double? Priority = null,
        IReadOnlyDictionary<string, string>? PathsByCulture = null)
        : this(Path, LastModified, ChangeFrequency, Priority, PathsByCulture)
    {
        ArgumentNullException.ThrowIfNull(Cultures);
        this.Cultures = Cultures;
    }
#pragma warning restore IDE1006

    /// <summary>
    /// Languages this entry exists in, or <see langword="null"/> for every indexed language.
    /// </summary>
    /// <remarks>
    /// Deliberately not a positional parameter and settable only through the constructor above.
    /// As a trailing optional it read as one more attribute of the entry, alongside
    /// <see cref="Priority"/>; it is not — it decides which files the entry appears in at all, and
    /// a scope that can be appended as an afterthought is one that gets forgotten.
    /// </remarks>
    public IReadOnlyList<Culture>? Cultures { get; private init; }

    /// <summary>Whether this entry should be listed for <paramref name="culture"/>.</summary>
    internal bool Exists(string culture)
    {
        if (Cultures is not { } declared)
            return true;

        foreach (var candidate in declared)
        {
            if (string.Equals(candidate.Value, culture, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

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
