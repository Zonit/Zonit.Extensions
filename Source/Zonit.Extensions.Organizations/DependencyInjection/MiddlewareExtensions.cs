using Microsoft.AspNetCore.Builder;
using Zonit.Extensions.Organizations.Middlewares;

namespace Zonit.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseOrganizationsExtension(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<WorkspaceMiddleware>();

        return builder;
    }
}