using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Toasts.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionToastsExtensions
{
    /// <summary>
    /// Registers <see cref="IToastProvider"/> as a scoped service. Scoped lifetime
    /// is mandatory: each Blazor circuit (or HTTP request) needs its own queue
    /// so a single toast-host component can render all notifications raised
    /// anywhere in the page.
    /// </summary>
    public static IServiceCollection AddToastsExtension(this IServiceCollection services)
    {
        services.TryAddScoped<IToastProvider, ToastService>();

        return services;
    }
}