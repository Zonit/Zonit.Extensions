using Zonit.Extensions;
using Zonit.Extensions.Website;

namespace Example.Cultures;

/// <summary>
/// Demo area exercising <see cref="Zonit.Extensions.Cultures.ICultureProvider"/>
/// / <see cref="Zonit.Extensions.Cultures.ICultureManager"/> — translation, time-zone
/// conversion, language switching.
/// </summary>
public sealed class CulturesArea : IWebsiteArea
{
    public string Key => "cultures";

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Cultures",
            Order = 10,
            Children =
            [
                new NavItem { Title = "Index",       Url = "/cultures" },
                new NavItem { Title = "Translation", Url = "/cultures/translation" },
                new NavItem { Title = "Time zone",   Url = "/cultures/time-zone" },
            ],
        },
    };
}
