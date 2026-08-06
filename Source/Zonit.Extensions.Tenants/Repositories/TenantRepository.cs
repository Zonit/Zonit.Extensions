using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Zonit.Extensions.Tenants.Repositories;

/// <summary>
/// Default scoped <see cref="ITenantRepository"/> implementation. Caches the resolved
/// <see cref="Tenant"/> for the lifetime of the request / circuit and forwards
/// resolution to the consumer-supplied <see cref="ITenantSource"/>.
/// </summary>
/// <remarks>
/// <see cref="ITenantSource"/> is an <b>optional</b> constructor dependency: a single-site host
/// registers none, and the DI container then supplies the parameter's default. The repository
/// must stay resolvable in that shape because <c>AddTenantsExtension()</c> registers
/// <see cref="ITenantRepository"/> unconditionally.
/// </remarks>
internal sealed partial class TenantRepository(
    ITenantSource? source = null,
    ILogger<TenantRepository>? logger = null) : ITenantRepository
{
    private readonly ITenantSource? _source = source;
    private readonly ILogger<TenantRepository> _logger = logger ?? NullLogger<TenantRepository>.Instance;

    /// <remarks>
    /// <see langword="null"/> until something resolves a tenant. Settings never read this
    /// directly — <c>TenantService</c> falls back to configuration and then to compile-time
    /// defaults — so a null here costs no null-checking on any page, and <see cref="Resolution"/>
    /// carries why it is null.
    /// </remarks>
    private Tenant? _current;

    /// <remarks>
    /// The host this scope has already asked the source about — recorded whether or not the
    /// source recognised it, so an unknown domain is not re-queried on every re-entry.
    /// <see langword="null"/> means "never asked".
    /// </remarks>
    private string? _resolvedDomain;

    public Tenant? Current => _current;

    public TenantResolution Resolution { get; private set; } = TenantResolution.None;

    public event Action? OnChange;

    public void Initialize(Tenant? tenant)
    {
        // An explicit instance is a resolution by definition — that is how the prerender→circuit
        // bridge and any host-driven seeding get their state in. Passing null is the deliberate
        // reset, and puts the scope back to "nobody has decided anything".
        Resolution = tenant is null ? TenantResolution.None : TenantResolution.Resolved;

        // No-op guard. Re-seeding the value already in effect would clear TenantService's
        // hydrated cache and re-run every component's OnRefreshChangeAsync for nothing, and the
        // paths that do exactly that are the common ones: a state bridge restoring what the scope
        // already had, a host re-seeding on every render.
        if (ReferenceEquals(_current, tenant))
            return;

        _current = tenant;
        _resolvedDomain = tenant?.Domain;
        OnChange?.Invoke();
    }

    public async Task<Tenant?> InitializeAsync(string domain, CancellationToken cancellationToken = default)
    {
        // Single-site shape: no source to ask, and nothing to notify about. Current simply
        // stays null, and Resolution says that is by design rather than a failure.
        if (_source is null)
        {
            Resolution = TenantResolution.SingleSite;
            return null;
        }

        if (string.IsNullOrEmpty(domain))
            return null;

        // Idempotent: repeated calls with the same host skip the round trip. The middleware calls
        // this once per request, but Blazor circuits re-enter on prerender → interactive
        // transitions. Note this short-circuits on the *domain* rather than on "we have a
        // tenant", so a host the source does not recognise is asked about once per scope too.
        if (string.Equals(_resolvedDomain, domain, StringComparison.OrdinalIgnoreCase))
            return _current;

        var tenant = await _source.GetByDomainAsync(domain, cancellationToken).ConfigureAwait(false);
        _resolvedDomain = domain;
        Resolution = tenant is null ? TenantResolution.Unknown : TenantResolution.Resolved;

        if (tenant is null)
        {
            // A multi-domain host that reaches here is misconfigured: a hostname is pointed at
            // the app with no tenant behind it. Settings still resolve — to configuration or to
            // compile-time defaults — which is the right *rendering* behaviour and the wrong
            // thing to do silently, so this is the one place the package speaks up unasked.
            UnknownHost(_logger, domain);
        }

        if (!ReferenceEquals(_current, tenant))
        {
            _current = tenant;
            OnChange?.Invoke();
        }

        return tenant;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "No tenant is configured for host '{Domain}'. ITenantProvider.Current stays null and settings fall back to configuration and compile-time defaults. Check ITenantSource and the tenant's configured domain; read ITenantProvider.Resolution to handle it explicitly.")]
    private static partial void UnknownHost(ILogger logger, string domain);
}
