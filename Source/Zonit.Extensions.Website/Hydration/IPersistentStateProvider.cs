using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Per-domain bridge that transports a scoped state snapshot across the
/// <em>prerender → interactive</em> boundary. The HTTP-request scope that runs the
/// Zonit middleware pipeline (Cookie / Session / Workspace / Project / Culture /
/// Tenant) is <b>not</b> the same DI scope as the SignalR circuit (or WASM client)
/// that owns interactive Blazor components — so without a bridge every scoped
/// repository starts <see langword="null"/> / <see cref="Identity.Empty"/> /
/// default culture / no cookies the moment the user moves from SSR to interactive,
/// even though the SSR pass populated them perfectly.
/// </summary>
/// <remarks>
/// <para><b>Why bridges (not the framework) carry the typed JSON calls.</b>
/// <see cref="PersistentComponentState"/> in .NET 10 only exposes <em>generic</em>
/// <c>PersistAsJson&lt;T&gt;</c> / <c>TryTakeFromJson&lt;T&gt;</c> — no
/// <see cref="Type"/>-accepting overload. A central aggregator that wanted to
/// dispatch by runtime <see cref="Type"/> would have to use
/// <c>MakeGenericMethod</c>, which is AOT-hostile and pulls in reflection
/// metadata. By inverting the responsibility — each bridge calls the generic
/// API directly with its own known <c>T</c> — the framework stays AOT-friendly
/// and the trimmer keeps the exact JSON types each bridge actually uses.</para>
///
/// <para><b>How <see cref="WebsiteHydrator"/> drives the contract.</b> The
/// aggregator component resolves <c>IEnumerable&lt;IPersistentStateProvider&gt;</c>
/// in <c>OnInitialized</c>. For every provider it calls <see cref="Restore"/>
/// first (so any interactive page sees populated scoped repositories on its first
/// render) then <see cref="RegisterPersist"/> to wire the SSR persisting phase.</para>
///
/// <para><b>Registration convention.</b> Each <c>Add{Domain}Extension()</c> method
/// registers its bridge via
/// <c>services.TryAddEnumerable(ServiceDescriptor.Scoped&lt;IPersistentStateProvider, MyBridge&gt;())</c>.
/// Scoped lifetime because the bridge writes into the scoped repository it bridges
/// for (Identity / Workspace state / etc.). One instance per circuit / request — same
/// lifetime as the data it serialises.</para>
///
/// <para><b>Why scoped, not singleton.</b> The bridge holds a reference to the
/// scoped repository (constructor-injected). Making it singleton would freeze the
/// repository instance to whatever the first scope resolved — every subsequent
/// circuit would write through that stale singleton repository and break.</para>
/// </remarks>
public interface IPersistentStateProvider
{
    /// <summary>
    /// Restores the bridge's scoped state from the SSR snapshot embedded in the
    /// response HTML, if one was persisted. Called once, during the interactive
    /// component's <c>OnInitialized</c>. Implementers should:
    /// <list type="bullet">
    ///   <item>Call <see cref="PersistentComponentState.TryTakeFromJson{TValue}"/>
    ///         with the bridge's typed payload.</item>
    ///   <item>Only mutate the underlying repository when the value is meaningful
    ///         (e.g. <see cref="Identity.HasValue"/> is <see langword="true"/>).</item>
    ///   <item>Be idempotent — circuits can re-render, repositories may already
    ///         hold the value.</item>
    /// </list>
    /// </summary>
    void Restore(PersistentComponentState state);

    /// <summary>
    /// Subscribes the bridge to the SSR persisting phase. The framework invokes
    /// the registered callback after prerender finishes and before the HTML hits
    /// the wire; the callback captures the current scoped repository state and
    /// calls <see cref="PersistentComponentState.PersistAsJson{TValue}"/>.
    /// The returned <see cref="PersistingComponentStateSubscription"/> is held by
    /// <see cref="WebsiteHydrator"/> and disposed on component teardown so
    /// hot-reload doesn't stack duplicate callbacks.
    /// </summary>
    PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state);
}
