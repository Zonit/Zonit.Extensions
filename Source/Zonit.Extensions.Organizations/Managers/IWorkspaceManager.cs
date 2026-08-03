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
    /// Retrieves details of the user's organization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation forwarded to the
    /// <c>IOrganizationSource</c> implementation. Pass
    /// <c>HttpContext.RequestAborted</c> from middleware so an aborted request
    /// stops the underlying database / HTTP work.</param>
    public Task<StateModel> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Change the default user organization.
    /// </summary>
    /// <param name="organizationId">Organization to switch into.</param>
    /// <param name="cancellationToken">Forwarded to the underlying source.</param>
    /// <returns>
    /// <see langword="true"/> when the switch was granted and the workspace snapshot now describes
    /// <paramref name="organizationId"/>; <see langword="false"/> when
    /// <c>IOrganizationSource.SwitchOrganizationAsync</c> answered <see langword="null"/>
    /// ("no access"), in which case the previously selected organization is left untouched.
    /// </returns>
    public Task<bool> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

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