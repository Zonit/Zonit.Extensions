using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Cultures.Repositories;
using Zonit.Extensions.Cultures.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Cultures extension: <see cref="CultureOption"/> binding, translation
    /// repositories (singletons, thread-safe via <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>),
    /// language registry (<see cref="ILanguageProvider"/>) and per-scope culture state
    /// (<see cref="ICultureState"/> / <see cref="ICultureManager"/>).
    /// </summary>
    public static IServiceCollection AddCulturesExtension(
        this IServiceCollection services,
        Action<CultureOption>? options = null)
    {
        services.AddOptions<CultureOption>().BindConfiguration("Culture");
        if (options is not null)
            services.PostConfigure(options);

        // Translation registries — shared across the process.
        services.TryAddSingleton<TranslationRepository>();
        services.TryAddSingleton<DefaultTranslationRepository>();
        services.TryAddSingleton<MissingTranslationRepository>();

        services.TryAddSingleton<ITranslationManager, TranslationService>();
        services.TryAddSingleton<ILanguageProvider, LanguageService>();
        services.TryAddSingleton<DetectCultureService>();

        // Per-scope culture state: register ONE instance and expose under three contracts
        // so injecting any of them resolves to the same scoped object — required for
        // OnChange propagation between writers (manager) and readers (state / provider).
        services.TryAddScoped<CultureStateService>();
        services.TryAddScoped<ICultureState>(sp => sp.GetRequiredService<CultureStateService>());
        services.TryAddScoped<ICultureManager>(sp => sp.GetRequiredService<CultureStateService>());

        services.TryAddScoped<ICultureProvider, CultureService>();

        return services;
    }
}