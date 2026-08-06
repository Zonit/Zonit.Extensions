using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Tenants;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the resolved <see cref="Tenant"/> across the prerender → interactive boundary.
/// Without it a Blazor Server circuit starts with <see cref="ITenantRepository.Current"/>
/// <see langword="null"/>, so every <c>TenantSettings</c> accessor falls back to its
/// compile-time default — <c>Settings.Site.Title</c> renders the real tenant's title during
/// prerender and flips to <c>"New website"</c> the moment the circuit takes over.
/// </summary>
/// <remarks>
/// <para><b>Why the circuit is empty in the first place.</b> <c>ITenantRepository</c> is
/// scoped and only <c>TenantMiddleware</c> writes to it. The middleware runs on the HTTP
/// request scope; the SignalR circuit is a different scope the middleware never sees. Every
/// other scoped repository in this framework has the same problem and the same fix — see
/// <see cref="IPersistentStateProvider"/>.</para>
///
/// <para><b>Why <see cref="TenantSnapshot"/> and not <see cref="Tenant"/>.</b>
/// <see cref="Tenant.Variables"/> is a <c>FrozenDictionary</c>, which has no public
/// constructor: writing works, reading throws <see cref="NotSupportedException"/>. The DTO is
/// the mutable mirror System.Text.Json needs on the read side.</para>
///
/// <para><b>Solo tenants are persisted too.</b> Unlike <c>AuthStateBridge</c>, which skips
/// <c>Identity.Empty</c> because the circuit already starts there, the circuit does <i>not</i>
/// start at <see cref="Tenant.Solo"/> — it starts at <see langword="null"/>, and the two are
/// distinguishable through <c>ITenantProvider.Current</c>. Persisting the sentinel (~80 bytes)
/// is what makes the interactive pass observe exactly what the prerender observed.</para>
///
/// <para><b>The payload is visible to the client.</b> Persisted component state is embedded in
/// the prerendered HTML, so everything in <see cref="Tenant.Variables"/> — the whole settings
/// blob, not just the values a page happens to render — travels to the browser in clear text.
/// That is inherent to <see cref="PersistentComponentState"/> and applies to every bridge here,
/// but tenant variables are the one payload a host is likely to fill with server-side
/// configuration. Keep credentials out of <see cref="Tenant.Variables"/>; they belong in
/// <c>IConfiguration</c> / a secret store, which the circuit can reach directly.</para>
///
/// <para><b>Trimming.</b> The persisted graph is one POCO
/// (<see cref="TenantSnapshot"/> → <see cref="Guid"/>, <see cref="string"/>,
/// <c>Dictionary&lt;string, string&gt;</c>), rooted by an explicit
/// <see cref="DynamicDependencyAttribute"/> on the methods below.</para>
///
/// <para><b>Native AOT / PublishTrimmed.</b> See the shared note on
/// <see cref="IPersistentStateProvider"/>: both calls are gated on
/// <see cref="JsonSerializer.IsReflectionEnabledByDefault"/>.</para>
/// </remarks>
internal sealed class TenantStateBridge(ITenantRepository repository) : IPersistentStateProvider
{
    /// <summary>
    /// Persistence key. Follows the <c>Zonit{Domain}Extension</c> convention the other five
    /// bridges use; there is no legacy component to stay compatible with — tenants never had
    /// a hydration bridge before 10.0.0-preview.10.
    /// </summary>
    private const string Key = "ZonitTenantsExtension";

    private const DynamicallyAccessedMemberTypes JsonMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    [DynamicDependency(JsonMembers, typeof(TenantSnapshot))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "TenantSnapshot is the only type the reflective binder walks here and it is rooted by the [DynamicDependency] on this method; its three properties are Guid, string and Dictionary<string, string>, all handled by built-in converters.")]
    public void Restore(PersistentComponentState state)
    {
        if (!JsonSerializer.IsReflectionEnabledByDefault)
            return;

        // Idempotent by construction: TryTakeFromJson removes the entry, so a second Restore
        // on the same state finds nothing. On the SSR pass there is nothing to take at all,
        // so the tenant the middleware resolved is never overwritten.
        if (state.TryTakeFromJson<TenantSnapshot>(Key, out var restored) && restored is not null)
            repository.Initialize(restored.ToTenant());
    }

    [DynamicDependency(JsonMembers, typeof(TenantSnapshot))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore above.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (!JsonSerializer.IsReflectionEnabledByDefault)
                return Task.CompletedTask;

            // Nothing to carry when the SSR pass resolved no tenant: the circuit starts at null
            // too, so persisting "null" would only spend bytes to restore the state it already
            // has. Settings are unaffected either way — they fall back to configuration and then
            // to compile-time defaults, in the circuit exactly as during prerender.
            if (repository.Current is { } tenant)
                state.PersistAsJson(Key, TenantSnapshot.From(tenant));

            return Task.CompletedTask;
        });
}
