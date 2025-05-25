using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Cookies.Services;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions;

public static class ServiceCollectionCookiesExtensions
{
    public static IServiceCollection AddCookiesExtension(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICookieProvider, CookieService>();
        services.AddScoped<ICookiesRepository, CookiesRepository>();

        return services;
    }
}