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

    public async Task<bool> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var workspace = await _userWorkspace.SwitchOrganizationAsync(organizationId, cancellationToken);

        // null is the source's documented "the user has no access" answer. Writing it through
        // would clear the organization the user is currently working in — an unauthorized or
        // stale id would silently log them out of a workspace they DO have access to, and every
        // org-scoped query behind IWorkspaceProvider would start returning nothing. Refusing the
        // switch and reporting it is the only outcome the caller can act on.
        if (workspace is null)
            return false;

        // A scope that never went through InitializeAsync (no middleware pass, or an interactive
        // circuit whose WorkspaceStateBridge restore did not run) used to make this whole call a
        // silent no-op — the switcher button simply did nothing. The switch itself needs no prior
        // state, so materialize the snapshot instead of dropping the user's action.
        _state ??= new StateModel();
        _state.Workspace = workspace;

        // Membership is per user, not per organization, so the switchable list does not change
        // across a switch — it is fetched only when this scope never had one.
        _state.Organizations ??= await _userWorkspace.GetOrganizationsAsync(cancellationToken);

        StateChanged();
        return true;
    }

    public void StateChanged()
        => OnChange?.Invoke();
}
