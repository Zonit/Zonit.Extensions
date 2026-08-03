using System.Collections.Frozen;

namespace Zonit.Extensions.Tenants;

/// <summary>
/// Serialisable mirror of <see cref="Tenant"/>, for transports that have to move a resolved
/// tenant between DI scopes — above all the Blazor <em>prerender → interactive</em> boundary.
/// </summary>
/// <remarks>
/// <para><b>Why the DTO exists.</b> <see cref="Tenant"/> itself cannot round-trip through
/// <c>System.Text.Json</c>: <see cref="Tenant.Variables"/> is a
/// <see cref="FrozenDictionary{TKey,TValue}"/>, which has no public constructor, so
/// deserialisation throws <see cref="NotSupportedException"/>
/// (<c>"…is abstract, an interface, or is read only, and could not be instantiated"</c>).
/// Writing works; reading does not. Anything that persists a tenant therefore has to go
/// through a mutable shape, and shipping one here keeps every host from inventing its own —
/// and from getting the <see cref="Tenant.Variables"/> key casing subtly wrong.</para>
///
/// <para><b>What it is for.</b> <c>ITenantRepository</c> state is per-scope and only
/// <c>TenantMiddleware</c> populates it, so an interactive Blazor circuit — a different scope,
/// which the middleware never runs against — starts with <c>Current == null</c> and renders
/// compile-time defaults ("New website") after a prerender that showed the real tenant. The
/// fix is <c>Zonit.Extensions.Website</c>'s <c>TenantStateBridge</c>, an
/// <c>IPersistentStateProvider</c> that persists <see cref="TenantSnapshot"/> during SSR and
/// calls <see cref="ITenantRepository.Initialize(Tenant?)"/> with <see cref="ToTenant"/> on
/// restore. This type is that bridge's payload; a host that moves a tenant across any other
/// boundary (cache, queue, out-of-process call) can use it for the same reason.</para>
///
/// <para><b>Persisted state is client-visible.</b> The bridge's payload is embedded in the
/// prerendered HTML, so a snapshot handed to it carries every entry of
/// <see cref="Variables"/> to the browser — see <c>TenantStateBridge</c> remarks.</para>
///
/// <para><b>Mutable by design.</b> Public parameterless constructor and settable properties,
/// because that is what <c>System.Text.Json</c> needs on the read side.
/// <see cref="Tenant"/> stays the immutable, frozen-lookup runtime shape.</para>
/// </remarks>
public sealed class TenantSnapshot
{
    /// <summary>Mirrors <see cref="Tenant.Id"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>Mirrors <see cref="Tenant.Domain"/>.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Mirrors <see cref="Tenant.Variables"/> as a plain dictionary. Keys are
    /// <c>ISetting.Key</c> values; entries are the persisted JSON blobs, copied verbatim.
    /// </summary>
    /// <remarks>
    /// <para><b>There is no "dehydrate" API.</b> Nothing on <see cref="Settings.Setting{T}"/>
    /// writes these blobs — the type only reads them, through
    /// <see cref="Settings.Setting{T}.Hydrate(string)"/>. Whoever persists a tenant (admin UI,
    /// seeder, migration) produces each entry itself, and the one rule is to serialise with the
    /// <b>same</b> <c>JsonTypeInfo</c> the matching <c>Hydrate</c> deserialises with: a blob
    /// written with default options is PascalCase, the built-in settings read camelCase, and
    /// System.Text.Json reports the mismatch as "no properties present" rather than as an
    /// error — the override then vanishes into compile-time defaults without a trace.</para>
    ///
    /// <para>For the built-in settings that context is
    /// <see cref="Settings.TenantsJsonContext"/>:</para>
    /// <code>
    /// snapshot.Variables["site"] = JsonSerializer.Serialize(
    ///     model, TenantsJsonContext.Default.SiteSettingsModel);
    /// </code>
    ///
    /// <para>A plugin ships its own context and owns both halves of the round trip — see
    /// <see cref="Settings.Setting{T}"/> for the recipe.</para>
    /// </remarks>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>Captures <paramref name="tenant"/> into a serialisable snapshot.</summary>
    public static TenantSnapshot From(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        return new TenantSnapshot
        {
            Id = tenant.Id,
            Domain = tenant.Domain,
            // Copy rather than alias: the frozen source is shared across the scope, and the
            // snapshot is handed to a serializer that may be running on another thread.
            Variables = new Dictionary<string, string>(tenant.Variables, StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Rebuilds the runtime <see cref="Tenant"/>. Re-freezes <see cref="Variables"/> so the
    /// restored instance has the same O(1) lookup characteristics as one resolved through
    /// <see cref="ITenantSource"/>.
    /// </summary>
    public Tenant ToTenant() => new()
    {
        Id = Id,
        Domain = Domain,
        Variables = Variables.Count == 0
            ? FrozenDictionary<string, string>.Empty
            : Variables.ToFrozenDictionary(StringComparer.Ordinal),
    };
}
