namespace Zonit.Extensions.Tenants;

/// <summary>
/// Strongly-typed accessor over <see cref="ITenantProvider.GetSetting{TSetting}"/> —
/// one property per <see cref="Settings.Setting{T}"/> discovered at compile time.
/// </summary>
/// <remarks>
/// <para>This class is intentionally <see langword="partial"/>. The companion file is
/// <b>auto-generated</b> by <c>Zonit.Extensions.Tenants.SourceGenerators</c> and contains
/// one property per <see cref="Settings.Setting{T}"/> visible to the consuming compilation
/// (built-in + plugins). Hand-edits go in this file; the generator only ever emits
/// <c>TenantSettings.g.cs</c>.</para>
///
/// <para><b>Caller sample:</b></para>
/// <code>
/// // Razor:
/// @inject ITenantProvider Tenant
/// &lt;h1&gt;@Tenant.Settings.Site.Title&lt;/h1&gt;
/// &lt;style&gt;:root { --primary: @Tenant.Settings.Theme.PrimaryColor; }&lt;/style&gt;
/// </code>
///
/// <para>Each property is lazily hydrated and cached by <see cref="ITenantProvider.GetSetting{TSetting}"/>.
/// The cache flips on tenant change (<see cref="ITenantProvider.OnChange"/>).</para>
/// </remarks>
public partial class TenantSettings(ITenantProvider provider)
{
    /// <summary>The provider that backs this façade. Exposed so the auto-generated
    /// partial can resolve settings without re-parameterising every accessor.</summary>
    protected ITenantProvider Provider { get; } = provider;
}
