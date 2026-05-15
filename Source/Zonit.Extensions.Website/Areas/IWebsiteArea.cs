using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website;

/// <summary>
/// Represents a self-contained, plug-in unit of a website host (Dashboard / public Website / etc.).
/// </summary>
/// <remarks>
/// <para>An <see cref="IWebsiteArea"/> is data-first: each implementation is a plain POCO that
/// exposes <see cref="Navigation"/> as a value, optionally registers its own services via
/// <see cref="ConfigureServices"/>, and points at a Razor components <see cref="ComponentsAssembly"/>
/// (to be wired into <c>AddRazorComponents().AddAdditionalAssemblies(...)</c>).</para>
///
/// <para>The host (<c>AddWebsite</c> / future <c>AddDashboard</c>) decides where each area
/// is mounted (URL prefix / layout / etc.). Multiple hosts can register the same area instance
/// to share functionality – e.g. an <c>IdentityWebsiteArea</c> can appear in both an admin
/// dashboard and a public website.</para>
/// </remarks>
public interface IWebsiteArea
{
    /// <summary>
    /// Stable, unique key identifying this area within a host (e.g. <c>"payments"</c>,
    /// <c>"identity"</c>, <c>"affiliate-admin"</c>).
    /// </summary>
    string Key { get; }

    /// <summary>Human-readable name (e.g. for admin/debug UIs).</summary>
    Title DisplayName { get; }

    /// <summary>
    /// Razor components assembly contributed by this area. Used by the host to wire
    /// <c>RazorComponentsEndpointConventionBuilder.AddAdditionalAssemblies(...)</c>.
    /// </summary>
    /// <remarks>
    /// Default: the assembly that declares the concrete <see cref="IWebsiteArea"/> type.
    /// Override only if your Razor components live in a separate assembly from the area
    /// implementation.
    /// </remarks>
    Assembly ComponentsAssembly => GetType().Assembly;

    /// <summary>
    /// Static navigation contributed by this area. Combined by the runtime
    /// <see cref="INavigationProvider"/> with navigation from other areas.
    /// </summary>
    IReadOnlyList<NavGroup> Navigation => Array.Empty<NavGroup>();

    /// <summary>
    /// Optional hook to register the area's services (idempotent — use <c>TryAdd*</c> for shared services).
    /// </summary>
    void ConfigureServices(IServiceCollection services) { }
}
