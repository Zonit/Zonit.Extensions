using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Tenants.Services;

/// <summary>
/// Singleton view over the <c>Tenants</c> configuration section, plus the reload signal that lets
/// an edit to <c>appsettings.json</c> reach a live Blazor page.
/// </summary>
/// <remarks>
/// <para><b>Ordinary .NET configuration.</b> Nothing here is a custom pipeline: the section comes
/// from <see cref="IConfiguration"/>, so every provider the host has registered feeds it —
/// <c>appsettings.json</c>, <c>appsettings.{Environment}.json</c>, user secrets, environment
/// variables (<c>Tenants__site__title</c>), command line, Key Vault. Reload uses
/// <see cref="IConfiguration.GetReloadToken"/> through
/// <see cref="ChangeToken.OnChange{TState}(Func{IChangeToken}, Action{TState}, TState)"/>, which
/// is what <c>reloadOnChange: true</c> already drives for <c>IOptionsMonitor</c>.</para>
///
/// <para><b>Why a singleton relays to scoped services.</b> A configuration reload is an
/// application-wide event, while <see cref="ITenantProvider"/> is scoped — a Blazor circuit that
/// wants to re-render on a settings change cannot subscribe to a token from a scope that
/// out-lives none of them. Each <see cref="TenantService"/> subscribes here and unsubscribes on
/// dispose; the circuit's own <c>OnChange</c> handling then does the re-render, exactly as it
/// does for a tenant switch.</para>
///
/// <para><b>Optional.</b> <see cref="IConfiguration"/> is an optional dependency, so the package
/// still composes in a bare <c>ServiceCollection</c> with no host behind it — a unit test, a
/// small console tool. <see cref="TryGetSection"/> then simply never finds anything.</para>
/// </remarks>
internal sealed class TenantConfigurationSource : IDisposable
{
    private readonly IConfiguration? _configuration;
    private readonly string _sectionName;
    private readonly IDisposable? _reloadSubscription;

    public TenantConfigurationSource(TenantSettingsOptions options, IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _sectionName = options.ConfigurationSection;

        if (_configuration is not null && options.ReloadOnChange)
        {
            _reloadSubscription = ChangeToken.OnChange(
                _configuration.GetReloadToken,
                static state => state.OnReload?.Invoke(),
                this);
        }
    }

    /// <summary>Raised after the configuration providers reload.</summary>
    public event Action? OnReload;

    /// <summary>
    /// The <c>Tenants:{key}</c> section, or <see langword="null"/> when the host configured
    /// nothing for this setting.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigurationExtensions.Exists"/> is the right test rather than a null check:
    /// <see cref="IConfiguration.GetSection"/> never returns <see langword="null"/>, it returns a
    /// section with no value and no children.
    /// </remarks>
    public IConfigurationSection? TryGetSection(string key)
    {
        if (_configuration is null)
            return null;

        var section = _configuration.GetSection(_sectionName).GetSection(key);
        return section.Exists() ? section : null;
    }

    public void Dispose() => _reloadSubscription?.Dispose();
}
