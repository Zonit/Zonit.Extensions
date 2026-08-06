using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website;

/// <summary>
/// Services-time configuration for the Website host. Only flags that influence the
/// <em>DI container</em> live here — everything middleware-level (compression / HSTS /
/// proxy / antiforgery / exception handler / HTTPS redirection / auth render mode)
/// belongs on <see cref="SiteOptions"/> because each Site picks its own request
/// pipeline.
/// </summary>
/// <remarks>
/// <para>Service registrations that match middleware flags
/// (<c>AddAntiforgery</c> / <c>AddResponseCompression</c> / <c>AddHsts</c> /
/// <c>Configure&lt;ForwardedHeadersOptions&gt;</c> / <c>AddProblemDetails</c>) are
/// installed unconditionally — they're cheap, idempotent and never fire by themselves.
/// The matching <c>Use*</c> middleware is then decided per Site.</para>
/// </remarks>
public sealed class WebsiteOptions
{
    private readonly IServiceCollection _services;
    private readonly WebsiteAreaRegistry _registry;

    internal WebsiteOptions(IServiceCollection services, WebsiteAreaRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    /// <summary>
    /// Whether <c>AddWebsite</c> also folds <c>AppData/Settings</c> into the host's configuration.
    /// Default <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>Turn it off for a host that arranges its own configuration ladder. The loader is
    /// idempotent, so a host that only wants <b>different</b> settings does not need this flag —
    /// call <c>builder.AddAppData(o =&gt; …)</c> first and <c>AddWebsite</c> will leave it alone.</para>
    ///
    /// <para>See <c>Zonit.Extensions.Configuration</c> for the file layout and precedence. Note
    /// that <c>AddWebsite</c> must still run before <c>Build()</c> for the files to reach Kestrel
    /// and the logging providers, which read configuration while the host is constructed.</para>
    /// </remarks>
    public bool UseAppData { get; set; } = true;

    /// <summary>Listening address of the site. Used for self-link generation and signed URLs.</summary>
    public Url Url { get; set; }

    /// <summary>
    /// Default in-memory cache (<c>IMemoryCache</c>). Disable if the host already wires a
    /// distributed cache and you want to avoid both being resolved.
    /// </summary>
    public bool MemoryCache { get; set; } = true;

    /// <summary>
    /// Wire <c>AddControllers()</c> for REST/API endpoints. Off by default — most Blazor
    /// Component hosts don't need controllers (prefer <see cref="IWebsiteArea.MapEndpoints"/>
    /// for minimal APIs).
    /// </summary>
    public bool Controllers { get; set; } = false;

    /// <summary>
    /// Wire <c>AddRazorComponents().AddInteractiveServerComponents()</c> at services-time
    /// so every Site branch can map Razor components. Disable only for pure-API hosts.
    /// </summary>
    public bool RazorComponents { get; set; } = true;

    /// <summary>
    /// Wire <c>AddRazorPages()</c> for classic <c>.cshtml</c> pages. Off by default —
    /// Blazor Razor Components is the modern primitive. Enable when migrating older
    /// apps that still ship <c>Pages/</c> with <c>@page</c>.
    /// </summary>
    public bool RazorPages { get; set; } = false;

    /// <summary>
    /// What to do when an <c>ITenantSource</c> is registered but does not recognise the request's
    /// host — <c>TenantResolution.Unknown</c>. Defaults to
    /// <see cref="UnknownHostBehavior.NotFound"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this defaults to refusing the request.</b> Reaching that state means a
    /// hostname is pointed at the application with no tenant behind it: a DNS record added
    /// without the matching row, a typo'd alias, a staging host leaking into production. The
    /// alternative — carrying on — serves the compile-time default branding on a domain the app
    /// does not know, under whatever certificate answered, and looks to a visitor like a real
    /// (if oddly generic) site. That failure is silent, and it is the exact thing a multi-domain
    /// host is least likely to notice.</para>
    ///
    /// <para>Set <see cref="UnknownHostBehavior.Continue"/> when unknown hosts are legitimate —
    /// a marketing site that answers on any domain, a health-check probe hitting the container's
    /// internal name, a catch-all landing page. Pages can then still branch on
    /// <c>ITenantProvider.Resolution</c> themselves.</para>
    ///
    /// <para>Single-site hosts are unaffected: with no <c>ITenantSource</c> registered the
    /// resolution is <c>SingleSite</c>, never <c>Unknown</c>.</para>
    /// </remarks>
    public UnknownHostBehavior UnknownHost { get; set; } = UnknownHostBehavior.NotFound;

    /// <summary>
    /// Registers an Area with the DI container. Instantiates <typeparamref name="TArea"/>
    /// (must have a public parameterless ctor — Areas are data-first POCOs), runs its
    /// <see cref="IWebsiteServices.ConfigureServices"/> hook if implemented, and stores
    /// the singleton instance in <see cref="WebsiteAreaRegistry"/> for later mounting
    /// at <c>app.UseWebsite&lt;TApp&gt;("/", o => o.AddArea&lt;TArea&gt;())</c>.
    /// </summary>
    public WebsiteOptions AddArea<TArea>() where TArea : class, IWebsiteArea, new()
    {
        var area = _registry.Register(new TArea());

        if (area is IWebsiteServices svc)
            svc.ConfigureServices(_services);

        return this;
    }
}
