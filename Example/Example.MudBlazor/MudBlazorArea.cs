using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Zonit.Extensions.Website;

namespace Example;

/// <summary>
/// Demo area that wires the optional Zonit.Extensions.Website.MudBlazor bridge:
/// MudBlazor services + a sample page bound to Value Objects through
/// <c>ZonitTextField&lt;T&gt;</c> / <c>ZonitTextArea&lt;T&gt;</c>.
/// </summary>
public sealed class MudBlazorArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "mudblazor";

    public void ConfigureServices(IServiceCollection services)
    {
        // MudBlazor's required services (snackbar / dialog / popover providers).
        // Idempotent — if the host already calls AddMudServices() this is a no-op.
        services.AddMudServices();
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "MudBlazor",
            Order = 80,
            Children =
            [
                new NavItem { Title = "VO-bound form", Url = "/mudblazor/forms" },
            ],
        },
    };
}
