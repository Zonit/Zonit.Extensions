using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Organizations.Repositories;
using Zonit.Extensions.Organizations.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationsExtension(this IServiceCollection services)
    {
        services.TryAddScoped<IWorkspaceManager, WorkspaceRepository>();
        services.TryAddScoped<IWorkspaceProvider, WorkspaceService>();

        return services;
    }
}