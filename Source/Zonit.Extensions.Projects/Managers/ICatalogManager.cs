﻿namespace Zonit.Extensions.Projects;

/// <summary>
/// Managing project status in the asp.net circuit
/// </summary>
public interface ICatalogManager
{
    /// <summary>
    /// Initializes the project manager.
    /// </summary>
    /// <param name="model"></param>
    public void Initialize(StateModel model);

    /// <summary>
    /// Initializes the project manager.
    /// </summary>
    /// <returns></returns>
    public Task<StateModel> InitializeAsync();

    /// <summary>
    /// Switches the current project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns></returns>
    public Task SwitchProjectAsync(Guid projectId);

    /// <summary>
    /// Gets the current project.
    /// </summary>
    public CatalogModel? Catalog { get; }

    /// <summary>
    /// Get all projects.
    /// </summary>
    public IReadOnlyCollection<ProjectModel>? Projects { get; }

    /// <summary>
    /// Event that is triggered when the project is changed.
    /// </summary>
    public StateModel? State { get; }

    /// <summary>
    /// Event that is triggered when the project is changed.
    /// </summary>
    public event Action? OnChange;
}