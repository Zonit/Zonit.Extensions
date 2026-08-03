using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// The single predicate every <see cref="IPersistentStateProvider"/> must consult before it
/// touches <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/>, plus the
/// diagnostic the hydration host emits when the answer is <see langword="false"/>.
/// </summary>
/// <remarks>
/// <para><b>Why a predicate at all.</b>
/// <c>PersistentComponentState.PersistAsJson&lt;T&gt;</c> and <c>TryTakeFromJson&lt;T&gt;</c> do not
/// serialise through anything the caller can configure. Their IL (verified against
/// <c>Microsoft.AspNetCore.Components</c> 10.0.9 in the shared framework) calls
/// <c>JsonSerializer.SerializeToUtf8Bytes&lt;T&gt;(value, JsonSerializerOptionsProvider.Options)</c> and
/// <c>JsonSerializer.Deserialize&lt;T&gt;(ref reader, JsonSerializerOptionsProvider.Options)</c>, and that
/// static options instance is built as
/// <c>new JsonSerializerOptions { PropertyNamingPolicy = CamelCase, PropertyNameCaseInsensitive = true, IncludeFields = true }</c>
/// — it has <b>no</b> <c>TypeInfoResolver</c>. So when reflection-based serialisation is disabled the
/// call throws <see cref="InvalidOperationException"/> for <em>every</em> payload type, including a bare
/// <see cref="string"/>.</para>
///
/// <para><b>When it is disabled.</b> The SDK turns
/// <c>System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault</c> off for any
/// <c>PublishTrimmed</c> publish — not only <c>PublishAot</c>. Measured on a plain
/// <c>PublishTrimmed=true</c> / <c>TrimMode=full</c> console publish against .NET 10.0.9: the switch reads
/// <see langword="false"/> and serialising a <see cref="string"/> with the options shape above throws
/// <c>"Reflection-based serialization has been disabled for this application."</c>. Blazor WebAssembly is
/// the exception — its SDK explicitly restores the switch to <see langword="true"/>.</para>
///
/// <para><b>The escape hatch consumers actually have.</b> Setting
/// <c>&lt;JsonSerializerIsReflectionEnabledByDefault&gt;true&lt;/JsonSerializerIsReflectionEnabledByDefault&gt;</c>
/// in the app's project file re-enables hydration in a trimmed build. That is safe for the built-in
/// bridges because each roots its payload types with <c>[DynamicDependency]</c> (or persists a type with
/// a hand-written <c>JsonConverter</c>, or a primitive); a custom
/// <see cref="IPersistentStateProvider"/> must root its own payload the same way.</para>
///
/// <para><b>What would make this unnecessary.</b>
/// <c>PersistentComponentStateSerializer&lt;T&gt;</c> is public and overridable in .NET 10, but it is
/// <em>not</em> consulted by <c>PersistAsJson</c>. The only consumer in the framework is
/// <c>PersistentValueProviderComponentSubscription</c>, i.e. the declarative
/// <c>[PersistentState]</c> component-parameter path; it resolves the serializer with
/// <c>Type.MakeGenericType(typeof(PersistentComponentStateSerializer&lt;&gt;), propertyType)</c> +
/// <c>IServiceProvider.GetService</c> and then reads/writes through the <c>internal</c>
/// <c>PersistAsBytes</c> / <c>TryTakeBytes</c>. There is therefore no way for a keyed, service-driven
/// bridge like <see cref="IPersistentStateProvider"/> to hand the framework a source-generated
/// <c>JsonTypeInfo</c>; moving to <c>[PersistentState]</c> would mean giving up both the key-based
/// contract and the open extension point.</para>
/// </remarks>
public static class HydrationSerialization
{
    /// <summary>
    /// <see langword="true"/> when <c>PersistAsJson</c> / <c>TryTakeFromJson</c> can actually run.
    /// Implementers of <see cref="IPersistentStateProvider"/> must return early from both
    /// <see cref="IPersistentStateProvider.Restore"/> and their persist callback when this is
    /// <see langword="false"/> — otherwise the prerender's persist phase faults.
    /// </summary>
    public static bool IsAvailable => JsonSerializer.IsReflectionEnabledByDefault;

    /// <summary>
    /// Bridge types already reported, so a server does not emit the same warning once per circuit.
    /// Static (process-wide) on purpose: the condition is a build-time property of the whole app, so
    /// repeating it per scope would be noise, not information.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, byte> ReportedBridges = new();

    /// <summary>
    /// Reports that hydration is disabled for this build, honouring
    /// <see cref="HydrationOptions.WhenSerializationUnavailable"/>.
    /// </summary>
    /// <param name="bridges">The bridges that will silently no-op.</param>
    /// <param name="logger">Logger of the hydration host.</param>
    /// <param name="behavior">Configured reporting behaviour.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="behavior"/> is <see cref="HydrationUnavailableBehavior.Throw"/>.
    /// </exception>
    internal static void ReportUnavailable(
        IEnumerable<IPersistentStateProvider> bridges,
        ILogger logger,
        HydrationUnavailableBehavior behavior)
    {
        if (behavior == HydrationUnavailableBehavior.Silent)
            return;

        var names = bridges.Select(b => b.GetType()).ToArray();

        if (behavior == HydrationUnavailableBehavior.Throw)
        {
            throw new InvalidOperationException(
                $"Prerender → interactive state hydration is unavailable: reflection-based System.Text.Json " +
                $"is disabled for this build (PublishTrimmed / PublishAot), and PersistentComponentState's " +
                $"PersistAsJson/TryTakeFromJson have no JsonTypeInfo seam. Affected bridges: " +
                $"{string.Join(", ", names.Select(t => t.Name))}. Either publish with " +
                $"<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>, " +
                $"or set HydrationOptions.WhenSerializationUnavailable to Warn or Silent to accept the loss.");
        }

        foreach (var type in names)
        {
            // TryAdd is the dedupe: first circuit that sees the condition logs it, the rest stay quiet.
            if (!ReportedBridges.TryAdd(type, 0))
                continue;

            logger.LogWarning(
                "Hydration bridge {Bridge} is disabled: reflection-based System.Text.Json is off for this " +
                "build (the SDK clears System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault for every " +
                "PublishTrimmed publish, PublishAot included). Prerendered state does NOT reach the interactive " +
                "render, so the circuit starts from a cold scoped state (anonymous identity, default culture, " +
                "no workspace/catalog/cookies). Publish with " +
                "<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault> to " +
                "restore it, or configure HydrationOptions.WhenSerializationUnavailable.",
                type.Name);
        }
    }
}
