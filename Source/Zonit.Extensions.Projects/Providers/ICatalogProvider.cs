using System.Collections.Immutable;

namespace Zonit.Extensions.Projects;

/// <summary>
/// Read-only API exposing the user's currently selected catalog project as a
/// <see cref="Extensions.Project"/> value object.
/// </summary>
public interface ICatalogProvider
{
    /// <summary>
    /// Current project (<see cref="Extensions.Project"/> VO).
    /// Returns <see cref="Extensions.Project.Empty"/> when no project is selected.
    /// </summary>
    Project Project { get; }

    /// <summary>
    /// Visible projects (read-only) — for views aggregating data across multiple projects.
    /// </summary>
    ImmutableArray<Project> Visible { get; }

    /// <summary>Raised when the active project changes.</summary>
    event Action? OnChange;
}