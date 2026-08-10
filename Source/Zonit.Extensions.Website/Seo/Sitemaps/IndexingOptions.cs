namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// One entry in <c>llms.txt</c> — a resource an AI agent should read to understand the site.
/// </summary>
/// <param name="Title">Human-readable name.</param>
/// <param name="Url">Absolute URL, or a site-relative path resolved against the canonical origin.</param>
/// <param name="Description">
/// One line on what it is and <b>when it is the right thing to read</b>. An agent picks a source by
/// its description, not by its name, so "Full signal history with settled outcomes — the source for
/// questions about hit rate" earns a read where "Signals page" does not.
/// </param>
public readonly record struct LlmsLink(string Title, string Url, string? Description = null);

/// <summary>
/// Contents of <c>llms.txt</c> — the site described for an AI agent rather than a crawler.
/// </summary>
/// <remarks>
/// <para>Off until the first <see cref="AddLink"/>. A file whose only content is the site title
/// tells an agent nothing it could not read from the page, and serving an empty one is worse than
/// serving none: it looks authoritative and says nothing.</para>
/// </remarks>
public sealed class LlmsOptions
{
    /// <summary>Whether <c>/llms.txt</c> is served. Set automatically by <see cref="AddLink"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Prose answer to "what is this site and what is it for", rendered as the blockquote under the
    /// title. Falls back to the tenant's meta description.
    /// </summary>
    /// <remarks>
    /// Write it for a reader who has never seen the product and has to decide in one paragraph
    /// whether this site can answer their question. A marketing tagline fails that test.
    /// </remarks>
    public string? Summary { get; set; }

    /// <summary>Curated resources listed under <c>## Resources</c>.</summary>
    public List<LlmsLink> Links { get; set; } = [];

    /// <summary>Adds a resource and enables the endpoint.</summary>
    public LlmsOptions AddLink(string title, string url, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Links.Add(new LlmsLink(title, url, description));
        Enabled = true;
        return this;
    }

    internal LlmsOptions Clone() => new()
    {
        Enabled = Enabled,
        Summary = Summary,
        Links = [.. Links],
    };
}

/// <summary>
/// Everything a Site tells crawlers and agents about what to fetch and what to skip:
/// <c>robots.txt</c>, <c>sitemap.xml</c> and <c>llms.txt</c>.
/// </summary>
/// <remarks>
/// <para><b>Why one tree.</b> The three files are one statement split across three formats, and
/// they only work if they agree. <c>robots.txt</c> has to advertise the sitemap's real address;
/// the sitemap must not list a path <c>robots.txt</c> disallows; both have to hold the same opinion
/// about which languages are indexed. Configured separately, that agreement is a convention someone
/// maintains by hand — and the failure is silent, because a <c>robots.txt</c> naming a sitemap that
/// moved is a valid file that crawlers simply believe.</para>
///
/// <para>Declaring them together means the framework can derive what it already knows.
/// <c>Sitemap:</c> lines are filled in from the sitemap endpoint this same call registers; the
/// disallow list for withheld languages comes from the Site's culture policy; a Site behind a
/// permission is closed in <c>robots.txt</c> without anyone restating it.</para>
///
/// <code>
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
/// </remarks>
public sealed class IndexingOptions
{
    /// <summary>Generated <c>robots.txt</c>.</summary>
    public RobotsOptions Robots { get; } = new();

    /// <summary>Generated <c>llms.txt</c>.</summary>
    public LlmsOptions Llms { get; } = new();

    /// <summary>
    /// Whether this Site maps <c>/sitemap.xml</c> (the index) and <c>/sitemap/{name}.xml</c>
    /// (the parts), and advertises the index in <c>robots.txt</c>.
    /// </summary>
    /// <remarks>
    /// A Site with no <see cref="ISitemapSource"/> registered still gets a valid, empty index —
    /// which is correct, and cheaper to explain than a 404 on an address <c>robots.txt</c> names.
    /// Turn it off for a mount that should publish crawl directives but no URL inventory: a panel
    /// publishing a sitemap would advertise exactly the URLs it is trying to keep out of search.
    /// </remarks>
    public bool Sitemap { get; set; } = true;

    /// <summary>
    /// Extra sitemap addresses to advertise in <c>robots.txt</c> — a legacy file, or one produced
    /// outside this framework. The index above is added automatically and does not belong here.
    /// </summary>
    public List<string> AdditionalSitemaps { get; } = [];

    /// <summary>Path of the generated index. Fixed by the endpoint mapping.</summary>
    internal const string SitemapPath = "/sitemap.xml";
}
