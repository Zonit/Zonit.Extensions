namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// What the hydration host does when the runtime cannot serialise persistent component
/// state — i.e. when <see cref="HydrationSerialization.IsAvailable"/> is
/// <see langword="false"/>.
/// </summary>
/// <remarks>
/// This is <em>not</em> a switch that turns hydration on or off. Hydration is decided by the
/// runtime: <see cref="System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"/> is off
/// for every <c>PublishTrimmed</c> publish (<c>PublishAot</c> included), and .NET 10 offers no
/// <c>JsonTypeInfo</c> seam on <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/>
/// for the key-addressed API the bridges use. All this enum controls is <b>how loudly the loss
/// is reported</b>.
/// </remarks>
public enum HydrationUnavailableBehavior
{
    /// <summary>
    /// Log one <c>Warning</c> per bridge type (deduplicated for the lifetime of the process) and
    /// keep rendering without the prerendered state. The default: the app still works, but the
    /// operator is told that Identity / culture / workspace / catalog / cookie state is not
    /// crossing the prerender → interactive boundary.
    /// </summary>
    Warn = 0,

    /// <summary>
    /// Throw <see cref="System.InvalidOperationException"/> from the hydration host the first
    /// time it renders. Choose this when running without hydrated state is worse than not
    /// starting at all — e.g. an app whose authorization depends on the restored
    /// <see cref="Identity"/>.
    /// </summary>
    Throw = 1,

    /// <summary>
    /// Report nothing. Only for hosts that deliberately publish trimmed <em>and</em> have
    /// accepted that every interactive circuit starts from a cold scoped state.
    /// </summary>
    Silent = 2,
}

/// <summary>
/// Configuration for the prerender → interactive state handoff. Bind it the ordinary way —
/// no <c>AddWebsite</c> change is needed:
/// <code>
/// services.Configure&lt;HydrationOptions&gt;(o =&gt;
///     o.WhenSerializationUnavailable = HydrationUnavailableBehavior.Throw);
/// </code>
/// </summary>
/// <remarks>
/// When the options type is not registered at all, <see cref="WebsiteHydrator"/> falls back to a
/// default-constructed instance, so the <see cref="HydrationUnavailableBehavior.Warn"/> behaviour
/// is what an unconfigured app gets.
/// </remarks>
public sealed class HydrationOptions
{
    /// <summary>
    /// How to report that persistent-state serialisation is unavailable for this build.
    /// Defaults to <see cref="HydrationUnavailableBehavior.Warn"/>.
    /// </summary>
    public HydrationUnavailableBehavior WhenSerializationUnavailable { get; set; }
        = HydrationUnavailableBehavior.Warn;
}
