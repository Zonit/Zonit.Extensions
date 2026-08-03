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

    public async Task<StateModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = _organizationProject.InitializeAsync(cancellationToken);
        var projectsTask = _organizationProject.GetProjectsAsync(cancellationToken);
        await Task.WhenAll(catalogTask, projectsTask);

        _state = new StateModel
        {
            Catalog = catalogTask.Result,
            Projects = projectsTask.Result,
        };
        StateChanged();
        return _state;
    }

    public async Task<bool> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var catalog = await _organizationProject.SwitchProjectAsync(projectId, cancellationToken);

        // null is the source's documented "the user has no access" answer. Writing it through
        // would clear the project the user is currently working in, so a denied switch would cost
        // them the project they were allowed to be in. Refuse the switch and report it instead.
        if (catalog is null)
            return false;

        // A scope that never went through InitializeAsync (no middleware pass, or an interactive
        // circuit whose CatalogStateBridge restore did not run) used to make this whole call a
        // silent no-op. The switch itself needs no prior state, so materialize the snapshot.
        _state ??= new StateModel();
        _state.Catalog = catalog;

        // The visible list is scoped to the organization, not to the active project, so it does
        // not change across a project switch — fetched only when this scope never had one.
        _state.Projects ??= await _organizationProject.GetProjectsAsync(cancellationToken);

        StateChanged();
        return true;
    }

    public void StateChanged()
        => OnChange?.Invoke();
}
