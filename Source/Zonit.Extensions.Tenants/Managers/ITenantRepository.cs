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
    /// <summary>
    /// Currently resolved tenant for this scope, or <see langword="null"/> when none was
    /// resolved. See <see cref="ITenantProvider.Current"/> for why this is nullable.
    /// </summary>
    Tenant? Current { get; }

    /// <summary>
    /// Why <see cref="Current"/> holds what it holds. The signal a multi-domain host needs to
    /// tell "single-site, defaults are correct" from "unknown host, somebody misconfigured DNS".
    /// </summary>
    TenantResolution Resolution { get; }

    /// <summary>
    /// Replaces the current snapshot synchronously (e.g. from prerender persisted state). Passing
    /// <see langword="null"/> resets the scope to "no tenant". Raises
    /// <see cref="OnChange"/> unless the value is already the one in effect.
    /// </summary>
    void Initialize(Tenant? tenant);

    /// <summary>
    /// Resolves the tenant for <paramref name="domain"/> via <see cref="ITenantSource.GetByDomainAsync"/>
    /// and stores the result in this scope. Idempotent — calling twice with the same domain
    /// short-circuits without round-tripping the source, whether or not the first call found
    /// anything.
    /// </summary>
    /// <returns>
    /// The resolved tenant, or <see langword="null"/> when no <see cref="ITenantSource"/> is
    /// registered or it did not recognise <paramref name="domain"/>. In both of those cases
    /// <see cref="Current"/> is left <see langword="null"/>, so the return value is only
    /// interesting to a caller that wants to tell "resolved" from "fell back".
    /// </returns>
    Task<Tenant?> InitializeAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Raised when the tenant snapshot changes within this scope.</summary>
    event Action? OnChange;
}
