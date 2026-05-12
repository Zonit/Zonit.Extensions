using Microsoft.AspNetCore.Builder;
using Zonit.Extensions.Projects.Middlewares;

namespace Zonit.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseProjectsExtension(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<ProjectMiddleware>();

        return builder;
    }
}