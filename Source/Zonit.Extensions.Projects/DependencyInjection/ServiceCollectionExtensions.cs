using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Projects;
using Zonit.Extensions.Projects.Repositories;
using Zonit.Extensions.Projects.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsExtension(this IServiceCollection services)
    {
        services.TryAddScoped<ICatalogManager, CatalogRepository>();
        services.TryAddScoped<ICatalogProvider, CatalogService>();

        return services;
    }
}