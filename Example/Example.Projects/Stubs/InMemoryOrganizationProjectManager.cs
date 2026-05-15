using Zonit.Extensions.Auth;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;

using Example.Shared;

namespace Example.Projects.Stubs;

/// <summary>
/// Backs <see cref="ICatalogProvider"/> / <see cref="ICatalogManager"/>: scoped to the user's
/// currently selected organization so switching org resets the visible projects.
/// </summary>
internal sealed class InMemoryOrganizationProjectManager(
    DemoStore store,
    IAuthenticatedProvider auth,
    IWorkspaceProvider workspace) : IProjectSource
{
    public Task<CatalogModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var orgId = workspace.Organization.Id;
        var userId = auth.Current.Id;

        if (orgId == Guid.Empty || userId == Guid.Empty)
            return Task.FromResult(new CatalogModel());

        if (!store.CurrentProjectByUserOrg.TryGetValue((userId, orgId), out var projectId))
            return Task.FromResult(new CatalogModel());

        if (!store.Projects.TryGetValue(projectId, out var project))
            return Task.FromResult(new CatalogModel());

        return Task.FromResult(new CatalogModel { Project = project });
    }

    public Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var orgId = workspace.Organization.Id;
        if (orgId == Guid.Empty || !store.OrganizationProjects.TryGetValue(orgId, out var projectIds))
            return Task.FromResult<IReadOnlyCollection<ProjectModel>?>(null);

        var projects = projectIds.Select(id => store.Projects[id]).ToArray();
        return Task.FromResult<IReadOnlyCollection<ProjectModel>?>(projects);
    }

    public Task<CatalogModel?> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var orgId = workspace.Organization.Id;
        var userId = auth.Current.Id;

        if (userId == Guid.Empty || orgId == Guid.Empty)
            return Task.FromResult<CatalogModel?>(null);

        if (!store.OrganizationProjects.TryGetValue(orgId, out var allowed) || !allowed.Contains(projectId))
            return Task.FromResult<CatalogModel?>(null);

        if (!store.Projects.TryGetValue(projectId, out var project))
            return Task.FromResult<CatalogModel?>(null);

        store.CurrentProjectByUserOrg[(userId, orgId)] = projectId;
        return Task.FromResult<CatalogModel?>(new CatalogModel { Project = project });
    }
}
