namespace Zonit.Extensions.Projects;

/// <summary>
/// Consumer-side data adapter for projects under the active organization.
/// </summary>
/// <remarks>
/// <para>The library auto-registers a <c>NullProjectSource</c> via
/// <c>AddProjectsExtension()</c>; a host that does <b>not</b> register its own
/// implementation simply shows an empty catalog (no exceptions).</para>
/// </remarks>
public interface IProjectSource
{
    /// <summary>Loads the current catalog state (active project for the active org).</summary>
    Task<CatalogModel> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists every project the active user can switch into within the active org.</summary>
    Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the active project. Returns the new catalog snapshot or
    /// <see langword="null"/> when the user has no access.
    /// </summary>
    Task<CatalogModel?> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
