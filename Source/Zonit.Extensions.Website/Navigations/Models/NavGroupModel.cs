namespace Zonit.Extensions.Website;

/// <summary>
/// A navigation group — a collapsible/expandable header containing links and sub-groups.
/// </summary>
/// <remarks>
/// Groups form a tree: a group can have <see cref="Children"/> (links) and
/// <see cref="Groups"/> (sub-groups). A group is bound to a logical
/// <see cref="Position"/> within a host (e.g. <c>"sidebar"</c>, <c>"header"</c>, <c>"footer"</c>).
/// The host (Dashboard / Website) decides which area-key to filter by –
/// see <see cref="IWebsiteArea"/>.
/// </remarks>
public sealed class NavGroupModel
{
    /// <summary>Display title of the group.</summary>
    public Title Title { get; init; }

    /// <summary>Icon identifier or inline SVG markup.</summary>
    public string? Icon { get; init; }

    /// <summary>Optional clickable header link (when the group itself is also a link).</summary>
    public LinkModel? Link { get; init; }

    /// <summary>Optional permission required to see this group.</summary>
    public Permission Permission { get; init; }

    /// <summary>Whether the group is expanded by default.</summary>
    public bool Expanded { get; init; }

    /// <summary>Direct child links of this group.</summary>
    public IReadOnlyList<NavLinkModel>? Children { get; init; }

    /// <summary>
    /// Logical position within the layout (e.g. <c>"sidebar"</c>, <c>"header"</c>, <c>"footer"</c>).
    /// Free-form string; UI layer interprets it.
    /// </summary>
    public string? Position { get; init; }

    /// <summary>Display order within the position.</summary>
    public int Order { get; init; }

    /// <summary>Optional nested groups (sub-tree).</summary>
    public IReadOnlyList<NavGroupModel>? Groups { get; init; }

    /// <summary>
    /// Free-form metadata bag for layout-specific settings (badges, tooltips, etc.).
    /// Kept as <see cref="IReadOnlyDictionary{TKey, TValue}"/> so consumers can attach
    /// data without forcing them into a fixed schema.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Settings { get; init; }
}
