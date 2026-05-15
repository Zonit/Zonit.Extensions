using Example.Auth.Stubs;
using Example.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Website;

namespace Example.Auth;

public sealed class AuthArea : IWebsiteArea
{
    public string Key => "auth";
    public Title DisplayName => new("Auth");

    /// <summary>
    /// Registers the demo's auth-side data adapters. A real consumer wires their own
    /// <see cref="IAuthSource"/> / <see cref="IUserDirectory"/> backed by EF or a
    /// remote API; <c>TryAdd*</c> means our stubs only fill the gap when the host hasn't
    /// already done so.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<DemoStore>();

        // One demo class implements BOTH consumer contracts (required IAuthSource +
        // optional IUserDirectory). Each interface points at the same scoped instance
        // so login + token lookup + admin search all hit the same in-memory store.
        services.TryAddScoped<InMemoryAuthSource>();
        services.TryAddScoped<IAuthSource>(sp => sp.GetRequiredService<InMemoryAuthSource>());
        services.TryAddScoped<IUserDirectory>(sp => sp.GetRequiredService<InMemoryAuthSource>());
    }

    public IReadOnlyList<NavGroup> Navigation { get; } = new[]
    {
        new NavGroup
        {
            Title = "Auth",
            Order = 20,
            Children =
            [
                new NavItem { Title = "Identity",         Url = "/auth" },
                new NavItem { Title = "AuthorizeView",    Url = "/auth/authorize-view" },
                new NavItem { Title = "[RequirePermission]", Url = "/auth/require-permission" },
            ],
        },
    };
}
