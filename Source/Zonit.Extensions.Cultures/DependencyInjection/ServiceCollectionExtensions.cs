using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Cultures.Repositories;
using Zonit.Extensions.Cultures.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configuration section bound onto <see cref="CultureOption"/> when the host has an
    /// <see cref="IConfiguration"/>.
    /// </summary>
    private const string ConfigurationSection = "Culture";

    /// <summary>
    /// Registers the Cultures extension: <see cref="CultureOption"/> binding, translation
    /// repositories (singletons, thread-safe via <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>),
    /// language registry (<see cref="ILanguageProvider"/>) and per-scope culture state
    /// (<see cref="ICultureState"/> / <see cref="ICultureManager"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>No hard dependency on <see cref="IConfiguration"/>.</b> The <c>"Culture"</c>
    /// section is bound when the host has configuration and skipped when it does not, so this
    /// package keeps working in the framework-agnostic containers it exists for — console apps,
    /// unit tests, minimal hosts — where the only configuration is the
    /// <paramref name="options"/> delegate.</para>
    /// </remarks>
    /// <param name="services">Container to register into.</param>
    /// <param name="options">
    /// Code configuration, applied as a post-configure step so it wins over the bound
    /// <c>"Culture"</c> section.
    /// </param>
    public static IServiceCollection AddCulturesExtension(
        this IServiceCollection services,
        Action<CultureOption>? options = null)
    {
        services.AddOptions<CultureOption>();

        // Deliberately NOT OptionsBuilder.BindConfiguration("Culture"): that helper resolves
        // IConfiguration with GetRequiredService, which turns configuration into a hard
        // requirement of a package whose whole point is being host-agnostic. A bare
        // `new ServiceCollection().AddCulturesExtension(o => …)` then dies with
        // "No service for type 'IConfiguration' has been registered" the moment anything reads
        // IOptions<CultureOption> — and everything here does. The two descriptors below keep
        // the exact same behaviour when configuration IS present (bind + reload) and degrade to
        // "defaults plus the code delegate" when it is not. The decision is made at resolve
        // time, not registration time, so it does not matter whether the host adds its
        // IConfiguration before or after this call.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<CultureOption>, CultureOptionConfiguration>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOptionsChangeTokenSource<CultureOption>, CultureOptionChangeTokenSource>());

        if (options is not null)
            services.PostConfigure(options);

        // Translation registries — shared across the process.
        services.TryAddSingleton<TranslationRepository>();
        services.TryAddSingleton<DefaultTranslationRepository>();

        // The missing-key store is the only bounded one: its keys are arbitrary Translate()
        // arguments, so it needs a ceiling to stay a diagnostic buffer rather than an
        // input-driven leak. Constructed from options instead of by type so the ceiling is
        // configurable; resolution throws if the configured value is not positive.
        services.TryAddSingleton(sp => new MissingTranslationRepository(
            sp.GetRequiredService<IOptions<CultureOption>>().Value.MaxTrackedMissingTranslations));

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

    /// <summary>
    /// Binds the <c>"Culture"</c> configuration section onto <see cref="CultureOption"/> when the
    /// host registered an <see cref="IConfiguration"/>, and does nothing when it did not.
    /// </summary>
    /// <remarks>
    /// A named type rather than an inline delegate so the registration can go through
    /// <c>TryAddEnumerable</c> and stay idempotent across repeated
    /// <c>AddCulturesExtension()</c> calls (<c>AddWebsite</c> makes one of them).
    /// </remarks>
    private sealed class CultureOptionConfiguration(IServiceProvider services)
        : IConfigureOptions<CultureOption>
    {
        /// <inheritdoc />
        public void Configure(CultureOption options)
        {
            var section = services.GetService<IConfiguration>()?.GetSection(ConfigurationSection);

            if (section is null)
                return;

            // ConfigurationBinder APPENDS to a collection that already holds items rather than
            // replacing it — deliberate framework behaviour ("binding to array has to preserve
            // already initialized arrays with values"), and the source-generated binder emits the
            // same Array.Resize + copy. Since SupportedCultures ships with 17 defaults, binding a
            // two-entry section straight onto it yields 19 entries with two duplicates: a host
            // could ADD a language but never NARROW the allow-list that CultureMiddleware,
            // SetCulture and the language picker all read. Blanking the defaults first makes the
            // section REPLACE them, which is what every scalar in the same section already does.
            var defaults = options.SupportedCultures;

            if (section.GetSection(nameof(CultureOption.SupportedCultures)).GetChildren().Any())
                options.SupportedCultures = [];

            section.Bind(options);

            // Restore instead of shipping an empty allow-list. Both `"SupportedCultures": []` and
            // a list of non-scalar entries bind to nothing, and a culture set that no request can
            // ever match is not the intended reading of either — it would strand every request on
            // the middleware's hardcoded "en-us" fallback.
            if (options.SupportedCultures is not { Length: > 0 })
                options.SupportedCultures = defaults;
        }
    }

    /// <summary>
    /// Propagates configuration reloads to <see cref="IOptionsMonitor{TOptions}"/> consumers,
    /// exactly as <c>BindConfiguration</c> would — and yields a token that never fires when the
    /// host has no <see cref="IConfiguration"/> to reload.
    /// </summary>
    private sealed class CultureOptionChangeTokenSource(IServiceProvider services)
        : IOptionsChangeTokenSource<CultureOption>
    {
        /// <inheritdoc />
        public string Name => Microsoft.Extensions.Options.Options.DefaultName;

        /// <inheritdoc />
        public IChangeToken GetChangeToken()
            => services.GetService<IConfiguration>()?.GetSection(ConfigurationSection).GetReloadToken()
               ?? NeverChangeToken.Instance;
    }

    /// <summary>
    /// <see cref="IChangeToken"/> that never signals — the honest answer for "there is no
    /// configuration behind these options, so nothing can reload them".
    /// </summary>
    /// <remarks>
    /// Hand-rolled because <c>Microsoft.Extensions.Primitives.NullChangeToken</c> is internal to
    /// that assembly. <see cref="ActiveChangeCallbacks"/> is <see langword="false"/> so
    /// <see cref="IOptionsMonitor{TOptions}"/> knows not to expect a callback rather than holding
    /// a registration that will never fire.
    /// </remarks>
    private sealed class NeverChangeToken : IChangeToken
    {
        /// <summary>Shared instance — the type is stateless.</summary>
        public static readonly NeverChangeToken Instance = new();

        /// <inheritdoc />
        public bool HasChanged => false;

        /// <inheritdoc />
        public bool ActiveChangeCallbacks => false;

        /// <inheritdoc />
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
            => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
