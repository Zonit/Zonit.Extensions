﻿namespace Zonit.Extensions.Organizations;

/// <summary>
/// User state management in the organization
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>
    /// Changes the state
    /// </summary>
    /// <param name="model"></param>
    public void Initialize(StateModel model);

    /// <summary>
    /// Retrieves details of the user's organization
    /// </summary>
    public Task<StateModel> InitializeAsync();

    /// <summary>
    /// Change the default user organization
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    public Task SwitchOrganizationAsync(Guid organizationId);

    /// <summary>
    /// Workspace data
    /// </summary>
    public WorkspaceModel? Workspace { get; }

    /// <summary>
    /// Get all user organizations
    /// </summary>
    public IReadOnlyCollection<OrganizationModel>? Organizations { get; }

    /// <summary>
    /// User status, workspace, organization list, etc.
    /// </summary>
    public StateModel? State { get; }

    /// <summary>
    /// Event of changing the user organization
    /// </summary>
    public event Action? OnChange;
}