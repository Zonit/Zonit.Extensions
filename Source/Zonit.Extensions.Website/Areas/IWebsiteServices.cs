using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website;

/// <summary>
/// Build-time half of an Area (or a stand-alone module): registers services into the
/// host's <see cref="IServiceCollection"/>. Runs exactly once during
/// <c>builder.Services.AddWebsite(o => o.AddArea&lt;TArea&gt;())</c>, regardless of how
/// many Sites later mount the same Area at run-time.
/// </summary>
/// <remarks>
/// <para><b>Why split from <see cref="IWebsiteArea"/>?</b> An Area can be mounted under
/// multiple Sites (e.g. <c>AuthArea</c> at <c>/</c> and <c>/admin</c>). Service registration
/// must run only once and against the build-time DI container, while routing/nav metadata
/// must run per-Site at middleware time. Two interfaces, one (or two) class(es) — the
/// consumer picks.</para>
///
/// <para>Idempotency: prefer <c>TryAdd*</c> for everything you register. Real consumers
/// will likely override your defaults with their own EF/Dapper-backed sources.</para>
/// </remarks>
public interface IWebsiteServices
{
    /// <summary>
    /// Registers the area's DI services. Called by
    /// <c>builder.Services.AddWebsite(o => o.AddArea&lt;TArea&gt;())</c>.
    /// </summary>
    void ConfigureServices(IServiceCollection services);
}
