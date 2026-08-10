namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Generated <c>robots.txt</c> — crawl directives for the Site.
/// </summary>
/// <remarks>
/// <para>Generated rather than shipped as a static file, because it has to agree with
/// configuration that lives in code. A <c>robots.txt</c> committed to <c>wwwroot</c> cannot know
/// that a Site requires a permission, or that three of twenty languages are deliberately kept out
/// of the index, or where the sitemap ended up — and the moment those drift, the file is
/// confidently wrong in a way nobody notices until pages appear in search that should not have.</para>
///
/// <para>Served from the Site's own branch, so a mount at <c>/admin</c> answers at
/// <c>/admin/robots.txt</c>. That is intentional: the file is about the Site, and the root
/// <c>/robots.txt</c> is the only one crawlers read for the host. Mount the public Site at
/// <c>/</c> and its file lands where it counts, while a panel's copy stays available to tooling
/// without pretending to speak for the domain.</para>
/// </remarks>
public sealed class RobotsOptions
{
    /// <summary>Whether <c>/robots.txt</c> is served by the framework.</summary>
    /// <remarks>Turn off to serve a hand-written file from <c>wwwroot</c> instead.</remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>Paths disallowed for all user agents. Framework paths are added automatically.</summary>
    public List<string> Disallowed { get; set; } = [];

    /// <summary>Paths explicitly allowed — used to carve exceptions out of a broader disallow.</summary>
    public List<string> Allowed { get; set; } = [];

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

    internal RobotsOptions Clone() => new()
    {
        Enabled = Enabled,
        Disallowed = [.. Disallowed],
        Allowed = [.. Allowed],
    };
}
