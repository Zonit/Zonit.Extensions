namespace Zonit.Extensions.Website;

/// <summary>
/// A leaf navigation item — a single clickable link with optional sub-links.
/// </summary>
public sealed record NavItem
{
    /// <summary>Display text.</summary>
    public Title Title { get; init; }

    /// <summary>
    /// Icon identifier or inline SVG markup. Kept as <see cref="string"/> intentionally —
    /// rendering is delegated to the UI layer (MudBlazor / custom).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Destination path <b>within the site</b>. The renderer resolves it against the active
    /// <c>&lt;base href&gt;</c>, so it picks up the mount and the culture prefix automatically.
    /// </summary>
    /// <remarks>
    /// <see cref="UrlPath"/> rejects absolute addresses by design — assigning
    /// <c>"https://twitter.com/acme"</c> throws. That is the type doing its job; put external
    /// destinations in <see cref="External"/>.
    /// </remarks>
    public UrlPath Url { get; init; }

    /// <summary>
    /// Destination <b>outside</b> the site — a social profile, a status page, documentation on
    /// another host. Wins over <see cref="Url"/> when both are set.
    /// </summary>
    /// <remarks>
    /// <para>A separate property rather than a looser type on <see cref="Url"/>, because the two
    /// are rendered differently and the distinction is worth keeping in the type system: an
    /// in-site path is resolved against <c>&lt;base href&gt;</c> and can be matched against the
    /// current route to mark the item active, while an absolute URL must be emitted verbatim and
    /// can never be "active".</para>
    ///
    /// <para>Set <see cref="Target"/> to <c>Blank</c> yourself if you want a new tab — the
    /// framework does not assume it, because "external" and "should open elsewhere" are not the
    /// same decision.</para>
    ///
    /// <code>
    /// new NavItem { Title = "GitHub", External = "https://github.com/acme", Target = Target.Blank }
    /// </code>
    /// </remarks>
    public Url External { get; init; }

    /// <summary>Whether this item points outside the site.</summary>
    public bool IsExternal => External.HasValue;

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

    /// <summary>
    /// Whether <see cref="Title"/> and <see cref="Tooltip"/> go through the translation registry.
    /// <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// The translation key in this framework <b>is</b> the English source string, so a literal
    /// <c>Title = "Settings"</c> is already a valid key. Text with no rendition falls through to
    /// itself, so this is safe for names that are never translated — turn it off only for a brand
    /// or product name that must never be looked up, or for text you already passed through
    /// <c>T(…)</c> yourself.
    /// </remarks>
    public bool Translate { get; init; } = true;
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
