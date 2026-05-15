namespace Zonit.Extensions.Organizations;

/// <summary>
/// Consumer-side data adapter for organizations / workspaces.
/// </summary>
/// <remarks>
/// <para>The library auto-registers a <c>NullOrganizationSource</c> via
/// <c>AddOrganizationsExtension()</c>; a host that does <b>not</b> register its
/// own implementation simply shows an empty workspace list (no exceptions).</para>
/// </remarks>
public interface IOrganizationSource
{
    /// <summary>
    /// Loads the current workspace state for the active user: the user's "current"
    /// organization (if any) and their permissions/roles inside it.
    /// </summary>
    Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every organization the active user can switch into.
    /// </summary>
    Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the active user's current organization. Returns the new
    /// workspace snapshot, or <see langword="null"/> when the user has no access.
    /// </summary>
    Task<WorkspaceModel?> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
