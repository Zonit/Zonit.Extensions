﻿using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions;

public sealed class ZonitProjectsExtension : ComponentBase, IDisposable
{
    [Inject]
    ICatalogManager Project { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    StateModel? State { get; set; }

    PersistingComponentStateSubscription persistingSubscription;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel is a POCO DTO in this package; trimming is expected to preserve public properties via consumer-side System.Text.Json source generation or default reflection.")]
    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<StateModel>("ZonitProjectsExtension", out var restored))
            State = Project.State;
        else
            State = restored!;

        if (State is not null)
            Project.Initialize(State);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "StateModel is a POCO DTO in this package; trimming is expected to preserve public properties via consumer-side System.Text.Json source generation or default reflection.")]
    private Task PersistData()
    {
        ApplicationState.PersistAsJson("ZonitProjectsExtension", State);

        return Task.CompletedTask;
    }

    public void Dispose()
        => persistingSubscription.Dispose();
}