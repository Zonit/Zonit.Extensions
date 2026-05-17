using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Auth.Repositories;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the authenticated <see cref="Identity"/> across the prerender → interactive
/// boundary. Replaces the legacy <c>ZonitIdentityExtension</c> ComponentBase bridge
/// — same persistence key (<c>ZonitIdentityExtension</c>) so any in-flight SSR blob
/// the browser already received continues to deserialise after deployment.
/// </summary>
/// <remarks>
/// <para><b>Restore semantics.</b> The scoped <see cref="IAuthenticatedRepository"/>
/// in the circuit scope starts at <see cref="Identity.Empty"/>; calling
/// <see cref="IAuthenticatedRepository.Initialize"/> with the persisted value
/// makes the Blazor circuit see exactly the same identity SSR rendered against.</para>
///
/// <para><b>AOT / trimming.</b> <see cref="Identity"/> ships with a hand-written
/// <c>JsonConverter</c>; no reflection-based property binding is involved, so the
/// trimmer doesn't need extra <c>[DynamicallyAccessedMembers]</c> annotations to
/// preserve the type's surface.</para>
/// </remarks>
internal sealed class AuthStateBridge(IAuthenticatedRepository repository) : IPersistentStateProvider
{
    private const string Key = "ZonitIdentityExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Identity VO has a hand-written JsonConverter; no reflection-based binding involved.")]
    public void Restore(PersistentComponentState state)
    {
        if (state.TryTakeFromJson<Identity>(Key, out var restored) && restored.HasValue)
            repository.Initialize(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Identity VO has a hand-written JsonConverter; no reflection-based binding involved.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            var current = repository.Current;
            // Anonymous → no point shipping `Identity.Empty` to the client; circuit
            // already starts empty and every byte counts in the SSR HTML payload.
            if (current.HasValue)
                state.PersistAsJson(Key, current);
            return Task.CompletedTask;
        });
}
