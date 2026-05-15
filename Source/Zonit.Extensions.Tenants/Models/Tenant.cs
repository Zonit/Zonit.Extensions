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
    /// <summary>Stable identifier. Persisted with the tenant record.</summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Primary host name this tenant answers to (e.g. <c>"acme.example.com"</c>). The
    /// middleware (<c>TenantMiddleware</c>) compares the request's host (from
    /// <c>HttpRequest.Host.Host</c>) against this value (case-insensitive) to resolve
    /// which tenant applies.
    /// </summary>
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
    /// Single-site sentinel for hosts that don't need multi-domain tenancy. Carries
    /// <see cref="Guid.Empty"/> as <see cref="Id"/> and <c>"*"</c> as <see cref="Domain"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>When it is used.</b> The web middleware falls back to this value when
    /// either:</para>
    /// <list type="bullet">
    ///   <item>the application did not register an <see cref="ITenantSource"/> at all
    ///         (single-site app — no need for per-domain resolution), or</item>
    ///   <item>the registered manager returned <see langword="null"/> for the current
    ///         host (unknown / unconfigured domain in a multi-site host).</item>
    /// </list>
    /// <para>The result: <see cref="ITenantProvider.Current"/> is never <see langword="null"/>
    /// in normal request flow, and <c>Settings.Site.Title</c> &amp; co. always surface
    /// either persisted overrides or the compile-time defaults — no null-checking on
    /// every page.</para>
    /// </remarks>
    public static readonly Tenant Solo = new()
    {
        Id = Guid.Empty,
        Domain = "*",
    };

    /// <summary>True when this is the <see cref="Solo"/> sentinel.</summary>
    public bool IsSolo => Id == Guid.Empty && Domain == "*";
}
