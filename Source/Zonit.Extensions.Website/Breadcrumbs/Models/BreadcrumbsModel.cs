namespace Zonit.Extensions.Website;

/// <summary>
/// Single breadcrumb item.
/// </summary>
public class BreadcrumbsModel
{
    /// <summary>Display text.</summary>
    public Title Text { get; init; }

    /// <summary>Optional in-site path to navigate to. Empty = non-clickable.</summary>
    public UrlPath Href { get; init; }

    /// <summary>Whether the item is disabled (cannot be clicked).</summary>
    public bool Disabled { get; init; }

    /// <summary>Icon identifier (e.g. MudBlazor icon name) or inline SVG.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Optional template key – host can render an interactive component for this slot
    /// (e.g. <c>"catalog"</c>, <c>"workspace"</c>) instead of plain text.
    /// </summary>
    public string? Template { get; set; }

    public BreadcrumbsModel() { }

    public BreadcrumbsModel(Title text, UrlPath href = default, bool disabled = false, string? icon = null)
    {
        Text = text;
        Href = href;
        Disabled = disabled;
        Icon = icon;
    }
}
