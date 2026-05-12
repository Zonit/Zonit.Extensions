using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Extensions;

/// <summary>
/// Top-level <c>AddWebsite</c> entry point: aggregates Razor Components hosting,
/// response compression, forwarded headers, antiforgery and registers a set of
/// <see cref="Zonit.Extensions.Website.IWebsiteArea"/>s.
/// </summary>
public static class WebsiteServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Website host with the supplied <paramref name="configure"/> options.
    /// </summary>
    /// <remarks>
    /// <para>What this does:</para>
    /// <list type="bullet">
    ///   <item>Registers <see cref="Zonit.Extensions.Website.WebsiteOptions"/> as a singleton.</item>
    ///   <item>Registers each declared <see cref="Zonit.Extensions.Website.IWebsiteArea"/>
    ///     as a singleton and runs <c>area.ConfigureServices(services)</c>.</item>
    ///   <item>Wires <see cref="Zonit.Extensions.Website.INavigationProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.IBreadcrumbsProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.IToastProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.ICookieProvider"/>.</item>
    ///   <item>Adds Razor Components host with Server / WebAssembly / Auto interactive modes
    ///     according to <see cref="Zonit.Extensions.Website.WebsiteOptions.Mode"/>, and
    ///     <c>AddAdditionalAssemblies(...)</c> for every area's <c>ComponentsAssembly</c>.</item>
    ///   <item>Configures response compression (gzip + brotli) and forwarded-headers
    ///     (when <c>Proxy=true</c>).</item>
    /// </list>
    ///
    /// <para><b>AOT/Trimming:</b> the host is otherwise AOT-safe; areas that bring their
    /// own services may add reflection/dynamic dependencies — annotate them as needed.</para>
    /// </remarks>
    [RequiresUnreferencedCode("Razor Components and Antiforgery use reflection. Components from area assemblies are discovered dynamically.")]
    [RequiresDynamicCode("Razor Components and Antiforgery may emit dynamic code at runtime.")]
    public static IServiceCollection AddWebsite(
        this IServiceCollection services,
        Action<Zonit.Extensions.Website.WebsiteOptions>? configure = null)
    {
        var opts = new Zonit.Extensions.Website.WebsiteOptions();
        configure?.Invoke(opts);

        services.TryAddSingleton(opts);

        // Areas
        foreach (var area in opts.Areas)
        {
            services.AddSingleton(area);
            area.ConfigureServices(services);
        }

        services.AddHttpContextAccessor();

        // Built-in providers
        services.AddNavigationsExtension();
        services.AddBreadcrumbsExtension();
        services.AddToastsExtension();
        services.AddCookiesExtension();

        if (opts.AntiForgery)
            services.AddAntiforgery();

        // Razor Components
        var razor = services.AddRazorComponents();
        if (opts.Mode is Zonit.Extensions.Website.WebsiteMode.Server or Zonit.Extensions.Website.WebsiteMode.Auto)
            razor.AddInteractiveServerComponents();
        // NOTE: Interactive WebAssembly components require
        // "Microsoft.AspNetCore.Components.WebAssembly.Server" package referenced by the host project.
        // Consumers using WebsiteMode.WebAssembly / Auto must call
        //   builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents()
        // themselves, or reference the package and call our overload.
        // We don't reference it here to keep this package usable in pure-Server hosts.

        // Compression
        if (opts.Compression)
        {
            services.AddResponseCompression(o =>
            {
                o.EnableForHttps = true;
                o.Providers.Add<BrotliCompressionProvider>();
                o.Providers.Add<GzipCompressionProvider>();
                o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    "application/javascript",
                    "application/wasm",
                    "image/svg+xml",
                });
            });
            services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        }

        // Forwarded headers (reverse proxy)
        if (opts.Proxy)
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        return services;
    }

    /// <summary>
    /// Wires the runtime middleware pieces (compression / forwarded-headers).
    /// Call <b>before</b> <c>app.UseAntiforgery()</c> / <c>app.MapRazorComponents&lt;App&gt;()</c>.
    /// </summary>
    public static IApplicationBuilder UseWebsite(this IApplicationBuilder app)
    {
        var opts = app.ApplicationServices.GetService<Zonit.Extensions.Website.WebsiteOptions>();
        if (opts is null) return app;

        if (opts.Proxy) app.UseForwardedHeaders();
        if (opts.Compression) app.UseResponseCompression();

        return app;
    }
}
