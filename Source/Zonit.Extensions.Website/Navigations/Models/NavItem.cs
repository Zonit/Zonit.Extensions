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
}
