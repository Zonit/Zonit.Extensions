using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Zonit.Extensions.Website;

public static class DataProtectionExtensions
{
    public static IServiceCollection AddDataProtectionWithAutoAppName(
        this IServiceCollection services)
    {
        // Opcja 1: Użyj AppContext.BaseDirectory (najlepsze)
        var keyPath = Path.Combine(AppContext.BaseDirectory, "keys");

        // Opcja 2: Alternatywnie można użyć Environment.CurrentDirectory
        // var keyPath = Path.Combine(Environment.CurrentDirectory, "keys");

        // Automatyczne wykrycie nazwy aplikacji
        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "ZonitApp";

        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName(appName);

        return services;
    }
}
