using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Website.Cookies.Repositories;
using Zonit.Extensions.Website.Abstractions.Cookies.Models;

namespace Zonit.Extensions.Website.Cookies.Middlewares;

internal class CookieMiddleware(RequestDelegate next)
{
    readonly RequestDelegate _next = next;

    public Task Invoke(HttpContext httpContext, ICookiesRepository cookie)
    {
        var cookies = cookie.GetCookies();

        foreach (var c in httpContext.Request.Cookies)
            cookies.Add(new CookieModel { 
                Name = c.Key,
                Value = c.Value,
            });

        return _next(httpContext);
    }
}