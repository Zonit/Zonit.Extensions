using Zonit.Extensions.Website.Sitemaps;

namespace Zonit.Extensions.Website;

/// <summary>
/// Publishes this page in the sitemap. Collected at build time by the source generator — nothing
/// scans, reflects or allocates at run time.
/// </summary>
/// <remarks>
/// <para><b>Opt-in, deliberately.</b> A page appears in the sitemap only because someone wrote
/// this attribute. The reverse default — every routable page published unless it objects — reads
/// as safer and is not: the failure mode is that a page written in a hurry, or copied from
/// another, is <em>advertised to search engines</em> before anyone decided it should be public.
/// An internal tool, a half-finished feature, a page whose URL was meant to be shared with three
/// people. Forgetting the attribute costs a page its listing, which is recoverable and visible in
/// Search Console; forgetting to remove it publishes something. One of those mistakes is worth
/// making cheaply.</para>
///
/// <para><b>The route comes from <c>@page</c>.</b> The generator reads both directives out of the
/// same file, so there is nothing to keep in step and no path stated twice. Give
/// <see cref="Path"/> only when the route cannot be seen there — a route declared in C# on a
/// component whose template lives elsewhere.</para>
///
/// <para><b>Parameterised routes are not listed.</b> <c>/signals/{slug}</c> has no single URL, so
/// this attribute cannot describe it and the generator says so at build time rather than emitting
/// a template into the XML. Those pages belong to an <see cref="ISitemapSource"/>, which is the
/// only thing that can enumerate the slugs.</para>
///
/// <code>
/// @page "/ebook"
/// @attribute [WebsiteSitemap(Change = ChangeFrequency.Monthly, Priority = 0.8)]
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WebsiteSitemapAttribute : Attribute
{
    /// <summary>Publishes the page in every indexed language, under the route from <c>@page</c>.</summary>
    public WebsiteSitemapAttribute()
    {
    }

    /// <summary>Publishes the page under an explicitly stated path.</summary>
    /// <param name="path">
    /// Site-relative path with no culture segment and no mount base — <c>"/ebook"</c>. The
    /// framework adds the origin, the mount and the language.
    /// </param>
    public WebsiteSitemapAttribute(string path) => Path = path;

    /// <summary>
    /// Publishes the page only in the languages its content actually exists in.
    /// </summary>
    /// <param name="cultures">
    /// BCP-47 tags — <c>["en-us", "pl-pl"]</c>. Validated at build time; an unknown tag is
    /// <c>ZONITSM0003</c>, not a silent miss.
    /// </param>
    public WebsiteSitemapAttribute(string[] cultures) => Cultures = cultures;

    /// <summary>Both, for a page that states its path and its languages.</summary>
    /// <param name="cultures">BCP-47 tags.</param>
    /// <param name="path">Site-relative path, no culture segment, no mount base.</param>
    public WebsiteSitemapAttribute(string[] cultures, string path)
    {
        Cultures = cultures;
        Path = path;
    }

    /// <summary>Explicit path, when the route is not visible next to the attribute.</summary>
    public string? Path { get; }

    /// <summary>
    /// Languages this page's content exists in. <see langword="null"/> — the usual case — means
    /// every indexed language.
    /// </summary>
    /// <remarks>
    /// <para><b>One declaration, three effects.</b> It narrows the sitemap, and it drives the page
    /// itself: rendering in a language outside the set is <c>noindex, follow</c>, and the
    /// <c>hreflang</c> cluster on the versions that do exist lists only those. A cluster naming a
    /// version that answers <c>noindex</c> is discarded whole, so the two halves have to agree —
    /// which is exactly why they come from one place.</para>
    ///
    /// <para><b><c>string[]</c>, not <c>Culture[]</c>.</b> Attribute arguments must be compile-time
    /// constants, and the language allows only primitives, <c>string</c>, <c>Type</c>, enums and
    /// arrays of those — a value object is <c>CS0181</c>. The validation the type would have given
    /// is done by the generator instead, which is strictly better: a squiggle at build time rather
    /// than an exception at start-up.</para>
    ///
    /// <para>This is the declaration for a <b>static</b> page. Content whose translations arrive
    /// per row is not known at compile time; that is <c>PageMeta.Cultures</c> for the page and
    /// <c>SitemapEntry.Cultures</c> for the map, and setting <c>PageMeta.Cultures</c> overrides
    /// whatever is written here.</para>
    /// </remarks>
    public string[]? Cultures { get; }

    /// <summary>
    /// When this page's content last changed, as <c>yyyy-MM-dd</c> or a full ISO-8601 instant.
    /// </summary>
    /// <remarks>
    /// <para>Editorial, and stated by hand on purpose. <c>lastmod</c> is the one field a crawler
    /// uses to decide whether re-fetching is worth its time, which is exactly why it must not be
    /// guessed: the build date would mark every untouched page as fresh on every deployment, and a
    /// sitemap whose dates do not survive contact with reality is one a search engine stops
    /// believing — for every URL in it, not just the wrong ones.</para>
    ///
    /// <para>So it means what it says: the day the terms were revised, the day the guide was
    /// rewritten. Leave it unset and the element is simply omitted, which is honest. Validated at
    /// build time — an unparsable value is <c>ZONITSM0004</c>.</para>
    /// </remarks>
    public string? LastModified { get; set; }

    /// <summary>
    /// Expected update cadence. Leave unset rather than guessing — a page that claims
    /// <see cref="ChangeFrequency.Hourly"/> and never changes is simply believed less next time.
    /// </summary>
    public ChangeFrequency Change { get; set; } = ChangeFrequency.Unset;

    /// <summary>
    /// Relative importance within <em>this</em> site, 0.0–1.0. Has no effect between sites and
    /// very little within one; set it only where a section genuinely outranks the rest.
    /// </summary>
    public double Priority { get; set; } = double.NaN;
}

