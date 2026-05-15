using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Organizations;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Lazily hydrates the scoped <see cref="IWorkspaceManager"/> for authenticated app
/// requests. Skips static assets and anonymous traffic to avoid pointless database
/// round trips on every CSS / JS / framework file in a page navigation.
/// </summary>
/// <remarks>
/// <para><b>Anonymous bypass.</b> Workspace state is per-user. For anonymous requests
/// (login, sign-up, public marketing pages) it is meaningless and previously caused
/// one provider call per request. We now skip when the auth repository is empty —
/// the workspace will hydrate naturally on the first request after the user signs in.</para>
///
/// <para><b>Static asset bypass.</b> Same rationale as the other middlewares; see
/// <see cref="WebsiteRequestFilter"/>.</para>
///
/// <para><b>Pipeline contract.</b> Must run after <see cref="SessionMiddleware"/> so
/// that <see cref="IAuthenticatedRepository.Current"/> reflects the current user.</para>
/// </remarks>
internal sealed class WorkspaceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IAuthenticatedRepository auth,
        IWorkspaceManager workspace)
    {
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
        {
            await next(httpContext);
            return;
        }

        // Anonymous request — workspace state has no meaning for it. Defer hydration
        // until the first request after sign-in.
        if (auth.Current.HasValue && workspace.State is null)
            await workspace.InitializeAsync();

        await next(httpContext);
    }
}
