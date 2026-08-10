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
    /// <summary>Publishes the page under the route declared by <c>@page</c>.</summary>
    public WebsiteSitemapAttribute()
    {
    }

    /// <summary>Publishes the page under an explicitly stated path.</summary>
    /// <param name="path">
    /// Site-relative path with no culture segment and no mount base — <c>"/ebook"</c>. The
    /// framework adds the origin, the mount and the language.
    /// </param>
    public WebsiteSitemapAttribute(string path) => Path = path;

    /// <summary>Explicit path, when the route is not visible next to the attribute.</summary>
    public string? Path { get; }

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
