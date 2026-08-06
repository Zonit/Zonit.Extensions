using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Tenants.Services;

/// <summary>
/// <see cref="ITenantProvider"/> implementation. Owns:
/// <list type="bullet">
///   <item>the per-scope cache of hydrated <see cref="ISetting"/> instances,</item>
///   <item>the relay of <see cref="ITenantRepository.OnChange"/> to consumers,</item>
///   <item>the lazy-built <see cref="TenantSettings"/> façade.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>AOT safety.</b> Hydration is delegated to <see cref="Setting{T}.Hydrate"/>
/// which is implemented per concrete setting using a source-generated
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>. Zero reflection
/// here — the legacy implementation's <c>typeof(T).GetProperty("Value")</c> +
/// <c>JsonSerializer.Deserialize</c>-via-<c>MakeGenericMethod</c> path is gone.</para>
///
/// <para><b>Per-scope cache.</b> <see cref="GetSetting{T}"/> caches by setting key inside
/// a <see cref="ConcurrentDictionary{TKey,TValue}"/>. A typical request hits any given
/// setting multiple times (layout, page, components) so this avoids re-deserialising
/// the same JSON. The cache is invalidated when the underlying tenant changes
/// (<see cref="ITenantRepository.OnChange"/>).</para>
///
/// <para><b>Lifetime.</b> <c>Scoped</c>.</para>
/// </remarks>
internal sealed partial class TenantService : ITenantProvider, IDisposable
{
    private readonly ITenantRepository _repository;

    /// <remarks>
    /// Never <see langword="null"/>: an absent registration collapses to
    /// <see cref="NullLogger{T}.Instance"/> in the constructor. That keeps the null check off
    /// the read path — <see cref="NullLogger{T}"/> answers <c>IsEnabled</c> with
    /// <see langword="false"/>, which is the same branch the generated logging method takes
    /// anyway — and it sidesteps the <c>[LoggerMessage]</c> generator, which emits an
    /// unguarded <c>logger.IsEnabled(…)</c> and therefore cannot take a nullable logger.
    /// </remarks>
    private readonly ILogger<TenantService> _logger;

    /// <remarks>
    /// Sized explicitly rather than with the parameterless constructor. This service is
    /// <c>Scoped</c>, so one of these is built per request, and the default constructor sizes
    /// its lock array by <see cref="Environment.ProcessorCount"/> — dozens of locks and a
    /// 31-bucket table to hold the two or three settings a typical page reads. Concurrency
    /// level 1 keeps it thread-safe (Blazor circuits do touch a scope from more than one
    /// thread) while allocating a single lock.
    /// </remarks>
    private readonly ConcurrentDictionary<string, ISetting> _hydrated =
        new(concurrencyLevel: 1, capacity: 4, StringComparer.Ordinal);

    private TenantSettings? _settings;

    /// <remarks>
    /// Resolved from the singleton <see cref="TenantSettingsSerializer"/> rather than rebuilt
    /// here: combining resolvers and freezing the options is once-per-application work, and
    /// System.Text.Json caches the resolved <c>JsonTypeInfo</c> per type <i>on the options
    /// instance</i> — a per-scope instance would throw that cache away every request.
    /// </remarks>
    private readonly JsonSerializerOptions _json;

    private readonly TenantConfigurationSource _configuration;

    public TenantService(
        ITenantRepository repository,
        ITenantSettingsSerializer serializer,
        TenantConfigurationSource configuration,
        ILogger<TenantService>? logger = null)
    {
        _repository = repository;
        _json = serializer.Options;
        _configuration = configuration;
        _logger = logger ?? NullLogger<TenantService>.Instance;

        _repository.OnChange += HandleStateChanged;

        // A configuration reload changes what un-overridden settings hydrate to, so it
        // invalidates exactly what a tenant switch invalidates and is routed through the same
        // handler. Unsubscribed in Dispose — this is a scoped service listening to a singleton,
        // which is the classic way to leak a request scope.
        _configuration.OnReload += HandleStateChanged;
    }

    public Tenant? Current => _repository.Current;

    public TenantResolution Resolution => _repository.Resolution;

    public event Action? OnChange;

    public event Action<SettingHydrationFailure>? OnSettingHydrationFailed;

    public TenantSettings Settings => _settings ??= new TenantSettings(this);

    public TSetting GetSetting<TSetting>() where TSetting : ISetting, new()
    {
        // Cache key is the setting's stable Key. We cannot key on `typeof(TSetting)` alone
        // because that would create a separate cached entry per generic instantiation
        // even when consumers ask for the same logical setting through different APIs.
        var key = SettingKeyOf<TSetting>.Value;

        if (_hydrated.TryGetValue(key, out var existing) && existing is TSetting cached)
            return cached;

        var hydrated = HydrateInto(new TSetting());
        _hydrated[key] = hydrated;
        return hydrated;
    }

