using System.Text.Json;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// The shared JSON contract, exposed so the <b>write</b> side can use the exact rules the read
/// side uses.
/// </summary>
/// <remarks>
/// <para>Nothing in this package writes <c>Tenant.Variables</c> — an admin UI, a seeder or a
/// migration does. Historically that side had to reverse-engineer the shape, and getting it
/// wrong was invisible: a blob written with default options is PascalCase, the settings read
/// camelCase, and System.Text.Json reports the mismatch as "no properties present" rather than
/// as an error, so the override vanishes into compile-time defaults without an exception and
/// without an <c>OnSettingHydrationFailed</c> event.</para>
///
/// <para>Resolve this service and the question does not arise:</para>
/// <code>
/// public sealed class TenantWriter(ITenantSettingsSerializer serializer)
/// {
///     public string ToBlob(PricingModel model) =&gt; serializer.Serialize(model);
/// }
/// </code>
///
/// <para><b>Lifetime.</b> Singleton.</para>
/// </remarks>
public interface ITenantSettingsSerializer
{
    /// <summary>
    /// The frozen options every <see cref="Setting{T}"/> is hydrated through: every
    /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> the host registered,
    /// plus <see cref="TenantsJsonContext"/>, under camelCase / <c>WhenWritingNull</c>.
    /// </summary>
    JsonSerializerOptions Options { get; }

    /// <summary>
    /// Serialises a setting model into the blob shape <see cref="Setting{T}.Hydrate"/> reads.
    /// </summary>
    /// <typeparam name="TModel">The setting's model type.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// No JSON metadata is registered for <typeparamref name="TModel"/> and reflection-based
    /// serialization is disabled (Native AOT / trimming). Register a context covering the model
    /// with <c>AddTenantsExtension(o =&gt; o.AddJsonContext(…))</c>.
    /// </exception>
    string Serialize<TModel>(TModel model) where TModel : class;
}
