namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Base class for all tenant settings. Plugins (Areas) ship a class deriving from this,
/// override the metadata and — crucially — implement <see cref="Hydrate(string)"/> in
/// an AOT-safe way (typically with a <c>JsonSerializerContext</c> from
/// <c>System.Text.Json</c>'s source generator).
/// </summary>
/// <typeparam name="T">Setting model.</typeparam>
/// <remarks>
/// <para><b>Why <see cref="Hydrate"/> is abstract.</b> The legacy implementation walked
/// the model with <c>System.Reflection</c> (<c>typeof(T).GetProperty("Value")</c>,
/// <c>JsonSerializer.Deserialize&lt;TModel&gt;</c> via <c>MakeGenericMethod</c>). That
/// path is not trim-safe and trips IL2026/IL3050 under <c>PublishAot=true</c>. By
/// pushing JSON deserialization down to each concrete <c>Setting&lt;T&gt;</c>, every
/// plugin keeps its own AOT-safe <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// and the core stays clean — no <c>[UnconditionalSuppressMessage]</c> needed here.</para>
///
/// <para><b>Plugin recipe</b> (1 line of JSON code per plugin):</para>
/// <code>
/// public class MyPluginSetting : Setting&lt;MyPluginModel&gt;
/// {
///     public override string Key         => "my_plugin";
///     public override string Name        => "My plugin";
///     public override string Description => "Plugin-specific options.";
///
///     public override MyPluginModel Hydrate(string json)
///         =&gt; System.Text.Json.JsonSerializer.Deserialize(
///                json, MyPluginJsonContext.Default.MyPluginModel) ?? new();
/// }
///
/// [JsonSerializable(typeof(MyPluginModel))]
/// internal partial class MyPluginJsonContext : JsonSerializerContext;
/// </code>
/// </remarks>
public abstract class Setting<T> : ISetting<T>, ISettingHydrator where T : class, new()
{
    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public virtual T Value { get; set; } = new();

    /// <inheritdoc />
    public virtual IReadOnlyCollection<T>? Templates => null;

    /// <summary>
    /// Internal bridge between the framework's hydration dispatcher (which sees
    /// only <see cref="ISetting"/>) and the type-specific <see cref="Hydrate(string)"/>.
    /// Implemented through explicit-interface so it does not pollute the public surface
    /// of plugin <see cref="Setting{T}"/> derivatives.
    /// </summary>
    void ISettingHydrator.HydrateFromJson(string json) => Value = Hydrate(json);

    /// <summary>
    /// Materialises the model from its persisted JSON blob. Called by
    /// <c>TenantService.GetSetting&lt;T&gt;()</c> when the tenant has an override for
    /// <see cref="Key"/>. Implementations <b>must</b> be AOT-safe — use a source-generated
    /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> instead of the
    /// reflection-based <c>JsonSerializer.Deserialize&lt;T&gt;(string)</c> overload.
    /// </summary>
    /// <param name="json">JSON payload. Never <see langword="null"/> or empty.</param>
    /// <returns>Hydrated model. Implementations should fall back to <c>new()</c> on
    /// malformed JSON rather than throwing — admin UI shouldn't crash on bad data.</returns>
    public abstract T Hydrate(string json);
}
