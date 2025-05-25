using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Navigations.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionNavigationsExtensions
{
    public static IServiceCollection AddNavigationsExtension(this IServiceCollection services)
    {
        services.AddSingleton<INavigationProvider, NavigationService>();

        return services;
    }
}