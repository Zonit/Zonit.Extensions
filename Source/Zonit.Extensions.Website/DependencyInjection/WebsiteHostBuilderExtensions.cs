using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Configuration;
using Zonit.Extensions.Website;

namespace Zonit.Extensions;

/// <summary>
/// Builder-level entry point for the website kernel: configuration sources plus every service
/// <see cref="WebsiteServiceCollectionExtensions.AddWebsite(IServiceCollection, Action{WebsiteOptions}?)"/>
/// registers, in one call.
/// </summary>
/// <remarks>
/// <para><b>Both receivers load <c>AppData/Settings</c>.</b> The
/// <see cref="IServiceCollection"/> overload reaches the host's configuration through the
/// registered <c>ConfigurationManager</c>, so the behaviour does not depend on which one you call.
/// Opt out with <see cref="WebsiteOptions.UseAppData"/>.</para>
///
/// <para>What this overload adds is ergonomics: it returns the concrete builder so the call chains,
/// and it takes the settings-loader options directly, which the services-level overload has no
/// place to put.</para>
/// </remarks>
public static class WebsiteHostBuilderExtensions
{
    /// <summary>
    /// Loads <c>AppData/Settings</c> into configuration and registers the website kernel and every
    /// area declared on <see cref="WebsiteOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>Call before <c>Build()</c>; the first line after <c>CreateBuilder(args)</c> is the
    /// right place, because anything reading configuration during host construction has to see the
    /// files.</para>
    ///
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.AddWebsite(o =>
    /// {
    ///     o.AddArea&lt;WebsiteArea&gt;();
    ///     o.AddArea&lt;DocsArea&gt;();
    /// });
    /// </code>
    ///
    /// <para><c>AddAppData</c> is idempotent, so a host that already called it explicitly — to
    /// override <see cref="AppDataOptions"/>, say — loses nothing by also using this overload; the
    /// earlier call's options stand.</para>
    /// </remarks>
    /// <typeparam name="TBuilder">
    /// The concrete builder type, so the call returns <c>WebApplicationBuilder</c> rather than the
    /// interface and stays chainable.
    /// </typeparam>
    /// <param name="builder">Host builder.</param>
    /// <param name="configure">Website options — areas, kernel behaviour.</param>
    /// <param name="appData">
    /// Settings-loader overrides. Omit for the defaults: <c>AppData/Settings</c>, reload on change,
    /// directory scaffolded in Development.
    /// </param>
    [RequiresUnreferencedCode("Razor Components and Antiforgery use reflection. Components from area assemblies are discovered dynamically.")]
    [RequiresDynamicCode("Razor Components and Antiforgery may emit dynamic code at runtime.")]
    public static TBuilder AddWebsite<TBuilder>(
        this TBuilder builder,
        Action<WebsiteOptions>? configure = null,
        Action<AppDataOptions>? appData = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Only when the caller has settings-loader options to pass: AddWebsite would otherwise do
        // this itself, and AddAppData is idempotent, so going first is how these options win.
        if (appData is not null)
            builder.AddAppData(appData);

        builder.Services.AddWebsite(configure);

        return builder;
    }
}
