using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Base class for all tenant settings. Plugins (Areas) ship a class deriving from this and
/// override the metadata; JSON handling is inherited and only needs overriding when a setting
/// has genuinely different serialisation rules.
/// </summary>
/// <typeparam name="T">Setting model. Needs a parameterless constructor because "this tenant has
/// no override" is answered with <c>new()</c>, never with <see langword="null"/>.</typeparam>
/// <remarks>
/// <para><b>Minimal setting</b> — this is the whole thing:</para>
/// <code>
/// public sealed class PricingSetting : Setting&lt;PricingModel&gt;
/// {
///     public override string Key         =&gt; "acme_pricing";
///     public override string Name        =&gt; "Pricing";
///     public override string Description =&gt; "Billing plan shown on the pricing page.";
/// }
/// </code>
///
/// <para><b>The write half is still part of the contract.</b> Nothing here produces the blob in
/// <c>Tenant.Variables[Key]</c> — whoever persists a tenant writes it, and must serialise with
/// the same rules <see cref="Hydrate"/> reads with. Those rules are camelCase and
/// <c>WhenWritingNull</c> (see <see cref="TenantSettingsOptions"/>), and a mismatch is <b>not</b>
/// an error: System.Text.Json matches property names case-sensitively, finds none, and hands
/// back a model full of compile-time defaults.</para>
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
    /// Internal bridge between the framework's hydration dispatcher (which sees only
    /// <see cref="ISetting"/>) and the type-specific <see cref="Hydrate"/>. Explicit-interface so
    /// it does not pollute the public surface of plugin <see cref="Setting{T}"/> derivatives.
    /// </summary>
    void ISettingHydrator.HydrateFromJson(string json, JsonSerializerOptions options)
        => Value = Hydrate(json, options);

    void ISettingHydrator.HydrateFromConfiguration(IConfigurationSection section, JsonSerializerOptions options)
        => Value = HydrateFromConfiguration(section, options);

    /// <summary>
    /// Materialises the model from an <c>appsettings</c> section — the layer beneath a persisted
    /// <c>Tenant.Variables</c> blob and above the compile-time defaults.
    /// </summary>
    /// <param name="section">
    /// The <c>Tenants:{Key}</c> section. Guaranteed to exist; never empty.
    /// </param>
    /// <param name="options">The shared options, as for <see cref="Hydrate"/>.</param>
    /// <remarks>
    /// The section is rendered to JSON and handed to <see cref="Hydrate"/>, so configuration and
    /// stored blobs share one contract: the same property matching, the same converters, and the
    /// same single override point if a setting needs different rules. Overriding
    /// <see cref="Hydrate"/> therefore covers configuration too; override this method only to
    /// change how <i>configuration</i> specifically is read.
    /// </remarks>
    public virtual T HydrateFromConfiguration(IConfigurationSection section, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(options);

        options.TryGetTypeInfo(typeof(T), out var info);
        return Hydrate(ConfigurationJsonWriter.Write(section, info), options);
    }

    /// <summary>
    /// Materialises the model from its persisted JSON blob. Called by <c>TenantService</c> when
    /// the tenant has an override for <see cref="Key"/>.
    /// </summary>
    /// <param name="json">JSON payload. Never <see langword="null"/> or empty.</param>
    /// <param name="options">
    /// The shared options built from <see cref="TenantSettingsOptions"/> — every
    /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> the host registered,
    /// plus <see cref="TenantsJsonContext"/> for the built-ins.
    /// </param>
    /// <returns>The hydrated model.</returns>
    /// <remarks>
    /// <para><b>Two paths, and which one you get is a build-time property of your app.</b></para>
    /// <list type="number">
    ///   <item><b>Registered metadata</b> — <typeparamref name="T"/> is covered by a context the
    ///         host passed to <c>AddJsonContext</c>. Fully trim- and AOT-safe, no reflection.</item>
    ///   <item><b>Reflection fallback</b> — nothing covers <typeparamref name="T"/>. Works
    ///         perfectly on a normal (JIT, untrimmed) host, which is why a setting needs no JSON
    ///         ceremony at all there, and <b>throws</b> under Native AOT or trimming, where the
    ///         model's property accessors may not exist in the binary. The Tenants source
    ///         generator reports <c>ZONITTS0003</c> at build time for any setting model with no
    ///         <c>[JsonSerializable]</c> entry in a project that opts into AOT, so this is a
    ///         compile-time discovery rather than a production surprise.</item>
    /// </list>
    ///
    /// <para><b>When to override.</b> Only when the setting's JSON rules differ from the shared
    /// ones — a different naming policy, a custom converter, a
    /// <c>JsonStringEnumConverter</c>. Reach for the concrete <c>JsonTypeInfo</c> then, because
    /// the shared options deliberately do not inherit a context's own
    /// <c>[JsonSourceGenerationOptions]</c>:</para>
    /// <code>
    /// public override PricingModel Hydrate(string json, JsonSerializerOptions options)
    ///     =&gt; JsonSerializer.Deserialize(json, PricingJsonContext.Default.PricingModel) ?? new();
    /// </code>
    ///
    /// <para><b>Do not swallow exceptions here.</b> <c>TenantService</c> catches
    /// <see cref="JsonException"/>, keeps the compile-time defaults, logs, and raises
    /// <c>ITenantProvider.OnSettingHydrationFailed</c>. Catching it inside an override keeps the
    /// page up but makes a corrupt blob indistinguishable from "no override".</para>
    /// </remarks>
    public virtual T Hydrate(string json, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Registered metadata. TryGetTypeInfo answers false — cleanly, without throwing — when
        // no resolver in the chain covers T, which is what makes the fallback below reachable.
        if (options.TryGetTypeInfo(typeof(T), out var info) && info is JsonTypeInfo<T> typed)
            return JsonSerializer.Deserialize(json, typed) ?? new();

        return HydrateWithoutMetadata(json) ?? new();
    }

    /// <summary>
    /// The reflection path, isolated so its suppressions cover exactly one call and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>The suppressions are honest rather than convenient: the guard means this method
    /// cannot reach the reflective serializer in a build where reflection has been disabled,
    /// which is precisely the configuration the two warnings describe. Under Native AOT
    /// <see cref="JsonSerializer.IsReflectionEnabledByDefault"/> is <see langword="false"/> and
    /// the call throws with a message naming the model and the fix, instead of handing back a
    /// silently empty object the way an unguarded reflective deserialise would.</para>
    ///
    /// <para>Keeping it in its own method also keeps the trim requirement from propagating:
    /// annotating <typeparamref name="T"/> on the class instead would push <c>IL2091</c> onto
    /// every derived setting in every consumer assembly.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Guarded by IsReflectionEnabledByDefault, which is false exactly when trimming makes the reflective path unsafe; the guard throws rather than silently mis-binding.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Same guard: unreachable under Native AOT.")]
    private static T? HydrateWithoutMetadata(string json)
    {
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            throw new InvalidOperationException(
                $"No JSON metadata is registered for tenant setting model '{typeof(T)}', and reflection-based " +
                "serialization is disabled in this build (Native AOT or trimming). Add " +
                $"[JsonSerializable(typeof({typeof(T).Name}))] to a JsonSerializerContext and register it with " +
                "AddTenantsExtension(o => o.AddJsonContext(YourContext.Default)), or override Hydrate on the setting.");
        }

        return JsonSerializer.Deserialize<T>(json);
    }
}
