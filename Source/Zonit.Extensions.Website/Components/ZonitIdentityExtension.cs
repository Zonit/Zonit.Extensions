using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Auth.Repositories;

namespace Zonit.Extensions;

/// <summary>
/// Razor component that bridges the prerendered <see cref="IAuthenticatedRepository"/> state
/// across Blazor render modes via <see cref="PersistentComponentState"/>. Persists / restores
/// the <see cref="Identity"/> snapshot using its hand-written JSON converter (AOT-safe).
/// </summary>
public sealed class ZonitIdentityExtension : ComponentBase, IDisposable
{
    [Inject]
    IAuthenticatedRepository AuthenticatedRepository { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    private const string StateKey = "ZonitIdentityExtension";

    private PersistingComponentStateSubscription _subscription;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Identity VO has a hand-written, AOT-safe JsonConverter; no reflection-based binding involved.")]
    protected override void OnInitialized()
    {
        _subscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (ApplicationState.TryTakeFromJson<Identity>(StateKey, out var restored) && restored.HasValue)
            AuthenticatedRepository.Initialize(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Identity VO has a hand-written, AOT-safe JsonConverter; no reflection-based binding involved.")]
    private Task PersistData()
    {
        ApplicationState.PersistAsJson(StateKey, AuthenticatedRepository.Current);
        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
