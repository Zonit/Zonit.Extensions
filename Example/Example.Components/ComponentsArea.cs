using Zonit.Extensions;
using Zonit.Extensions.Website;

namespace Example.Components;

public sealed class ComponentsArea : IWebsiteArea
{
    public string Key => "components";

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
            ],
        },
    };
}
