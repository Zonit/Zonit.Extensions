namespace Zonit.Extensions.Organizations.Repositories;

/// <summary>
/// Per-scope cache for the current user's workspace + organization snapshot. Acts as
/// the boundary between the (consumer-supplied) <see cref="IOrganizationSource"/>
/// data source and Zonit's <see cref="IWorkspaceProvider"/> consumers.
/// </summary>
/// <remarks>
/// <para><b>Hydration model.</b> The middleware (<c>WorkspaceMiddleware</c>) calls
/// <see cref="InitializeAsync"/> exactly once per request scope; subsequent reads of
/// <see cref="State"/> hit the cached snapshot. Consumers that need a fresh fetch
/// across requests should bring their own caching layer at the
/// <see cref="IOrganizationSource"/> implementation — this class is intentionally
/// stateless beyond the per-scope snapshot, in line with the rule "no caching beyond
/// request scope".</para>
///
/// <para><b>OnChange semantics.</b> Both <see cref="Initialize"/> and
/// <see cref="InitializeAsync"/> raise <see cref="OnChange"/>. This is the fix for the
/// late-subscriber bug: a Razor component that injects <see cref="IWorkspaceProvider"/>
/// in its constructor (or via parameter set) needs to be told the state landed, even if
/// hydration happened slightly before subscription. Idempotent for components that
/// hydrate themselves on <see cref="OnChange"/> — they will re-read the same snapshot.</para>
///
/// <para><b>Performance.</b> <see cref="InitializeAsync"/> issues two consumer calls
/// in parallel (<c>Workspace</c> and <c>Organizations</c>) — they target different
/// resources and can run concurrently, halving the perceived latency on cold loads.</para>
/// </remarks>
internal sealed class WorkspaceRepository(IOrganizationSource userWorkspace) : IWorkspaceManager
{
    private readonly IOrganizationSource _userWorkspace = userWorkspace;
    private StateModel? _state;

    public event Action? OnChange;

    public WorkspaceModel? Workspace => _state?.Workspace;
    public IReadOnlyCollection<OrganizationModel>? Organizations => _state?.Organizations;
    public StateModel? State => _state;

    public void Initialize(StateModel model)
    {
        _state = model;
        StateChanged();
    }

    public async Task<StateModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Parallelise — two independent reads, no need to serialise them.
        var workspaceTask = _userWorkspace.InitializeAsync(cancellationToken);
        var organizationsTask = _userWorkspace.GetOrganizationsAsync(cancellationToken);
        await Task.WhenAll(workspaceTask, organizationsTask);

        _state = new StateModel
        {
            Workspace = workspaceTask.Result,
            Organizations = organizationsTask.Result,
        };
        StateChanged();
        return _state;
    }

    public async Task SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (_state is null)
            return;

        _state.Workspace = await _userWorkspace.SwitchOrganizationAsync(organizationId, cancellationToken);
        StateChanged();
    }

    public void StateChanged()
        => OnChange?.Invoke();
}
