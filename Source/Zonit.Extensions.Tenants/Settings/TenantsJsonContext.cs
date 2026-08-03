using System.Text.Json.Serialization;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Source-generated JSON metadata for the built-in <see cref="Setting{T}"/> models — and the
/// <b>supported way to produce the blobs they read back</b>.
/// Each <c>JsonSerializable</c> entry instructs <c>System.Text.Json</c>'s source generator
/// to emit a <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo{T}"/> at
/// compile time — <b>no reflection at runtime</b>, fully AOT- and trim-safe.
/// </summary>
/// <remarks>
/// <para><b>Why this is public.</b> The options below are not the System.Text.Json defaults:
/// property names are written in <c>camelCase</c>. Deserialisation is case-sensitive, so a
/// blob written with default options (<c>PascalCase</c>) matches <i>nothing</i> — and
/// System.Text.Json reports that as "no properties present", not as an error. The setting then
/// resolves to its compile-time defaults and the tenant's override vanishes without a trace.
/// Through 10.0.0-preview.9 this context was <c>internal</c>, so there was no way for a host
/// writing to <c>Tenant.Variables</c> to hit the right shape except by reverse-engineering it.
/// It is now part of the public contract: write with the same <c>JsonTypeInfo</c> the matching
/// <see cref="Setting{T}.Hydrate(string)"/> reads with.</para>
///
/// <para><b>Producing a value for <c>Tenant.Variables["site"]</c>:</b></para>
/// <code>
/// var json = JsonSerializer.Serialize(model, TenantsJsonContext.Default.SiteSettingsModel);
/// </code>
///
/// <para><b>Plugins.</b> A plugin ships its own <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// and owns both halves of its round trip; the only rule is to use the <i>same</i>
/// <c>JsonTypeInfo</c> on the write side as <see cref="Setting{T}.Hydrate(string)"/> uses on
/// the read side, and to make that context reachable by whoever persists the value. See
/// <see cref="Setting{T}"/> for the recipe.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SiteSettingsModel))]
[JsonSerializable(typeof(ThemeSettingsModel))]
[JsonSerializable(typeof(MaintenanceSettingsModel))]
[JsonSerializable(typeof(SocialMediaModel))]
public sealed partial class TenantsJsonContext : JsonSerializerContext;
