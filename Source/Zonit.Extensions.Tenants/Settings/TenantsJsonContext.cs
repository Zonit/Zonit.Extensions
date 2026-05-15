using System.Text.Json.Serialization;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Source-generated JSON metadata for the built-in <see cref="Setting{T}"/> models.
/// Each <c>JsonSerializable</c> entry instructs <c>System.Text.Json</c>'s source generator
/// to emit a <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo{T}"/> at
/// compile time — <b>no reflection at runtime</b>, fully AOT- and trim-safe.
/// </summary>
/// <remarks>
/// Plugins shipping their own <see cref="Setting{T}"/> must follow the same pattern in
/// their own assembly. See <see cref="Setting{T}.Hydrate(string)"/> docs for the
/// recipe.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SiteSettingsModel))]
[JsonSerializable(typeof(ThemeSettingsModel))]
[JsonSerializable(typeof(MaintenanceSettingsModel))]
[JsonSerializable(typeof(SocialMediaModel))]
internal sealed partial class TenantsJsonContext : JsonSerializerContext;
