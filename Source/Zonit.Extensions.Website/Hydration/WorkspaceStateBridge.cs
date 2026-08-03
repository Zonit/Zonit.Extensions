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
/// <para><b>Why ship the whole snapshot.</b> A second-round backend hit on every circuit
/// start to re-fetch the user's organizations would defeat the point of the bridge.
/// The SSR pass already paid that cost; we hand the result over to the circuit
/// verbatim so the client lands fully populated on first paint.</para>
///
/// <para><b>Trimming.</b> The persisted graph is exactly three POCOs
/// (<see cref="StateModel"/> → <see cref="WorkspaceModel"/> → <see cref="OrganizationModel"/>),
/// each rooted by an explicit <see cref="DynamicDependencyAttribute"/> on the methods below.
/// A <see cref="DynamicallyAccessedMembersAttribute"/> on the payload type would not be
/// enough — it does not recurse, and the two nested types are reachable only through
/// property types.</para>
///
/// <para><b>Native AOT / PublishTrimmed.</b> See the shared note on
/// <see cref="IPersistentStateProvider"/>: both calls are gated on
/// <see cref="HydrationSerialization.IsAvailable"/>.</para>
/// </remarks>
internal sealed class WorkspaceStateBridge(IWorkspaceManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitOrganizationsExtension";

    private const DynamicallyAccessedMemberTypes JsonMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    [DynamicDependency(JsonMembers, typeof(StateModel))]
    [DynamicDependency(JsonMembers, typeof(WorkspaceModel))]
    [DynamicDependency(JsonMembers, typeof(OrganizationModel))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Every type the reflective binder walks for this payload is rooted by the [DynamicDependency] attributes on this method, so the members STJ looks up survive trimming.")]
    public void Restore(PersistentComponentState state)
    {
        if (!HydrationSerialization.IsAvailable)
            return;

        if (state.TryTakeFromJson<StateModel>(Key, out var restored) && restored is not null)
            manager.Initialize(restored);
    }

    [DynamicDependency(JsonMembers, typeof(StateModel))]
    [DynamicDependency(JsonMembers, typeof(WorkspaceModel))]
    [DynamicDependency(JsonMembers, typeof(OrganizationModel))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore above.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (!HydrationSerialization.IsAvailable)
                return Task.CompletedTask;

            if (manager.State is { } snapshot)
                state.PersistAsJson(Key, snapshot);
            return Task.CompletedTask;
        });
}
