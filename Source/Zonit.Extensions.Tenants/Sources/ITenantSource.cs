namespace Zonit.Extensions.Tenants;

/// <summary>
/// Consumer-side data adapter that resolves a host name to a <see cref="Tenant"/>.
/// </summary>
/// <remarks>
/// <para><b>Caching.</b> The natural seam for cross-request caching — tenants change
/// rarely and a single domain hits this on every cold request. The Zonit-side
/// <c>ITenantRepository</c> caches only within a request scope; durable caching
/// belongs here, in your implementation (typical: a decorator over
/// <c>IMemoryCache</c> or <c>IDistributedCache</c>).</para>
///
/// <para><b>Lifetime.</b> Recommended <c>Scoped</c>. The middleware resolves it once
/// per request and never holds the reference across requests.</para>
///
/// <para>If you do not register an <see cref="ITenantSource"/>, the library auto-wires
/// <c>NullTenantSource</c> via <c>AddTenantsExtension()</c>; <c>TenantMiddleware</c>
/// then falls back to <see cref="Tenant.Solo"/> and the host runs in single-tenant mode.</para>
/// </remarks>
public interface ITenantSource
{
    /// <summary>
    /// Resolves a tenant by its public host name (case-insensitive).
    /// Returns <see langword="null"/> when no tenant matches — the middleware then
    /// either falls back to solo-mode or leaves per-scope state empty depending on
    /// host configuration.
    /// </summary>
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
}
