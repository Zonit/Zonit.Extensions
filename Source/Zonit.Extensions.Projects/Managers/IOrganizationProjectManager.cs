namespace Zonit.Extensions.Projects;


public interface IOrganizationProjectManager
{
    public Task<CatalogModel> InitializeAsync();
    public Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync();
    public Task<CatalogModel?> SwitchProjectAsync(Guid projectId);
}