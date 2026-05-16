﻿using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Organizations;

namespace Zonit.Extensions;

public sealed class ZonitOrganizationsExtension : ComponentBase, IDisposable
{
    [Inject]
    IWorkspaceManager Workspace { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    StateModel? State { get; set; }

    PersistingComponentStateSubscription persistingSubscription;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel and the types it transitively references (WorkspaceModel, OrganizationModel) are all top-level POCO DTOs in this package. Trim-keep is provided structurally by the OrganizationsStateJsonContext source-generated JsonSerializerContext — see same folder. The reflective PersistAsJson/TryTakeFromJson is used only because .NET 10's PersistentComponentState has no JsonTypeInfo overload yet; .NET 11 will (see Docs/NET11-Migration.md) and these suppressions disappear.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Świadomy kompromis: w .NET 10 PersistentComponentState ma wyłącznie refleksyjne PersistAsJson/TryTakeFromJson. Pod pełnym Native AOT operacja rzuci NotSupportedException — konsumenci AOT muszą wyłączyć tę komponentę aż do .NET 11. OrganizationsStateJsonContext jest już wygenerowany i czeka na overload PersistAsJson(JsonTypeInfo) — patrz Docs/NET11-Migration.md.")]
    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<StateModel>("ZonitOrganizationsExtension", out var restored))
            State = Workspace.State;
        else
            State = restored!;

        if(State is not null)
            Workspace.Initialize(State);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel and the types it transitively references (WorkspaceModel, OrganizationModel) are all top-level POCO DTOs in this package. Trim-keep is provided structurally by the OrganizationsStateJsonContext source-generated JsonSerializerContext — see same folder. The reflective PersistAsJson is used only because .NET 10's PersistentComponentState has no JsonTypeInfo overload yet; .NET 11 will (see Docs/NET11-Migration.md) and these suppressions disappear.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Świadomy kompromis: w .NET 10 PersistentComponentState ma wyłącznie refleksyjne PersistAsJson. Pod Native AOT rzuci NotSupportedException. OrganizationsStateJsonContext jest już wygenerowany i czeka na overload PersistAsJson(JsonTypeInfo) — patrz Docs/NET11-Migration.md.")]
    private Task PersistData()
    {
        ApplicationState.PersistAsJson("ZonitOrganizationsExtension", State);

        return Task.CompletedTask;
    }

    public void Dispose()
        => persistingSubscription.Dispose();
}