    /// <summary>
    /// The <see cref="ISetting.Key"/> of <typeparamref name="TSetting"/>, read once per closed
    /// generic instead of once per call.
    /// </summary>
    /// <remarks>
    /// <see cref="ISetting.Key"/> is an instance member, so reading it used to mean constructing
    /// a prototype — <b>before</b> the cache lookup, and therefore on every single read. A layout
    /// plus a page plus a handful of components easily touch <c>Settings.Site</c> and
    /// <c>Settings.Theme</c> a dozen times per render, each allocating a setting object (and, for
    /// <see cref="Settings.Setting{T}"/>, its default model) purely to ask for a constant string.
    /// Every implementation returns a literal from an expression-bodied property, so hoisting it
    /// into a static generic field is safe and makes a cache hit allocation-free.
    /// </remarks>
    private static class SettingKeyOf<TSetting> where TSetting : ISetting, new()
    {
        internal static readonly string Value = new TSetting().Key;
    }

    /// <summary>
    /// Looks the persisted JSON up in <see cref="Tenant.Variables"/> and dispatches
    /// hydration to <see cref="ISettingHydrator.HydrateFromJson"/> on the
    /// prototype. Falls through to defaults when no override exists or the JSON is bad —
    /// but reports the bad-JSON case through <see cref="OnSettingHydrationFailed"/>.
    /// </summary>
    private TSetting HydrateInto<TSetting>(TSetting prototype) where TSetting : ISetting
    {
        var tenant = _repository.Current;

        // Every Setting<T> implements ISettingHydrator (explicit-interface) and routes through
        // Setting<T>, which resolves T's JsonTypeInfo from the shared options. The call stays
        // monomorphic for each TSetting closed-generic.
        if (prototype is not ISettingHydrator hydrator)
            return prototype;

        // Layer 1: this tenant's persisted override. Absent when no tenant resolved, which is not
        // an error — the layers below still apply, and that is exactly why a nullable Current
        // costs no page a null check.
        string? json = null;
        var hasBlob = tenant is not null
            && tenant.Variables.TryGetValue(prototype.Key, out json)
            && !string.IsNullOrEmpty(json);

        // Layer 2: appsettings — the house default shared by every tenant. Only consulted when
        // the tenant stored nothing, so a stored value always wins.
        var section = hasBlob ? null : _configuration.TryGetSection(prototype.Key);

        // Layer 3: neither — the model's compile-time defaults, already sitting in prototype.
        if (!hasBlob && section is null)
            return prototype;

        try
        {
            if (hasBlob)
                hydrator.HydrateFromJson(json!, _json);
            else
                hydrator.HydrateFromConfiguration(section!, _json);
        }
        catch (JsonException ex)
        {
            // Deliberately narrow. A corrupt blob is a DATA problem: the request survives on
            // the defaults already sitting in prototype.Value. Everything else — a
            // NullReferenceException inside a plugin's Hydrate, a bad JsonTypeInfo, an
            // InvalidOperationException — is a CODE problem, and the previous blanket
            // `catch { }` turned those into "this tenant has no override", which is
            // indistinguishable from success and cost real debugging time.
            //
            // Even the data case is no longer silent. It goes out twice on purpose: the log
            // line is what a host gets for free, the event is what a host subscribes to when it
            // wants to react (surface a banner, flag the tenant for repair) rather than record.
            // Neither is a substitute for the other, and dropping the event would be a breaking
            // change for the hosts already using it.
            SettingHydrationFailed(_logger, tenant?.Id, prototype.Key, ex);
            OnSettingHydrationFailed?.Invoke(new SettingHydrationFailure(tenant?.Id, prototype.Key, ex));
        }

        return prototype;
    }

    /// <summary>
    /// Records a corrupt override. Source-generated by <c>Microsoft.Extensions.Logging</c>, so
    /// the enabled-check happens before any argument is boxed and nothing is allocated when the
    /// level is off — which matters because this sits on the settings read path.
    /// </summary>
    /// <remarks>
    /// <c>Warning</c> rather than <c>Error</c>: the request is not failing — the setting falls
    /// back to its compile-time defaults — but somebody's persisted configuration is being
    /// ignored, which nobody wants to discover from a screenshot.
    /// </remarks>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Tenant {TenantId}: the persisted value for setting '{SettingKey}' could not be hydrated and its compile-time defaults are being used instead.")]
    private static partial void SettingHydrationFailed(
        ILogger logger, Guid? tenantId, string settingKey, Exception exception);

    private void HandleStateChanged()
    {
        _hydrated.Clear();
        _settings = null;
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _repository.OnChange -= HandleStateChanged;
        _configuration.OnReload -= HandleStateChanged;
    }
}
