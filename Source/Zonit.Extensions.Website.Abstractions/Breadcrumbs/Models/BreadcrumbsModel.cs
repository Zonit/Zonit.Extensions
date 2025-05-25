namespace Zonit.Extensions.Website;

/// <summary>
/// Creates a new instance.
/// </summary>
/// <param name="text">The text to display for this item.</param>
/// <param name="href">The URL to navigate to when this item is clicked.</param>
/// <param name="disabled">Whether the item cannot be clicked.</param>
/// <param name="icon">The custom icon to display for this item.</param>
public class BreadcrumbsModel(string text, string? href, bool disabled = false, string? icon = null)
{
    /// <summary>
    /// The text to display.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// The URL to navigate to when clicked.
    /// </summary>
    public string? Href { get; } = href;

    /// <summary>
    /// Prevents this item from being clicked.
    /// </summary>
    public bool Disabled { get; } = disabled;

    /// <summary>
    /// The custom icon for this item.
    /// </summary>
    public string? Icon { get; } = icon;

    /// <summary>
    /// In the case of interactive templates
    /// </summary>
    public string? Template { get; set; }
}
