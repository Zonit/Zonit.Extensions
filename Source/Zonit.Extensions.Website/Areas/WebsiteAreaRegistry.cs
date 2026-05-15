using System.Collections.Concurrent;

namespace Zonit.Extensions.Website;

/// <summary>
/// Bridge between build-time <c>AddWebsite(o => o.AddArea&lt;TArea&gt;())</c> (DI side)
/// and middleware-time <c>app.UseWebsite&lt;TApp&gt;(o => o.AddArea&lt;TArea&gt;())</c>
/// (routing side). Holds the singleton instance of every Area registered by type so
/// each Site mount can pull the same instance without re-running
/// <see cref="IWebsiteServices.ConfigureServices"/>.
/// </summary>
/// <remarks>
/// Internal — exposed only through the <c>AddArea&lt;T&gt;</c> facades on
/// <see cref="WebsiteOptions"/> and <see cref="SiteOptions"/>.
/// </remarks>
public sealed class WebsiteAreaRegistry
{
    private readonly ConcurrentDictionary<Type, object> _instances = new();

    /// <summary>
    /// Stores the supplied area instance. Idempotent — registering the same type
    /// twice is a no-op (the first instance wins, matching <c>TryAdd</c> semantics).
    /// </summary>
    public T Register<T>(T instance) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(instance);
        return (T)_instances.GetOrAdd(typeof(T), instance);
    }

    /// <summary>
    /// Returns the singleton instance previously registered for <typeparamref name="T"/>.
    /// Throws when the host calls <c>app.UseWebsite(o => o.AddArea&lt;T&gt;())</c> without
    /// first registering it via <c>builder.Services.AddWebsite(o => o.AddArea&lt;T&gt;())</c>.
    /// </summary>
    public T Resolve<T>() where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var existing))
            return (T)existing;

        throw new InvalidOperationException(
            $"Area '{typeof(T).FullName}' is referenced from app.UseWebsite() but was " +
            $"never registered with builder.Services.AddWebsite(o => o.AddArea<{typeof(T).Name}>()). " +
            $"Add it at services-time so its IWebsiteServices.ConfigureServices runs against " +
            $"the DI container before app.Build().");
    }

    /// <summary>True if an area of <typeparamref name="T"/> was registered.</summary>
    public bool Contains<T>() => _instances.ContainsKey(typeof(T));

    /// <summary>
    /// Enumerates every registered area as <see cref="IWebsiteArea"/>. Used by
    /// <see cref="INavigationProvider"/> to materialise the global navigation map.
    /// </summary>
    public IEnumerable<IWebsiteArea> AsAreas()
        => _instances.Values.OfType<IWebsiteArea>();
}
