using Zonit.Extensions.Tenants;

using Example.Shared;

namespace Example.Tenants.Stubs;

/// <summary>
/// Resolves a tenant by request host. The middleware falls back to <see cref="Tenant.Solo"/>
/// when this manager is not registered or returns <see langword="null"/> — try commenting out
/// the registration in <c>Program.cs</c> to flip the demo into solo mode.
/// </summary>
internal sealed class InMemoryTenantManager(DemoStore store) : ITenantSource
{
    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
        => Task.FromResult(store.TenantsByDomain.TryGetValue(domain, out var t) ? t : null);
}
