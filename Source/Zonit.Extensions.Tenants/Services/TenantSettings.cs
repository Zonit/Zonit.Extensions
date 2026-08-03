using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Tenants;

/// <summary>
/// Strongly-typed accessor over <see cref="ITenantProvider.GetSetting{TSetting}"/> —
/// one member per <see cref="Settings.Setting{T}"/> discovered at compile time.
/// </summary>
/// <remarks>
/// <para>This class is intentionally <see langword="partial"/>. The companion file is
/// <b>auto-generated</b> by <c>Zonit.Extensions.Tenants.SourceGenerators</c> and contains
/// one property per <see cref="Settings.Setting{T}"/> declared <b>in this assembly</b>
/// (<c>Site</c>, <c>Theme</c>, <c>Maintenance</c>, <c>SocialMedia</c>). Hand-edits go in
/// this file; the generator only ever emits <c>TenantSettings.g.cs</c>.</para>
///
/// <para><b>Plugin settings.</b> A partial class cannot span assemblies, so a
/// <see cref="Settings.Setting{T}"/> declared in a plugin cannot become a property here.
/// The generator instead emits an extension method into the plugin's own namespace, backed
/// by <see cref="Get{TSetting}"/>: <c>@Tenant.Settings.MyPlugin().Headline</c>. Import the
/// plugin namespace and the accessor shows up in IntelliSense alongside the built-ins.</para>
///
/// <para><b>Caller sample:</b></para>
/// <code>
/// // Razor:
/// @inject ITenantProvider Tenant
/// &lt;h1&gt;@Tenant.Settings.Site.Title&lt;/h1&gt;
/// &lt;style&gt;:root { --primary: @Tenant.Settings.Theme.PrimaryColor; }&lt;/style&gt;
/// </code>
///
/// <para>Each accessor is lazily hydrated and cached by <see cref="ITenantProvider.GetSetting{TSetting}"/>.
/// The cache flips on tenant change (<see cref="ITenantProvider.OnChange"/>).</para>
/// </remarks>
public partial class TenantSettings(ITenantProvider provider)
{
    /// <summary>The provider that backs this façade. Exposed so the auto-generated
    /// partial can resolve settings without re-parameterising every accessor.</summary>
    protected ITenantProvider Provider { get; } = provider;

    /// <summary>
    /// Resolves (and hydrates) <typeparamref name="TSetting"/> through the backing provider.
    /// </summary>
    /// <remarks>
    /// Public because the generated plugin façades live in <b>other assemblies</b> and so
    /// cannot see <see cref="Provider"/> — a <see langword="protected"/> member is not
    /// reachable from an extension method. This is the single supported seam through which
    /// out-of-package accessors reach the provider.
    /// </remarks>
    /// <typeparam name="TSetting">Concrete setting type to resolve.</typeparam>
    /// <returns>The scope-cached, hydrated setting instance.</returns>
    public TSetting Get<TSetting>() where TSetting : ISetting, new()
        => Provider.GetSetting<TSetting>();
}
