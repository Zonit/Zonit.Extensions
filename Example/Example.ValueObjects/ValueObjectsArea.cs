using Zonit.Extensions;
using Zonit.Extensions.Website;

namespace Example.ValueObjects;

public sealed class ValueObjectsArea : IWebsiteArea
{
    public string Key => "value-objects";
    public Title DisplayName => new("Value objects");

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Value objects",
            Order = 70,
            Children =
            [
                new NavItem { Title = "Strings",  Url = "/vo/strings" },
                new NavItem { Title = "Numbers",  Url = "/vo/numbers" },
                new NavItem { Title = "Identity", Url = "/vo/identity" },
                new NavItem { Title = "Tenancy",  Url = "/vo/tenancy" },
            ],
        },
    };
}
