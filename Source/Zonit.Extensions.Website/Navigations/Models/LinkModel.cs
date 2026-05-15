namespace Zonit.Extensions.Website;

/// <summary>
/// A simple labeled hyperlink (used by groups, breadcrumbs, etc.).
/// </summary>
public sealed class LinkModel
{
    /// <summary>Display text of the link.</summary>
    public Title Title { get; init; }

    /// <summary>Target path within the site. Empty when the link is a non-clickable header.</summary>
    public UrlPath Url { get; init; }

    /// <summary>HTML target attribute.</summary>
    public Target Target { get; init; } = Target.Self;

    public LinkModel() { }

    public LinkModel(Title title, UrlPath url = default, Target target = Target.Self)
    {
        Title = title;
        Url = url;
        Target = target;
    }
}
