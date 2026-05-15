using Zonit.Extensions;
using Zonit.Extensions.Website;

namespace Example.Components;

/// <summary>
/// Built-in "home" navigation contributor. The host project owns the index pages
/// (Home / Login) and exposes them as a navigation group so they show up alongside
/// links contributed by feature-area projects.
/// </summary>
public sealed class HomeArea : IWebsiteArea
{
    public string Key => "home";
    public Title DisplayName => new("Home");

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title    = "Overview",
            Order    = 0,
            Children =
            [
                new NavItem
                {
                    Title = "Home",
                    Url   = "/",
                    Match = true,
                },
                new NavItem
                {
                    Title = "Log in",
                    Url   = "/login",
                    Match = false,
                },
            ],
        },
    };
}
