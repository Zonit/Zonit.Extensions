namespace Zonit.Extensions.Website;

/// <summary>
/// Anchor target attribute (HTML <c>target</c>).
/// </summary>
public enum Target
{
    /// <summary>Opens the linked document in the same frame as it was clicked (default).</summary>
    Self = 0,

    /// <summary>Opens the linked document in a new window or tab.</summary>
    Blank = 1,

    /// <summary>Opens the linked document in the parent frame.</summary>
    Parent = 2,

    /// <summary>Opens the linked document in the full body of the window.</summary>
    Top = 3,
}

/// <summary>Extension methods for <see cref="Target"/>.</summary>
public static class TargetExtensions
{
    /// <summary>Returns the HTML <c>target</c> attribute value (e.g. <c>"_blank"</c>).</summary>
    public static string ToHtml(this Target target) => target switch
    {
        Target.Blank => "_blank",
        Target.Parent => "_parent",
        Target.Top => "_top",
        _ => "_self",
    };
}
