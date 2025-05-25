using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Breadcrumbs.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionBreadcrumbsExtensions
{
    public static IServiceCollection AddBreadcrumbsExtension(this IServiceCollection services)
    {
        services.AddScoped<IBreadcrumbsProvider, BreadcrumbsService>();

        return services;
    }
}