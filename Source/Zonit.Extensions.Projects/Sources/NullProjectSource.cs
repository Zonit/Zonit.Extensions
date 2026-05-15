namespace Zonit.Extensions.Projects;

internal sealed class NullProjectSource : IProjectSource
{
    private static readonly CatalogModel _empty = new();

    public Task<CatalogModel> InitializeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_empty);

    public Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ProjectModel>?>(Array.Empty<ProjectModel>());

    public Task<CatalogModel?> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<CatalogModel?>(null);
}
