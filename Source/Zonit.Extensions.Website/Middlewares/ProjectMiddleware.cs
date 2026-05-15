using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Lazily hydrates the scoped <see cref="ICatalogManager"/> for authenticated app
/// requests. Skips static assets and anonymous traffic for the same reasons as
/// <see cref="WorkspaceMiddleware"/>.
/// </summary>
internal sealed class ProjectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IAuthenticatedRepository auth,
        ICatalogManager catalog)
    {
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
        {
            await next(httpContext);
            return;
        }

        if (auth.Current.HasValue && catalog.State is null)
            await catalog.InitializeAsync();

        await next(httpContext);
    }
}
