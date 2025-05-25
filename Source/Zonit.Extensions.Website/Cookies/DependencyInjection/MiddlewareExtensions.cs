using Microsoft.AspNetCore.Builder;
using Zonit.Extensions.Website.Cookies.Middlewares;

namespace Zonit.Extensions;

public static class MiddlewareCookiesExtensions
{
    public static IApplicationBuilder UseCookiesExtension(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<CookieMiddleware>();

        return builder;
    }
}