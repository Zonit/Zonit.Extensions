using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Tenants.Services;

/// <summary>
/// Singleton holder of the options built from <see cref="TenantSettingsOptions"/>, and the write
/// half of the settings contract.
/// </summary>
/// <remarks>
/// <para><b>Why a singleton.</b> Combining resolvers and freezing a
/// <see cref="JsonSerializerOptions"/> is once-per-application work, and — more importantly —
/// System.Text.Json caches each resolved <see cref="JsonTypeInfo"/> on the options instance. A
/// per-scope instance would rebuild that cache on every request, which is exactly the cost the
/// per-scope hydration cache in <see cref="TenantService"/> exists to avoid.</para>
/// </remarks>
internal sealed class TenantSettingsSerializer(TenantSettingsOptions options) : ITenantSettingsSerializer
{
    public JsonSerializerOptions Options { get; } = options.Build();

    public string Serialize<TModel>(TModel model) where TModel : class
    {
        ArgumentNullException.ThrowIfNull(model);

        if (Options.TryGetTypeInfo(typeof(TModel), out var info) && info is JsonTypeInfo<TModel> typed)
            return JsonSerializer.Serialize(model, typed);

        return SerializeWithoutMetadata(model);
    }

    /// <summary>
    /// Mirror of <c>Setting&lt;T&gt;.HydrateWithoutMetadata</c> — same guard, same reasoning, so
    /// that a host which can read a setting can always write it back.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Guarded by IsReflectionEnabledByDefault, which is false exactly when trimming makes the reflective path unsafe; the guard throws rather than silently writing an empty object.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Same guard: unreachable under Native AOT.")]
    private string SerializeWithoutMetadata<TModel>(TModel model) where TModel : class
    {
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            throw new InvalidOperationException(
                $"No JSON metadata is registered for tenant setting model '{typeof(TModel)}', and reflection-based " +
                "serialization is disabled in this build (Native AOT or trimming). Add " +
                $"[JsonSerializable(typeof({typeof(TModel).Name}))] to a JsonSerializerContext and register it with " +
                "AddTenantsExtension(o => o.AddJsonContext(YourContext.Default)).");
        }

        return JsonSerializer.Serialize(model, Options);
    }
}
