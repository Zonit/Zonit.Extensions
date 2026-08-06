using System.Collections.Frozen;

namespace Zonit.Extensions.Tenants;

/// <summary>
/// One tenant record — the per-domain "site identity" that <see cref="Settings.Setting{T}"/>
/// are loaded against. Hydrated by the consumer's <see cref="ITenantSource"/> from whatever
/// backing store they choose (DB, file, remote API).
/// </summary>
/// <remarks>
/// <para><b>Lookup performance.</b> <see cref="Variables"/> is a <see cref="FrozenDictionary{TKey,TValue}"/>
/// keyed by <c>setting key</c> (the <see cref="Settings.ISetting.Key"/> value, e.g. <c>"site"</c>,
/// <c>"theme"</c>). The legacy <see cref="List{T}"/>-based representation walked the list
/// linearly on every <c>GetSetting&lt;T&gt;()</c> call — fine for a handful of settings,
/// quadratic for hosts that hydrate many request-scoped <see cref="Settings.ISetting"/>
/// instances per request. Frozen dictionary gives O(1) reads with a one-off build cost.</para>
///
/// <para><b>Immutability.</b> A <see cref="Tenant"/> is conceptually a snapshot; the
/// state machine that mutates it lives in <see cref="ITenantRepository"/> which replaces
/// the whole instance under <c>OnChange</c>.</para>
/// </remarks>
public sealed class Tenant
{
    /// <summary>
    /// Stable identifier, as persisted with the tenant record. <see langword="null"/> for a tenant
    /// that has no identity — see <see cref="Default"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Required and nullable, which is not a contradiction.</b> The two answer different
    /// questions. <c>required</c> means the author of a <see cref="Tenant"/> has to state what the
    /// id is; forgetting it is a compile error rather than a record that quietly carries a
    /// meaningless value. Nullable means "no identity" is sayable at all — which it has to be,
    /// because a single-site host has no store to get an id from.</para>
    ///
    /// <para><b>Why not <see cref="Guid.Empty"/> as the no-identity value.</b> It is a real
    /// <see cref="Guid"/>, so nothing distinguishes "this tenant has no id" from "somebody built a
    /// <see cref="Tenant"/> and forgot the id" — and the second reads as the first at every use
    /// site, including a database lookup that then matches nothing. With this shape
    /// <see cref="Guid.Empty"/> is simply never produced by the framework, and
    /// <c>tenant.Id is null</c> means exactly one thing.</para>
    /// </remarks>
    public required Guid? Id { get; init; }

    /// <summary>
    /// Primary host name this tenant answers to (e.g. <c>"acme.example.com"</c>), or <c>"*"</c>
    /// for <see cref="Solo"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing in the framework matches on this value.</b> Host-to-tenant resolution is
    /// entirely <see cref="ITenantSource.GetByDomainAsync"/>'s job: <c>TenantMiddleware</c> hands
    /// it the raw <c>HttpRequest.Host.Host</c> and stores whatever comes back, so case folding,
    /// aliases, wildcards and <c>www.</c> stripping are all decisions the implementation owns.
    /// The one place the value is read is <c>TenantRepository.InitializeAsync</c>, which compares
    /// it against the host already resolved in this scope to skip a repeat round trip.</para>
    ///
    /// <para>It follows that this field is free-form as far as the framework is concerned — a
    /// source that resolves by something other than host name can put whatever label its store
    /// uses here, as long as it stays stable for a given tenant.</para>
    /// </remarks>
    public required string Domain { get; init; }

    /// <summary>
    /// Persisted <see cref="Settings.ISetting"/> overrides keyed by <see cref="Settings.ISetting.Key"/>.
    /// Values are JSON-serialized model instances; they are deserialised on demand by
    /// <c>Setting&lt;T&gt;.Hydrate(string)</c>. Empty dictionary when the tenant has no
    /// overrides — every setting then surfaces its compile-time defaults.
    /// </summary>
    public FrozenDictionary<string, string> Variables { get; init; }
        = FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// A tenant with no identity: <see langword="null"/> <see cref="Id"/>, <c>"*"</c> as
    /// <see cref="Domain"/>, no overrides. For a single-site host that wants a
    /// <see cref="Tenant"/> instance without inventing an id for it.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing in the framework produces this.</b> "No tenant resolved" is
    /// <see cref="ITenantProvider.Current"/> being <see langword="null"/>, with
    /// <see cref="ITenantProvider.Resolution"/> saying why — the repository never substitutes a
    /// sentinel, and settings do not need one either, since they fall back to configuration and
    /// then to their compile-time defaults on their own.</para>
    ///
    /// <para>It remains available for a host that deliberately wants a non-null
    /// <c>Current</c> in a single-site app:
    /// <c>repository.Initialize(Tenant.Default)</c>. That is a choice, not a default.</para>
    /// </remarks>
    public static readonly Tenant Default = new()
    {
        Id = null,
        Domain = "*",
    };

    /// <summary>True when this tenant has no identity and answers to any host.</summary>
    public bool IsDefault => Id is null && Domain == "*";

    /// <inheritdoc cref="Default"/>
    [Obsolete($"Renamed to {nameof(Default)}. 'Solo' described the single-site host that produced it rather than what the value is. Note that nothing produces it any more: an unresolved scope has a null Current. This alias will be removed in the next preview.")]
    public static readonly Tenant Solo = Default;

    /// <inheritdoc cref="IsDefault"/>
    [Obsolete($"Renamed to {nameof(IsDefault)}. This alias will be removed in the next preview.")]
    public bool IsSolo => IsDefault;
}
