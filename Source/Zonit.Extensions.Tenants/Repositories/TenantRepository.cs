namespace Zonit.Extensions.Tenants.Repositories;

/// <summary>
/// Default scoped <see cref="ITenantRepository"/> implementation. Caches the resolved
/// <see cref="Tenant"/> for the lifetime of the request / circuit and forwards
/// resolution to the consumer-supplied <see cref="ITenantSource"/>.
/// </summary>
/// <remarks>
/// <see cref="ITenantSource"/> is an <b>optional</b> constructor dependency: a solo-site host
/// registers none, and the DI container then supplies the parameter's default. The repository
/// must stay resolvable in that shape because <c>AddTenantsExtension()</c> registers
/// <see cref="ITenantRepository"/> unconditionally.
/// </remarks>
internal sealed class TenantRepository(ITenantSource? source = null) : ITenantRepository
{
    private readonly ITenantSource? _source = source;
    private Tenant? _current;
    private string? _resolvedDomain;

    public Tenant? Current => _current;

    public event Action? OnChange;

    public void Initialize(Tenant? tenant)
    {
        _current = tenant;
        _resolvedDomain = tenant?.Domain;
        OnChange?.Invoke();
    }

    public async Task<Tenant?> InitializeAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain))
            return _current;

        // Solo shape: nothing to resolve against, and — critically — nothing to notify about.
        // Returning early keeps the caller's "unresolved" fallback (Tenant.Solo) the single
        // source of the one OnChange this scope should see.
        if (_source is null)
            return _current;

        // Idempotent: repeated calls with the same domain on a scope that already
        // resolved skip the round trip. The middleware calls this once per request,
        // but Blazor circuits can re-enter on prerender → interactive transitions.
        if (_current is not null
            && string.Equals(_resolvedDomain, domain, StringComparison.OrdinalIgnoreCase))
        {
            return _current;
        }

        var tenant = await _source.GetByDomainAsync(domain, cancellationToken).ConfigureAwait(false);
        var changed = !ReferenceEquals(_current, tenant);
        _current = tenant;
        _resolvedDomain = domain;

        // An unknown host resolves null over a null snapshot — nothing observable changed, and
        // raising anyway made the caller's subsequent Initialize(Tenant.Solo) the *second*
        // notification of a single logical resolution. Every subscriber (TenantService clears
        // its hydrated cache, ExtensionsBase re-runs OnRefreshChangeAsync) paid for both.
        if (changed)
            OnChange?.Invoke();

        return tenant;
    }
}
