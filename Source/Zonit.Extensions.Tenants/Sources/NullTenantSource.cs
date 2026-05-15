namespace Zonit.Extensions.Tenants;

/// <summary>
/// Safe-default <see cref="ITenantSource"/>: returns <see langword="null"/> for every
/// host name, which the <c>TenantMiddleware</c> treats as solo-mode fallback
/// (<see cref="Tenant.Solo"/>). Registered via <c>TryAdd*</c> in
/// <c>AddTenantsExtension()</c>.
/// </summary>
internal sealed class NullTenantSource : ITenantSource
{
    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
        => Task.FromResult<Tenant?>(null);
}
