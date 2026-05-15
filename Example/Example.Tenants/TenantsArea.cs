using Example.Tenants.Stubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Website;

namespace Example.Tenants;

public sealed class TenantsArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "tenants";

    /// <summary>
    /// <para>Registers an in-memory <see cref="ITenantSource"/>. This is intentionally
    /// OPTIONAL: comment the call below out (or remove this area entirely) and
    /// <c>TenantMiddleware</c> falls back to <see cref="Tenant.Solo"/> for every request.</para>
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<ITenantSource, InMemoryTenantManager>();
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Tenants",
            Order = 50,
            Children =
            [
                new NavItem { Title = "Settings", Url = "/tenants" },
                new NavItem { Title = "Solo mode", Url = "/tenants/solo" },
            ],
        },
    };
}
