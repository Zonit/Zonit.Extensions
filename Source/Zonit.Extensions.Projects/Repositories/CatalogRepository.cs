namespace Zonit.Extensions.Projects.Repositories;

/// <summary>
/// Per-scope cache for the current user's project / catalog snapshot. Mirror of
/// <c>WorkspaceRepository</c> for the project domain; see that type for the full
/// contract notes (OnChange-on-init, parallel hydration, no cross-scope cache).
/// </summary>
internal sealed class CatalogRepository(IProjectSource organizationProject) : ICatalogManager
{
    private readonly IProjectSource _organizationProject = organizationProject;
    private StateModel? _state;

    public event Action? OnChange;

    public CatalogModel? Catalog => _state?.Catalog;
    public IReadOnlyCollection<ProjectModel>? Projects => _state?.Projects;
    public StateModel? State => _state;

    public void Initialize(StateModel model)
    {
        _state = model;
        StateChanged();
    }

    public async Task<StateModel> InitializeAsync()
    {
        var catalogTask = _organizationProject.InitializeAsync();
        var projectsTask = _organizationProject.GetProjectsAsync();
        await Task.WhenAll(catalogTask, projectsTask);

        _state = new StateModel
        {
            Catalog = catalogTask.Result,
            Projects = projectsTask.Result,
        };
        StateChanged();
        return _state;
    }

    public async Task SwitchProjectAsync(Guid projectId)
    {
        if (_state is null)
            return;

        _state.Catalog = await _organizationProject.SwitchProjectAsync(projectId);
        StateChanged();
    }

    public void StateChanged()
        => OnChange?.Invoke();
}
