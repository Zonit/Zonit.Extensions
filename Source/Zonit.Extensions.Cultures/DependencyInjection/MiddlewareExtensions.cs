using Microsoft.AspNetCore.Builder;
using Zonit.Extensions.Cultures.Middlewares;

namespace Zonit.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCulturesExtension(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<CultureMiddleware>();

        return builder;
    }
}