using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions;
using Zonit.Extensions.Website;

namespace Example;

public sealed class ComponentsArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "components";

    public void ConfigureServices(IServiceCollection services)
    {
        // Demo registration — the plug-in pages reference "Demo.Box" as a string only.
        // The concrete layout type (DemoBoxLayout) lives in this area's assembly; any
        // page in any other assembly can [LayoutKey("Demo.Box")] without referencing it.
        services.AddWebsiteLayout<Example.Layouts.DemoBoxLayout>("Demo.Box");
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Components",
            Order = 60,
            Children =
            [
                new NavItem { Title = "PageViewBase<T>", Url = "/components/page-view" },
                new NavItem { Title = "PageEditBase<T>", Url = "/components/page-edit" },
                new NavItem { Title = "AutoSave",        Url = "/components/auto-save" },
                new NavItem { Title = "Toasts",          Url = "/components/toasts" },
                new NavItem { Title = "Breadcrumbs",     Url = "/components/breadcrumbs" },
                new NavItem { Title = "Cookies",         Url = "/components/cookies" },
                new NavItem { Title = "Layouts",         Url = "/components/layouts" },
                new NavItem { Title = "State extensions", Url = "/components/state-extensions" },
            ],
        },
    };
}
