using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Framework-internal contract through which <see cref="Setting{T}"/> exposes its JSON hydration
/// to the dispatcher in <c>TenantService</c>. Plugins do not implement this directly — they
/// inherit (and optionally override) <see cref="Setting{T}.Hydrate"/>, and the
/// explicit-interface implementation on <see cref="Setting{T}"/> wires the rest. Keeping the
/// interface internal preserves the simple <see cref="Setting{T}"/> public surface.
/// </summary>
internal interface ISettingHydrator
{
    /// <summary>
    /// Hydrates the setting's <c>Value</c> from its persisted JSON blob, using the shared options
    /// built from <see cref="TenantSettingsOptions"/>.
    /// </summary>
    void HydrateFromJson(string json, JsonSerializerOptions options);

    /// <summary>
    /// Hydrates the setting's <c>Value</c> from an <c>appsettings</c> section.
    /// </summary>
    void HydrateFromConfiguration(IConfigurationSection section, JsonSerializerOptions options);
}
