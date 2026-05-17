using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Organizations;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the user's workspace snapshot (<see cref="StateModel"/> with the active
/// <c>WorkspaceModel</c> + visible <c>OrganizationModel</c> list) across the
/// prerender → interactive boundary. Replaces the legacy
/// <c>ZonitOrganizationsExtension</c> ComponentBase bridge — same persistence key.
/// </summary>
/// <remarks>
/// <b>Why ship the whole snapshot.</b> A second-round backend hit on every circuit
/// start to re-fetch the user's organizations would defeat the point of the bridge.
/// The SSR pass already paid that cost; we hand the result over to the circuit
/// verbatim so the client lands fully populated on first paint.
/// </remarks>
internal sealed class WorkspaceStateBridge(IWorkspaceManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitOrganizationsExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel + WorkspaceModel + OrganizationModel are top-level POCO DTOs; OrganizationsStateJsonContext keeps the trim graph intact. Reflective PersistAsJson<T> overload is used only because .NET 10's PersistentComponentState lacks the JsonTypeInfo-accepting variant — see Docs/NET11-Migration.md.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Native AOT requires .NET 11's JsonTypeInfo overload on PersistentComponentState (tracked in Docs/NET11-Migration.md). Until then this bridge silently no-ops under full AOT — degraded UX, no crash.")]
    public void Restore(PersistentComponentState state)
    {
        if (state.TryTakeFromJson<StateModel>(Key, out var restored) && restored is not null)
            manager.Initialize(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore above.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Same rationale as Restore above.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (manager.State is { } snapshot)
                state.PersistAsJson(Key, snapshot);
            return Task.CompletedTask;
        });
}
