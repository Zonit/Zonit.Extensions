using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Auth.Repositories;

namespace Zonit.Extensions.Auth.Middlewares;

/// <summary>
/// Reads the <c>Session</c> cookie and hydrates the scoped <see cref="IAuthenticatedRepository"/>
/// with an <see cref="Identity"/> on the first request of the scope.
/// </summary>
internal sealed class SessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ISessionProvider session,
        IAuthenticatedRepository repository)
    {
        // Skip work if the repository is already populated for this scope.
        if (!repository.Current.HasValue)
        {
            var sessionValue = httpContext.Request.Cookies["Session"];
            if (!string.IsNullOrEmpty(sessionValue))
            {
                var identity = await session.GetByTokenAsync(sessionValue, httpContext.RequestAborted);
                if (identity.HasValue)
                    repository.Initialize(identity);
            }
        }

        await next(httpContext);
    }
}