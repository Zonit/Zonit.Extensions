﻿
namespace Zonit.Extensions.Projects.Repositories;

internal class CatalogRepository(
    IOrganizationProjectManager _organizationProject
    ) : ICatalogManager
{
    public event Action? OnChange;

    StateModel? _state;

    public CatalogModel? Catalog => _state?.Catalog;
    public IReadOnlyCollection<ProjectModel>? Projects => _state?.Projects;
    public StateModel? State => _state;

    public void Initialize(StateModel model)
        => _state = model;

    public async Task<StateModel> InitializeAsync()
    {
        return _state = new StateModel
        {
            Catalog = await _organizationProject.InitializeAsync(),
            Projects = await _organizationProject.GetProjectsAsync()
        };
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
