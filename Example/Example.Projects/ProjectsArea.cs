using Example.Projects.Stubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;
using Zonit.Extensions.Projects;
using Zonit.Extensions.Website;

namespace Example.Projects;

public sealed class ProjectsArea : IWebsiteArea
{
    public string Key => "projects";
    public Title DisplayName => new("Projects");

    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IProjectSource, InMemoryOrganizationProjectManager>();
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Projects",
            Order = 40,
            Children =
            [
                new NavItem { Title = "Catalog",  Url = "/projects" },
                new NavItem { Title = "Switcher", Url = "/projects/switcher" },
            ],
        },
    };
}
