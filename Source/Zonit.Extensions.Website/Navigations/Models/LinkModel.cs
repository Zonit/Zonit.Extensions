namespace Zonit.Extensions.Website;

/// <summary>
/// A simple labeled hyperlink (used by groups, breadcrumbs, etc.).
/// </summary>
public sealed record LinkModel
{
    /// <summary>Display text of the link.</summary>
    public Title Title { get; init; }

    /// <summary>Target path within the site. Empty when the link is a non-clickable header.</summary>
    /// <remarks>
    /// <see cref="UrlPath"/> rejects absolute addresses — use <see cref="External"/> for those.
    /// </remarks>
    public UrlPath Url { get; init; }

    /// <summary>Destination outside the site. Wins over <see cref="Url"/> when both are set.</summary>
    public Url External { get; init; }

    /// <summary>Whether this link points outside the site.</summary>
    public bool IsExternal => External.HasValue;

    /// <summary>HTML target attribute.</summary>
    public Target Target { get; init; } = Target.Self;

    public LinkModel() { }

    public LinkModel(Title title, UrlPath url = default, Target target = Target.Self)
    {
        Title = title;
        Url = url;
        Target = target;
    }

    /// <summary>Creates a link to an external address.</summary>
    public LinkModel(Title title, Url external, Target target = Target.Blank)
    {
        Title = title;
        External = external;
        Target = target;
    }
}
