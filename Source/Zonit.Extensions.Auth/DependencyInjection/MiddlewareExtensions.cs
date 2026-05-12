using Microsoft.AspNetCore.Builder;
using Zonit.Extensions.Auth.Middlewares;

namespace Zonit.Extensions;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Wires the Zonit authentication / authorization middleware pipeline:
    /// <c>UseAuthentication()</c> → <c>UseAuthorization()</c> → <see cref="SessionMiddleware"/>.
    /// </summary>
    /// <remarks>
    /// Order matters. <c>UseAuthentication</c> must run before <c>UseAuthorization</c>, and
    /// <see cref="SessionMiddleware"/> runs after both so that it can hydrate the scoped
    /// repository for downstream Razor components on first request.
    /// </remarks>
    public static IApplicationBuilder UseAuthExtension(this IApplicationBuilder builder)
    {
        builder.UseAuthentication();
        builder.UseAuthorization();
        builder.UseMiddleware<SessionMiddleware>();
        return builder;
    }
}