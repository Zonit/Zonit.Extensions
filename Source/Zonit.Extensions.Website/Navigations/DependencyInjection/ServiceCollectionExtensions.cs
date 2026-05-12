using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Navigations.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionNavigationsExtensions
{
    public static IServiceCollection AddNavigationsExtension(this IServiceCollection services)
    {
        services.TryAddSingleton<INavigationProvider, NavigationService>();
        return services;
    }
}