namespace Zonit.Extensions.Projects;

/// <summary>
/// Represents a project
/// </summary>
public class ProjectModel
{
    /// <summary>
    /// Project ID. Must be non-empty: a row carrying <see cref="Guid.Empty"/> is not addressable by
    /// <see cref="ICatalogManager.SwitchProjectAsync"/> and is therefore dropped from
    /// <see cref="ICatalogProvider.Visible"/>.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Project name (display label).
    /// </summary>
    /// <remarks>
    /// Projected to <see cref="Title"/> for <see cref="ICatalogProvider"/>, so values that are blank
    /// or longer than <see cref="Title.MaxLength"/> graphemes cannot be represented faithfully. The
    /// projection never throws: blank becomes an empty title, longer text is cut at the grapheme
    /// boundary.
    /// </remarks>
    public string Name { get; set; } = string.Empty;
}