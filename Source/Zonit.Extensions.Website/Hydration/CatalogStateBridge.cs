using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the user's project catalog snapshot (<see cref="StateModel"/> with the
/// active <c>CatalogModel</c> + visible <c>ProjectModel</c> list) across the
/// prerender → interactive boundary. Twin of <c>WorkspaceStateBridge</c> — see
/// that type for the design rationale, the trimming contract and the
/// <see cref="HydrationSerialization.IsAvailable"/> gate.
/// </summary>
internal sealed class CatalogStateBridge(ICatalogManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitProjectsExtension";

    private const DynamicallyAccessedMemberTypes JsonMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    [DynamicDependency(JsonMembers, typeof(StateModel))]
    [DynamicDependency(JsonMembers, typeof(CatalogModel))]
    [DynamicDependency(JsonMembers, typeof(ProjectModel))]
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
    [DynamicDependency(JsonMembers, typeof(CatalogModel))]
    [DynamicDependency(JsonMembers, typeof(ProjectModel))]
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
