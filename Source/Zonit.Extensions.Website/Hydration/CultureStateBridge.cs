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
/// <para>Persists a plain <see cref="string"/> rather than the <see cref="Culture"/> VO
/// directly: the VO is reconstructed deterministically from the BCP 47 tag, the
/// payload stays minimal, and STJ has zero trimming concerns over a primitive.</para>
///
/// <para><b>Native AOT / PublishTrimmed.</b> See the shared note on
/// <see cref="IPersistentStateProvider"/>. Even a <see cref="string"/> payload needs a
/// <c>TypeInfoResolver</c> on the framework's options instance — measured, not assumed — so both
/// calls are gated on <see cref="HydrationSerialization.IsAvailable"/>.</para>
/// </remarks>
internal sealed class CultureStateBridge(ICultureManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitCulturesExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String — STJ binds it with a built-in converter, no member reflection, so the trimmer has nothing to remove.")]
    public void Restore(PersistentComponentState state)
    {
        if (!HydrationSerialization.IsAvailable)
            return;

        if (state.TryTakeFromJson<string>(Key, out var restored) && !string.IsNullOrWhiteSpace(restored))
            manager.SetCulture(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String — STJ binds it with a built-in converter, no member reflection, so the trimmer has nothing to remove.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (!HydrationSerialization.IsAvailable)
                return Task.CompletedTask;

            var current = manager.Current;
            if (current.HasValue)
                state.PersistAsJson(Key, current.Value);
            return Task.CompletedTask;
        });
}
