namespace Zonit.Extensions.Website.Abstractions.Navigations.Types;

public static class Target
{
    /// <summary>
    /// Opens the linked document in the same frame as it was clicked (this is default)
    /// </summary>
    public const string Self = "_self";

    /// <summary>
    /// Opens the linked document in a new window or tab
    /// </summary>
    public const string Blank = "_blank";

    /// <summary>
    /// Opens the linked document in the parent frame
    /// </summary>
    public const string Parent = "_parent";

    /// <summary>
    /// Opens the linked document in the full body of the window
    /// </summary>
    public const string Top = "_top";
}