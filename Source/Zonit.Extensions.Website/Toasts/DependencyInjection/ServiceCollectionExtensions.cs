using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Toasts.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionToastsExtensions
{
    public static IServiceCollection AddToastsExtension(this IServiceCollection services)
    {
        services.TryAddTransient<IToastProvider, ToastService>();

        return services;
    }
}