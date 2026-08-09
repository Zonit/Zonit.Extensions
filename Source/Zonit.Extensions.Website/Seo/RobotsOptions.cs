namespace Zonit.Extensions.Website;

/// <summary>
/// One entry in <c>llms.txt</c> — a resource an AI agent should read to understand the site.
/// </summary>
/// <param name="Title">Human-readable name.</param>
/// <param name="Url">Absolute URL, or a site-relative path resolved against the canonical origin.</param>
/// <param name="Description">One line on what it is and when it is the right thing to read.</param>
public readonly record struct LlmsLink(string Title, string Url, string? Description = null);

/// <summary>
/// Machine-readable site descriptors served at the Site root: <c>/robots.txt</c> and
/// <c>/llms.txt</c>.
/// </summary>
/// <remarks>
/// <para>Generated rather than shipped as static files, because both of them have to agree with
/// configuration that lives in code. A <c>robots.txt</c> committed to <c>wwwroot</c> cannot know
/// that a Site requires a permission, or that three of twenty languages are deliberately kept
/// out of the index — and the moment those drift, the file is confidently wrong in a way nobody
/// notices until pages appear in search that should not have.</para>
///
/// <para>Both are served from the Site's own branch, so a mount at <c>/admin</c> answers at
/// <c>/admin/robots.txt</c>. That is intentional: the file is about the Site, and the root
/// <c>/robots.txt</c> is the only one crawlers read for the host. Mount the public Site at
/// <c>/</c> and its file lands where it counts, while a panel's copy is available for tooling
/// without pretending to speak for the domain.</para>
/// </remarks>
public sealed class RobotsOptions
{
    /// <summary>Whether <c>/robots.txt</c> is served by the framework.</summary>
    /// <remarks>Turn off to serve a hand-written file from <c>wwwroot</c> instead.</remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether <c>/llms.txt</c> is served.</summary>
    /// <remarks>
    /// Off by default: an <c>llms.txt</c> whose only content is the site title tells an agent
    /// nothing it could not read from the page. Turn it on once there is a
    /// <see cref="Summary"/> and a curated set of <see cref="LlmsLinks"/> worth pointing at.
    /// </remarks>
    public bool Llms { get; set; }

    /// <summary>
    /// Prose summary for <c>llms.txt</c>. Falls back to the tenant's meta description.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>Paths disallowed for all user agents. Framework paths are added automatically.</summary>
    /// <remarks>
    /// Settable so configuration can bind it. A <c>Disallow</c> array in the <c>Website</c>
    /// section <b>replaces</b> what the code declared rather than extending it, matching how every
    /// other collection in this framework binds — see <c>SiteSettingsProvider</c>.
    /// </remarks>
    public List<string> Disallowed { get; set; } = [];

    /// <summary>Paths explicitly allowed — used to carve exceptions out of a broader disallow.</summary>
    public List<string> Allowed { get; set; } = [];

    /// <summary>Sitemap URLs advertised at the end of the file. Relative paths resolve against the canonical origin.</summary>
    public List<string> Sitemaps { get; set; } = [];

    /// <summary>Curated resources listed in <c>llms.txt</c>.</summary>
    public List<LlmsLink> LlmsLinks { get; set; } = [];

    /// <summary>Disallows a path prefix for all user agents.</summary>
    public RobotsOptions Disallow(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Disallowed.Add(path);
        return this;
    }

    /// <summary>Allows a path prefix, overriding a broader <see cref="Disallow"/>.</summary>
    public RobotsOptions Allow(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Allowed.Add(path);
        return this;
    }

    /// <summary>Advertises a sitemap.</summary>
    public RobotsOptions Sitemap(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Sitemaps.Add(url);
        return this;
    }

    /// <summary>Adds a resource to <c>llms.txt</c> and enables the endpoint.</summary>
    public RobotsOptions AddLlmsLink(string title, string url, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        LlmsLinks.Add(new LlmsLink(title, url, description));
        Llms = true;
        return this;
    }

    internal RobotsOptions Clone() => new()
    {
        Enabled = Enabled,
        Llms = Llms,
        Summary = Summary,
        Disallowed = [.. Disallowed],
        Allowed = [.. Allowed],
        Sitemaps = [.. Sitemaps],
        LlmsLinks = [.. LlmsLinks],
    };
}
