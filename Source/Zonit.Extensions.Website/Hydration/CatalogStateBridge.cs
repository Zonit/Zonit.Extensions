using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the user's project catalog snapshot (<see cref="StateModel"/> with the
/// active <c>CatalogModel</c> + visible <c>ProjectModel</c> list) across the
/// prerender → interactive boundary. Twin of <c>WorkspaceStateBridge</c> — see
/// that type for the design rationale.
/// </summary>
internal sealed class CatalogStateBridge(ICatalogManager manager) : IPersistentStateProvider
{
    private const string Key = "ZonitProjectsExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel + CatalogModel + ProjectModel are top-level POCO DTOs; ProjectsStateJsonContext keeps the trim graph intact. Reflective PersistAsJson<T> overload is used only because .NET 10's PersistentComponentState lacks the JsonTypeInfo-accepting variant — see Docs/NET11-Migration.md.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Native AOT requires .NET 11's JsonTypeInfo overload on PersistentComponentState — see Docs/NET11-Migration.md.")]
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
