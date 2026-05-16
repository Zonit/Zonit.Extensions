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
    /// <param name="cancellationToken">Cancellation forwarded to the
    /// <c>IProjectSource</c> implementation. Pass
    /// <c>HttpContext.RequestAborted</c> from middleware so an aborted request
    /// stops the underlying work.</param>
    public Task<StateModel> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the current project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="cancellationToken">Forwarded to the underlying source.</param>
    public Task SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

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