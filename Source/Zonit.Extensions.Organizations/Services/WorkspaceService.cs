namespace Zonit.Extensions.Organizations.Services;

/// <summary>
/// Maps the legacy <see cref="OrganizationModel"/> stored in <see cref="IWorkspaceManager"/>
/// to the public <see cref="Organization"/> value object exposed via <see cref="IWorkspaceProvider"/>.
/// </summary>
internal sealed class WorkspaceService : IWorkspaceProvider, IDisposable
{
    private readonly IWorkspaceManager _manager;
    private Organization _organization = Organization.Empty;

    public Organization Organization => _organization;
    public event Action? OnChange;

    public WorkspaceService(IWorkspaceManager manager)
    {
        _manager = manager;
        _manager.OnChange += HandleStateChanged;
        Hydrate();
    }

    private void Hydrate()
    {
        var model = _manager.Workspace?.Organization;
        _organization = model is null
            ? Organization.Empty
            : new Organization(model.Id, new Title(model.Name));
    }

    private void HandleStateChanged()
    {
        Hydrate();
        OnChange?.Invoke();
    }

    public void Dispose() => _manager.OnChange -= HandleStateChanged;
}