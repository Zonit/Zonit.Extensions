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
