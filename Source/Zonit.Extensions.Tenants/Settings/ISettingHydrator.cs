namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Framework-internal contract through which <see cref="Setting{T}"/> exposes its
/// AOT-safe JSON hydration to the dispatcher in <c>TenantService</c>. Plugins do not
/// implement this directly — they implement the abstract <see cref="Setting{T}.Hydrate(string)"/>
/// method, and the explicit-interface implementation on <see cref="Setting{T}"/> wires
/// the rest. Keeping the interface <see cref="System.Reflection.MemberInfo">internal</see>
/// preserves the simple <see cref="Setting{T}"/> public surface.
/// </summary>
internal interface ISettingHydrator
{
    /// <summary>Hydrates the setting's <c>Value</c> from its persisted JSON blob.</summary>
    void HydrateFromJson(string json);
}
