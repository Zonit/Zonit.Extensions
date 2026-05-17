namespace Zonit.Extensions.Website;

/// <summary>
/// A leaf navigation item — a single clickable link with optional sub-links.
/// </summary>
public sealed class NavItem
{
    /// <summary>Display text.</summary>
    public Title Title { get; init; }

    /// <summary>
    /// Icon identifier or inline SVG markup. Kept as <see cref="string"/> intentionally —
    /// rendering is delegated to the UI layer (MudBlazor / custom).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Destination path within the site. Use <see cref="UrlPath"/> for in-site links
    /// (renderer adds <c>PathBase</c> automatically); for external destinations, render
    /// a full <see cref="Url"/> through <see cref="Icon"/> or a custom layout slot.
    /// </summary>
    public UrlPath Url { get; init; }

    /// <summary>Optional permission required to see / activate this link.</summary>
    public Permission Permission { get; init; }

    /// <summary>HTML target attribute.</summary>
    public Target Target { get; init; } = Target.Self;

    /// <summary>Display order within the parent group.</summary>
    public int Order { get; init; }

    /// <summary>
    /// <c>true</c> – exact URL match required to mark this link active.
    /// <c>false</c> – partial (prefix / contains) match.
    /// </summary>
    public bool Match { get; init; } = true;

    /// <summary>Optional nested links (sub-tree). <c>null</c> for leaf links.</summary>
    public IReadOnlyList<NavItem>? Children { get; init; }

    /// <summary>Optional badge text rendered next to the item (e.g. "12", "NEW").</summary>
    public string? Badge { get; init; }

    /// <summary>Badge color hint for the UI layer.</summary>
    public NavBadgeColor BadgeColor { get; init; } = NavBadgeColor.Default;

    /// <summary>Optional tooltip text displayed on hover.</summary>
    public string? Tooltip { get; init; }

    /// <summary>When <c>true</c>, the item is rendered as non-interactive (greyed out).</summary>
    public bool Disabled { get; init; }
}

/// <summary>Color hint for <see cref="NavItem.BadgeColor"/> / <see cref="NavGroup"/> badges.</summary>
public enum NavBadgeColor
{
    /// <summary>Neutral / theme default.</summary>
    Default,
    /// <summary>Primary theme color.</summary>
    Primary,
    /// <summary>Secondary theme color.</summary>
    Secondary,
    /// <summary>Success indicator (green).</summary>
    Success,
    /// <summary>Warning indicator (orange/yellow).</summary>
    Warning,
    /// <summary>Error indicator (red).</summary>
    Error,
    /// <summary>Informational indicator (blue).</summary>
    Info
}
