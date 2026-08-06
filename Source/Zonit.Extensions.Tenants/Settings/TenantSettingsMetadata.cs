using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Process-wide registry of JSON metadata for setting models, populated automatically by the
/// Tenants source generator so a consumer never has to wire it up.
/// </summary>
/// <remarks>
/// <para><b>Why a static registry rather than a DI registration.</b> The metadata the generator
/// emits lives in the <i>consumer's</i> assembly, and <c>AddTenantsExtension()</c> is compiled into
/// this one — it cannot name a type that does not exist yet. Something has to carry the reference
/// across that gap, and the only thing that runs without being called is a module initializer. The
/// generator emits one per assembly that declares a <c>Setting&lt;T&gt;</c>; it calls
/// <see cref="Register"/> when the assembly loads, and <see cref="TenantSettingsOptions"/> folds
/// whatever is registered into the shared options.</para>
///
/// <para><b>Ordering, and why the registry is read live.</b> A module initializer runs before any
/// other code in its assembly, but assemblies load lazily — an assembly is not touched until
/// something first names a type in it. That is <i>later</i> than the options get built, which
/// happens on the first resolution of <see cref="ITenantSettingsSerializer"/>. An app whose layout
/// reads a built-in setting before anything touches a plugin therefore builds its options first,
/// and a snapshot taken at that moment would miss the plugin permanently — verified: the plugin's
/// metadata was absent from options built one step too early. <see cref="Live"/> closes that by
/// consulting the registry at lookup time instead.</para>
///
/// <para><b>Manual registration still works</b> and takes precedence:
/// <c>AddTenantsExtension(o =&gt; o.AddJsonContext(MyContext.Default))</c> is folded in ahead of
/// anything here, which is how a hand-written context overrides a generated description.</para>
/// </remarks>
public static class TenantSettingsMetadata
{
    private static readonly List<IJsonTypeInfoResolver> Resolvers = [];

    /// <summary>
    /// Adds a resolver. Called by generated module initializers; safe to call directly, and
    /// idempotent per instance so a repeated call cannot lengthen the chain.
    /// </summary>
    /// <remarks>
    /// Locked rather than lock-free: module initializers can run on different threads when
    /// assemblies load concurrently, and this happens a handful of times per process.
    /// </remarks>
    public static void Register(IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        lock (Resolvers)
        {
            if (!Resolvers.Contains(resolver))
                Resolvers.Add(resolver);
        }
    }

    /// <summary>
    /// A resolver that consults the registry <b>at lookup time</b> rather than capturing a
    /// snapshot, so an assembly that loads after the options were built still contributes.
    /// </summary>
    /// <remarks>
    /// <para>The distinction is not theoretical. Options are built once, on first resolution of
    /// <see cref="ITenantSettingsSerializer"/>; assemblies load lazily, when something first names
    /// a type in them. An app whose layout reads a built-in setting before anything touches a
    /// plugin builds the options first — and a snapshot taken there would miss the plugin
    /// permanently, silently downgrading it to reflection. Measured: with a snapshot the plugin's
    /// metadata was absent; consulting the registry live, it is found.</para>
    ///
    /// <para>The cost is one extra indirection per <i>uncached</i> type lookup. System.Text.Json
    /// caches the resolved <see cref="JsonTypeInfo"/> per type on the options instance, so this
    /// runs a handful of times per process, not per read.</para>
    /// </remarks>
    internal static readonly IJsonTypeInfoResolver Live = new LiveResolver();

    private sealed class LiveResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            IJsonTypeInfoResolver[] current;
            lock (Resolvers)
            {
                if (Resolvers.Count == 0) return null;
                current = [.. Resolvers];
            }

            foreach (var resolver in current)
            {
                if (resolver.GetTypeInfo(type, options) is { } info)
                    return info;
            }

            return null;
        }
    }
}
