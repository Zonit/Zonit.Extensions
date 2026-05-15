namespace Zonit.Extensions.Tenants.Repositories;

/// <summary>
/// Default scoped <see cref="ITenantRepository"/> implementation. Caches the resolved
/// <see cref="Tenant"/> for the lifetime of the request / circuit and forwards
/// resolution to the consumer-supplied <see cref="ITenantSource"/>.
/// </summary>
internal sealed class TenantRepository(ITenantSource manager) : ITenantRepository
{
    private readonly ITenantSource _manager = manager;
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

        // Idempotent: repeated calls with the same domain on a scope that already
        // resolved skip the round trip. The middleware calls this once per request,
        // but Blazor circuits can re-enter on prerender → interactive transitions.
        if (_current is not null
            && string.Equals(_resolvedDomain, domain, StringComparison.OrdinalIgnoreCase))
        {
            return _current;
        }

        var tenant = await _manager.GetByDomainAsync(domain, cancellationToken).ConfigureAwait(false);
        _current = tenant;
        _resolvedDomain = domain;
        OnChange?.Invoke();
        return tenant;
    }
}
