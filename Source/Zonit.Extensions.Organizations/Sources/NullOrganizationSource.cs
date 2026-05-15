namespace Zonit.Extensions.Organizations;

/// <summary>
/// Safe-default <see cref="IOrganizationSource"/>: empty workspace, no orgs,
/// switches always fail with <see langword="null"/>. Registered via <c>TryAdd*</c>
/// in <c>AddOrganizationsExtension()</c>.
/// </summary>
internal sealed class NullOrganizationSource : IOrganizationSource
{
    private static readonly WorkspaceModel _empty = new();

    public Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_empty);

    public Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<OrganizationModel>?>(Array.Empty<OrganizationModel>());

    public Task<WorkspaceModel?> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult<WorkspaceModel?>(null);
}
