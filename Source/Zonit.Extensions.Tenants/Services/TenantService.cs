using System.Collections.Concurrent;
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
/// <para><b>AOT safety.</b> Hydration is delegated to <see cref="Setting{T}.Hydrate(string)"/>
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
internal sealed class TenantService : ITenantProvider, IDisposable
{
    private readonly ITenantRepository _repository;
    private readonly ConcurrentDictionary<string, ISetting> _hydrated = new(StringComparer.Ordinal);
    private TenantSettings? _settings;

    public TenantService(ITenantRepository repository)
    {
        _repository = repository;
        _repository.OnChange += HandleStateChanged;
    }

    public Tenant? Current => _repository.Current;

    public event Action? OnChange;

    public TenantSettings Settings => _settings ??= new TenantSettings(this);

    public TSetting GetSetting<TSetting>() where TSetting : ISetting, new()
    {
        var prototype = new TSetting();

        // Cache key is the setting's stable Key. We cannot key on `typeof(TSetting)` alone
        // because that would create a separate cached entry per generic instantiation
        // even when consumers ask for the same logical setting through different APIs.
        if (_hydrated.TryGetValue(prototype.Key, out var existing) && existing is TSetting cached)
            return cached;

        var hydrated = HydrateInto(prototype);
        _hydrated[hydrated.Key] = hydrated;
        return hydrated;
    }

    /// <summary>
    /// Looks the persisted JSON up in <see cref="Tenant.Variables"/> and dispatches
    /// hydration to <see cref="ISettingHydrator.HydrateFromJson(string)"/> on the
    /// prototype. Falls through to defaults when no override exists or the JSON is bad.
    /// </summary>
    private TSetting HydrateInto<TSetting>(TSetting prototype) where TSetting : ISetting
    {
        var tenant = _repository.Current;
        if (tenant is null) return prototype;
        if (!tenant.Variables.TryGetValue(prototype.Key, out var json) || string.IsNullOrEmpty(json))
            return prototype;

        try
        {
            // Every Setting<T> implements ISettingHydrator (explicit-interface) and
            // routes through its own AOT-safe Hydrate(string). No reflection, no JIT
            // surprises — the call stays monomorphic for each TSetting closed-generic.
            if (prototype is ISettingHydrator hydrator)
                hydrator.HydrateFromJson(json);
        }
        catch
        {
            // Malformed JSON must not crash the request — defaults already populated
            // prototype.Value. Persistent bad blobs are a concern for the consumer's
            // ITenantSource (which can decide whether to log / replace / surface).
        }

        return prototype;
    }

    private void HandleStateChanged()
    {
        _hydrated.Clear();
        _settings = null;
        OnChange?.Invoke();
    }

    public void Dispose() => _repository.OnChange -= HandleStateChanged;
}
