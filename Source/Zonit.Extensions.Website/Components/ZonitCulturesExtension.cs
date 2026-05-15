using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Cultures;

namespace Zonit.Extensions;

public sealed class ZonitCulturesExtension : ComponentBase, IDisposable
{
    [Inject]
    ICultureManager Culture { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    string CultureName { get; set; } = null!;
    PersistingComponentStateSubscription persistingSubscription;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String, trimming-safe.")]
    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<string>("ZonitCulturesExtension", out var restored))
            CultureName = Culture.Current.Value;
        else
            CultureName = restored!;

        Culture.SetCulture(CultureName);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Persisted value is System.String, trimming-safe.")]
    private Task PersistData()
    {
        ApplicationState.PersistAsJson("ZonitCulturesExtension", CultureName);

        return Task.CompletedTask;
    }

    public void Dispose()
        => persistingSubscription.Dispose();
}