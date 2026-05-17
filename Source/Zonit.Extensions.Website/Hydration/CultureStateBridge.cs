using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Cultures;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the active culture (BCP 47 string) across the prerender → interactive
/// boundary. Replaces the legacy <c>ZonitCulturesExtension</c> ComponentBase bridge
/// — same persistence key for back-compat with in-flight SSR blobs.
/// </summary>
/// <remarks>
/// Persists a plain <see cref="string"/> rather than the <see cref="Culture"/> VO
/// directly: the VO is reconstructed deterministically from the BCP 47 tag, the
/// payload stays minimal, and STJ has zero AOT/trimming concerns over a primitive.
/// </remarks>
internal sealed class CultureStateBridge(ICultureManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitCulturesExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String, trimming-safe.")]
    public void Restore(PersistentComponentState state)
    {
        if (state.TryTakeFromJson<string>(Key, out var restored) && !string.IsNullOrWhiteSpace(restored))
            manager.SetCulture(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String, trimming-safe.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            var current = manager.Current;
            if (current.HasValue)
                state.PersistAsJson(Key, current.Value);
            return Task.CompletedTask;
        });
}