/// <summary>
/// Lists this page in <c>llms.txt</c>, the site map written for AI agents rather than crawlers.
/// </summary>
/// <remarks>
/// <para>Separate from <see cref="WebsiteSitemapAttribute"/> because the two answer different questions.
/// A sitemap is an inventory — everything worth crawling, however many thousands. <c>llms.txt</c>
/// is a briefing: the handful of pages that explain what this site is and where its real answers
/// live. Publishing all of one into the other serves neither, so a page opts into each on its own,
/// and most pages want only the first.</para>
///
/// <para><b>Write the description about <em>when</em> to read the page.</b> An agent chooses a
/// source by its description, not its name. "Settled outcomes for every signal — the source for
/// hit-rate questions" earns a read; "Signals page" does not.</para>
///
/// <para>The text lives in the attribute rather than being lifted from <c>PageMeta</c> because
/// <c>llms.txt</c> is assembled without rendering anything, and a page's metadata is computed per
/// request — it can depend on route parameters and on data. What the attribute states is true of
/// the page itself, which is what belongs in a static index of the site.</para>
///
/// <code>
/// @page "/signals"
/// @attribute [WebsiteSitemap(Change = ChangeFrequency.Daily)]
/// @attribute [WebsiteLlms("Settled outcomes for every signal — the source for hit-rate questions.")]
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WebsiteLlmsAttribute : Attribute
{
    /// <param name="description">One line on what this page is and when it is worth reading.</param>
    public WebsiteLlmsAttribute(string description) => Description = description;

    /// <summary>One line on what this page is and when it is worth reading.</summary>
    public string Description { get; }

    /// <summary>
    /// Heading this entry is grouped under in <c>llms.txt</c>. Defaults to <c>Resources</c>.
    /// </summary>
    /// <remarks>
    /// The convention's <c>## Optional</c> section marks material an agent may skip when short of
    /// context; anything else becomes its own section, in first-seen order.
    /// </remarks>
    public string? Section { get; set; }

    /// <summary>
    /// Title shown in the file. Defaults to the page's route, which is rarely what you want —
    /// set it.
    /// </summary>
    public string? Title { get; set; }
}
