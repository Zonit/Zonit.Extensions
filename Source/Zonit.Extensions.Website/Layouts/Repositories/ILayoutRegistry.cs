using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Website.Layouts.Repositories;

/// <summary>
/// Process-wide registry mapping a string layout key (e.g. <c>"Minimal"</c>,
/// <c>"Dashboard.404"</c>) to a concrete <see cref="Type"/> that derives from
/// <c>LayoutComponentBase</c>. The registry is the indirection that lets a plug-in
/// reference a layout <em>by name</em> without taking an assembly dependency on the
/// host that owns the layout class.
/// </summary>
/// <remarks>
/// <para><b>Lifetime.</b> Singleton — the map is built once during DI configuration
/// (via <c>services.AddWebsiteLayout&lt;TLayout&gt;(key)</c>) and is read concurrently
/// from every circuit / request thereafter. No mutation after the container is built.</para>
///
/// <para><b>Key semantics.</b> Case-insensitive (<see cref="StringComparer.OrdinalIgnoreCase"/>).
/// Re-registering the same key with a different type overwrites; this is intentional —
/// it lets a host swap a built-in layout for a custom one by registering the same
/// canonical key (e.g. overwrite <c>"Zonit.Minimal"</c>).</para>
///
/// <para><b>AOT-safety.</b> The registry only stores and returns <see cref="Type"/>
/// references that were captured at compile time via the generic
/// <c>AddWebsiteLayout&lt;TLayout&gt;</c> overload. No reflective discovery, no
/// runtime type loading.</para>
/// </remarks>
public interface ILayoutRegistry
{
    /// <summary>
    /// Registers a layout type under the given key. Overwrites any prior registration
    /// for the same key (case-insensitive). Prefer the typed
    /// <c>services.AddWebsiteLayout&lt;TLayout&gt;(key)</c> extension over calling this
    /// directly — the extension ensures the layout type is preserved by the trimmer.
    /// </summary>
    void Register(string key,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                  | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type layoutType);

    /// <summary>
    /// Tries to resolve a layout type for the given key.
    /// </summary>
    /// <param name="key">Case-insensitive layout key.</param>
    /// <param name="layoutType">Resolved type when the key is registered; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key resolves; <see langword="false"/> otherwise.</returns>
    bool TryResolve(string key, [NotNullWhen(true)] out Type? layoutType);

    /// <summary>
    /// Enumerates the registered keys — useful for diagnostics / admin UIs listing
    /// available layouts. Order is not guaranteed.
    /// </summary>
    IReadOnlyCollection<string> Keys { get; }
}
