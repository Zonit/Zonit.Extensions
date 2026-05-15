using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Zonit.Extensions.Website;

/// <summary>
/// Middleware/routing half of an Area: contributes navigation, Razor pages and
/// (optionally) per-Site middleware + minimal-API endpoints. An Area is mountable
/// at multiple Sites — see <see cref="SiteOptions.AddArea{TArea}"/>.
/// </summary>
/// <remarks>
/// <para><b>DI side</b>: implement <see cref="IWebsiteServices"/> on the same class
/// (or a sibling class) to register the Area's services. <see cref="IWebsiteServices"/>
/// is wired exactly once at <c>builder.Services.AddWebsite(o => o.AddArea&lt;T&gt;())</c>;
/// <see cref="IWebsiteArea"/> can be mounted on any number of Sites at run-time.</para>
///
/// <para><b>Hook timings</b>:</para>
/// <list type="bullet">
///   <item><see cref="Use"/> runs inside the Site's branch <em>before</em> routing —
///         use it to attach area-scoped middleware (signed-URL guards, request
///         enrichers, per-area redirects).</item>
///   <item><see cref="MapEndpoints"/> runs inside the Site's branch endpoint route
///         builder — use it to map minimal-API endpoints (login POST, OAuth callback,
///         webhooks). Endpoints inherit the Site's <c>PathBase</c> automatically.</item>
/// </list>
/// </remarks>
public interface IWebsiteArea
{
    /// <summary>
    /// Stable, unique key identifying this area (e.g. <c>"payments"</c>, <c>"identity"</c>).
    /// Must be unique within a single Site (a Site cannot mount two areas with the same key).
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Razor components assembly contributed by this area. Used by the host to wire
    /// <c>MapRazorComponents&lt;TApp&gt;().AddAdditionalAssemblies(...)</c> per Site.
    /// </summary>
    /// <remarks>
    /// Default: the assembly that declares the concrete <see cref="IWebsiteArea"/> type.
    /// Override only when Razor components live in a separate assembly.
    /// </remarks>
    Assembly ComponentsAssembly => GetType().Assembly;

    /// <summary>Static navigation items contributed by this area.</summary>
    IReadOnlyList<NavGroup> Navigation => Array.Empty<NavGroup>();

    /// <summary>
    /// Optional. Runs at the START of the Site's branch (after PathBase, BEFORE routing
    /// and auth). Use for libraries that must wrap every request from byte zero —
    /// e.g. <c>app.UseImageSharp()</c>, per-area static-file pipelines. Default: no-op.
    /// </summary>
    void App(IApplicationBuilder app) { }

    /// <summary>
    /// Optional. Runs AFTER auth + Zonit hydrators, BEFORE endpoints. Use for area-scoped
    /// middleware that needs an authenticated principal / hydrated workspace (signed-URL
    /// guards consuming identity, audit hooks). Default: no-op.
    /// </summary>
    void Use(IApplicationBuilder app) { }

    /// <summary>
    /// Optional. Runs inside the Site's branch endpoint route builder — register
    /// minimal-API endpoints here (<c>endpoints.MapPost("login", ...)</c>). Default:
    /// no-op. Endpoints inherit the Site's <c>PathBase</c> and any
    /// <see cref="SiteOptions.Permission"/> applied at the Razor level.
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
