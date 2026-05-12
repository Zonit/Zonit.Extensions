namespace Zonit.Extensions.Website;

/// <summary>
/// Configuration for a Website host.
/// </summary>
/// <remarks>
/// Used by <c>AddWebsite(...)</c>. Captures hosting-level concerns (compression / proxy /
/// antiforgery / blazor mode) plus a registry of plug-in <see cref="IWebsiteArea"/>s.
/// </remarks>
public sealed class WebsiteOptions
{
    /// <summary>Listening address of the site. Used for self-link generation and signed URLs.</summary>
    public Url Url { get; set; }

    /// <summary>
    /// Optional one-time password / static auth token gating access to the site.
    /// <c>null</c> = no token required.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>Sit behind a reverse proxy (nginx / traefik). Enables forwarded-headers middleware.</summary>
    public bool Proxy { get; set; } = false;

    /// <summary>Enable response compression (gzip + brotli).</summary>
    public bool Compression { get; set; } = true;

    /// <summary>Enable antiforgery middleware.</summary>
    public bool AntiForgery { get; set; } = true;

    /// <summary>Blazor hosting mode for the site.</summary>
    public WebsiteMode Mode { get; set; } = WebsiteMode.Server;

    /// <summary>Areas registered for this host. Use <see cref="AddArea"/> to add.</summary>
    public IReadOnlyList<IWebsiteArea> Areas => _areas;

    private readonly List<IWebsiteArea> _areas = new();

    /// <summary>Registers a plug-in area in this host.</summary>
    public WebsiteOptions AddArea(IWebsiteArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (_areas.Any(a => a.Key.Equals(area.Key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Area with key '{area.Key}' is already registered.");
        _areas.Add(area);
        return this;
    }
}
