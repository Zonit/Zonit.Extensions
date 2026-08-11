using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Zonit.Extensions.Tenants;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// Registration and endpoints for sitemap generation.
/// </summary>
public static class SitemapExtensions
{
    /// <summary>
    /// Registers the sitemap generator. Sources register themselves from the areas that own them.
    /// </summary>
    /// <remarks>
    /// <code>
    /// builder.Services.AddWebsite(w => w.AddArea&lt;NewsArea&gt;());
    /// builder.Services.AddSitemap();               // no source list — see below
    ///
    /// app.UseWebsite("/", o => o.Indexing());      // robots.txt + sitemap.xml + llms.txt
    /// </code>
    ///
    /// <para><c>Indexing()</c> maps the sitemap and advertises it in <c>robots.txt</c> in one
    /// call — see <see cref="IndexingExtensions.Indexing"/>. Call <see cref="MapSitemap"/>
    /// directly only to publish a sitemap that <c>robots.txt</c> should not name.</para>
    ///
    /// <para>The area contributes its maps in its own <c>ConfigureServices</c>:</para>
    /// <code>
    /// public void ConfigureServices(IServiceCollection services)
    ///     => services.AddSitemapSource&lt;NewsSitemap&gt;();
    /// </code>
    ///
    /// <para>So installing a plug-in adds its URLs and removing it removes them, with no list in
    /// the host to keep in step.</para>
    /// </remarks>
    public static IServiceCollection AddSitemap(
        this IServiceCollection services,
        Action<SitemapOptions>? configure = null)
    {
        var options = new SitemapOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IOptions<SitemapOptions>>(new OptionsWrapper<SitemapOptions>(options));
        services.TryAddSingleton<SitemapCache>();
        services.TryAddScoped<SitemapGenerator>();

        // The pages a project used to hand-write as a three-entry ISitemapSource are now the
        // [Sitemap] attributes themselves, collected at build time. TryAddEnumerable keyed on the
        // implementation type, so calling AddSitemap() twice does not list every page twice.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ISitemapSource, StaticPageSitemapSource>());

        return services;
    }

    /// <summary>
    /// Maps <c>/sitemap.xml</c> (the index) and <c>/sitemap/{name}.xml</c> (the parts) inside the
    /// current Site's branch, so both inherit its path base.
    /// </summary>
    /// <remarks>
    /// Called from <c>SiteOptions.MapEndpoints</c> rather than mounted globally: which Sites
    /// publish a sitemap is a per-mount decision, and a panel publishing one would advertise
    /// exactly the URLs it is trying to keep out of search.
    /// </remarks>
    public static IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder endpoints)
    {
        // RequestDelegate overloads, not the Delegate ones: minimal-API parameter binding
        // reflects over the handler signature and is neither trim- nor AOT-safe.
        //
        // No language guard is needed: both paths carry a skipped extension, so CultureMiddleware
        // answers /pl/sitemap.xml with 404 before routing runs and a prefixed request never
        // reaches the generator. GET+HEAD for the same reason as robots.txt — an unmatched HEAD
        // reads as "no sitemap here" to anything that probes before it fetches.
        endpoints.MapMethods("/sitemap.xml", IndexingEndpoints.GetAndHead, async context =>
        {
            var set = await ResolveAsync(context);
            await WriteAsync(context, set.Index);
        }).AllowAnonymous().ExcludeFromDescription();

        endpoints.MapMethods("/sitemap/{name}.xml", IndexingEndpoints.GetAndHead, async context =>
        {
            var name = context.Request.RouteValues["name"] as string;
            var set = await ResolveAsync(context);

            if (name is null || !set.Files.TryGetValue(name, out var file))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await WriteAsync(context, file.Xml);
        }).AllowAnonymous().ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<SitemapSet> ResolveAsync(HttpContext context)
    {
        var cache = context.RequestServices.GetRequiredService<SitemapCache>();
        var generator = context.RequestServices.GetRequiredService<SitemapGenerator>();
        var options = context.RequestServices.GetRequiredService<IOptions<SitemapOptions>>().Value;

        return await cache.GetOrCreateAsync(
            Origin(context),
            options.CacheDuration,
            token => generator.GenerateAsync(Origin(context), token),
            context.RequestAborted);
    }

    /// <summary>
    /// Tenant setting, then the request — the same order the canonical tag uses, so a URL in the
    /// sitemap and the canonical on the page it points at cannot name different hosts.
    /// </summary>
    private static string Origin(HttpContext context)
    {
        var tenant = context.RequestServices.GetService<ITenantProvider>();
        var configured = tenant?.Settings.Site.CanonicalUrl;

        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        return $"{context.Request.Scheme}://{context.Request.Host.Value}";
    }

    private static Task WriteAsync(HttpContext context, string xml)
    {
        context.Response.ContentType = "application/xml; charset=utf-8";
        return context.Response.WriteAsync(xml, context.RequestAborted);
    }
}

/// <summary>
/// Holds generated sitemaps per origin for a bounded time.
/// </summary>
/// <remarks>
/// <para>Generation walks every source, which on a large site is real database work, and an
/// uncached sitemap endpoint is a free denial-of-service primitive: anyone can request it in a
/// loop. Crawlers, meanwhile, re-read it far less often than the content behind it changes.</para>
///
/// <para>Concurrent first requests for the same origin share one generation rather than starting
/// several — the point of caching an expensive walk is lost if a cold cache under load runs it
/// once per caller.</para>
/// </remarks>
public sealed class SitemapCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the cached set for <paramref name="origin"/>, generating it if stale.</summary>
    public async Task<SitemapSet> GetOrCreateAsync(
        string origin,
        TimeSpan duration,
        Func<CancellationToken, Task<SitemapSet>> factory,
        CancellationToken cancellationToken)
    {
        var entry = _entries.GetOrAdd(origin, _ => new Entry());
        return await entry.GetAsync(duration, factory, cancellationToken);
    }

    /// <summary>
    /// Drops every cached sitemap, so the next request regenerates.
    /// </summary>
    /// <remarks>
    /// For the case the cache duration cannot cover: content published and needing to be
    /// discoverable now. Call it from whatever already knows a publish happened.
    /// </remarks>
    public void Invalidate() => _entries.Clear();

    private sealed class Entry
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private SitemapSet? _value;
        private DateTimeOffset _expires;

        public async Task<SitemapSet> GetAsync(
            TimeSpan duration,
            Func<CancellationToken, Task<SitemapSet>> factory,
            CancellationToken cancellationToken)
        {
            if (_value is not null && DateTimeOffset.UtcNow < _expires)
                return _value;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                // Re-checked inside the gate: everyone who queued behind the first caller would
                // otherwise regenerate in turn, which is the stampede the gate exists to stop.
                if (_value is not null && DateTimeOffset.UtcNow < _expires)
                    return _value;

                _value = await factory(cancellationToken);
                _expires = DateTimeOffset.UtcNow.Add(duration);
                return _value;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
