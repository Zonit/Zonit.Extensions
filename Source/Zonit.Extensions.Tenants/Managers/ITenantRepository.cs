namespace Zonit.Extensions.Tenants;

/// <summary>
/// Per-scope cache of the resolved <see cref="Tenant"/>. Boundary between
/// <see cref="ITenantSource"/> (consumer data source) and
/// <see cref="ITenantProvider"/> (read API consumed by views and components). Mirrors
/// the <c>IWorkspaceManager</c> / <c>ICatalogManager</c> shape.
/// </summary>
/// <remarks>
/// <para><b>Hydration.</b> The middleware (<c>TenantMiddleware</c>) calls
/// <see cref="InitializeAsync"/> exactly once per request scope. Subsequent reads of
/// <see cref="Current"/> hit the cached snapshot.</para>
///
/// <para><b>OnChange.</b> Both <see cref="Initialize"/> and <see cref="InitializeAsync"/>
/// raise <see cref="OnChange"/>. Late-subscribed Razor components observe the snapshot
/// the first time they receive the event.</para>
///
/// <para><b>Scope.</b> Lifetime: <c>Scoped</c>. No cross-request cache lives here — that
/// belongs in <see cref="ITenantSource"/> implementations.</para>
/// </remarks>
public interface ITenantRepository
{
    /// <summary>Currently resolved tenant for this scope, or <see langword="null"/> when none was found.</summary>
    Tenant? Current { get; }

    /// <summary>Replaces the current snapshot synchronously (e.g. from prerender persisted state). Raises <see cref="OnChange"/>.</summary>
    void Initialize(Tenant? tenant);

    /// <summary>
    /// Resolves the tenant for <paramref name="domain"/> via <see cref="ITenantSource.GetByDomainAsync"/>
    /// and stores the result in this scope. Idempotent — calling twice with the same domain on a
    /// scope that already has a hit short-circuits without round-tripping the manager.
    /// </summary>
    Task<Tenant?> InitializeAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Raised when the tenant snapshot changes within this scope.</summary>
    event Action? OnChange;
}
