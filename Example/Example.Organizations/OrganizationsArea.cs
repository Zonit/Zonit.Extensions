using Example.Organizations.Stubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Website;

namespace Example.Organizations;

public sealed class OrganizationsArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "organizations";

    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IOrganizationSource, InMemoryUserOrganizationManager>();
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Organizations",
            Order = 30,
            Children =
            [
                new NavItem { Title = "Workspace", Url = "/organizations" },
                new NavItem { Title = "Switcher",  Url = "/organizations/switcher" },
            ],
        },
    };
}
