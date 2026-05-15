using Zonit.Extensions.Auth;
using Zonit.Extensions.Organizations;

using Example.Shared;

namespace Example.Organizations.Stubs;

/// <summary>
/// Backs <see cref="IWorkspaceProvider"/> / <see cref="IWorkspaceManager"/>: returns the
/// current user's selected organization and the list of organizations they can switch to.
/// Real hosts hit a database; here we read <see cref="DemoStore"/>.
/// </summary>
internal sealed class InMemoryUserOrganizationManager(
    DemoStore store,
    IAuthenticatedProvider auth) : IOrganizationSource
{
    public Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var userId = auth.Current.Id;
        if (userId == Guid.Empty)
            return Task.FromResult(new WorkspaceModel());

        if (!store.CurrentOrganizationByUser.TryGetValue(userId, out var orgId))
            return Task.FromResult(new WorkspaceModel());

        if (!store.Organizations.TryGetValue(orgId, out var org))
            return Task.FromResult(new WorkspaceModel());

        return Task.FromResult(new WorkspaceModel
        {
            Organization = org,
            Roles = auth.Current.Roles.Select(r => r.Value).ToArray(),
            Permissions = auth.Current.Permissions.Select(p => p.Value).ToArray(),
        });
    }

    public Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var userId = auth.Current.Id;
        if (userId == Guid.Empty || !store.UserOrganizations.TryGetValue(userId, out var orgIds))
            return Task.FromResult<IReadOnlyCollection<OrganizationModel>?>(null);

        var orgs = orgIds.Select(id => store.Organizations[id]).ToArray();
        return Task.FromResult<IReadOnlyCollection<OrganizationModel>?>(orgs);
    }

    public Task<WorkspaceModel?> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var userId = auth.Current.Id;
        if (userId == Guid.Empty) return Task.FromResult<WorkspaceModel?>(null);
        if (!store.UserOrganizations.TryGetValue(userId, out var orgIds) || !orgIds.Contains(organizationId))
            return Task.FromResult<WorkspaceModel?>(null);
        if (!store.Organizations.TryGetValue(organizationId, out var org))
            return Task.FromResult<WorkspaceModel?>(null);

        store.CurrentOrganizationByUser[userId] = organizationId;
        return Task.FromResult<WorkspaceModel?>(new WorkspaceModel
        {
            Organization = org,
            Roles = auth.Current.Roles.Select(r => r.Value).ToArray(),
            Permissions = auth.Current.Permissions.Select(p => p.Value).ToArray(),
        });
    }
}
