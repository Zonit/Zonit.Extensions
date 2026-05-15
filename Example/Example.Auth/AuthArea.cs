using Example.Auth.Stubs;
using Example.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Website;

namespace Example.Auth;

public sealed class AuthArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "auth";

    /// <summary>
    /// Wires the area's HTTP endpoints into the Site's branch. Endpoints inherit the
    /// Site's <c>PathBase</c>, so mounting <see cref="AuthArea"/> under a sub-Site
    /// (e.g. <c>/admin</c>) automatically exposes <c>POST /admin/auth/login</c>.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login",  DemoLoginService.LoginAsync);
        endpoints.MapPost("/auth/logout", DemoLoginService.LogoutAsync);
    }

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
