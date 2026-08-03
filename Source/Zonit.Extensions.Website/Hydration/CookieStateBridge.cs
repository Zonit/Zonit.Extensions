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
/// <remarks>
/// <para><b>Trimming.</b> The payload is <c>List&lt;CookieModel&gt;</c> and
/// <see cref="CookieModel"/> is a flat DTO of primitives, rooted by the
/// <see cref="DynamicDependencyAttribute"/> on the methods below. The previous
/// justification pointed at a <c>[DynamicDependency]</c> on the legacy
/// <c>ZonitCookiesExtension</c> component — a type deleted in 1cfc6d8, i.e. the
/// suppression had no mechanism behind it at all.</para>
///
/// <para><b>Native AOT / PublishTrimmed.</b> See the shared note on
/// <see cref="IPersistentStateProvider"/>: both calls are gated on
/// <see cref="HydrationSerialization.IsAvailable"/>.</para>
/// </remarks>
internal sealed class CookieStateBridge(ICookiesRepository repository) : IPersistentStateProvider
{
    private const string Key = "ZonitCookiesExtension";

    private const DynamicallyAccessedMemberTypes JsonMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    [DynamicDependency(JsonMembers, typeof(CookieModel))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "CookieModel is the only type the reflective binder walks here and it is rooted by the [DynamicDependency] on this method; List<T> needs no extra rooting.")]
    public void Restore(PersistentComponentState state)
    {
        if (!HydrationSerialization.IsAvailable)
            return;

        if (state.TryTakeFromJson<List<CookieModel>>(Key, out var restored) && restored is not null)
            repository.Initialize(restored);
    }

    [DynamicDependency(JsonMembers, typeof(CookieModel))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore above.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (!HydrationSerialization.IsAvailable)
                return Task.CompletedTask;

            var cookies = repository.GetCookies();
            if (cookies.Count > 0)
                state.PersistAsJson(Key, cookies);
            return Task.CompletedTask;
        });
}
