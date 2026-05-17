using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Website.Cookies.Models;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions.Website.Hydration;

/// <summary>
/// Carries the request-scoped <see cref="CookieModel"/> snapshot across the
/// prerender → interactive boundary. Without this, the circuit's
/// <c>ICookieProvider</c> reads <see cref="List{T}"/> of zero cookies even when
/// the SSR request carried a populated jar.
/// </summary>
internal sealed class CookieStateBridge(ICookiesRepository repository) : IPersistentStateProvider
{
    private const string Key = "ZonitCookiesExtension";

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "CookieModel is a public DTO; the trimmer keeps its public properties and constructors via [DynamicDependency] on the legacy ZonitCookiesExtension component class.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "CookieModel is a simple DTO; JSON source-generated serialization not required here.")]
    public void Restore(PersistentComponentState state)
    {
        if (state.TryTakeFromJson<List<CookieModel>>(Key, out var restored) && restored is not null)
            repository.Initialize(restored);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore above.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Same rationale as Restore above.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            var cookies = repository.GetCookies();
            if (cookies.Count > 0)
                state.PersistAsJson(Key, cookies);
            return Task.CompletedTask;
        });
}
