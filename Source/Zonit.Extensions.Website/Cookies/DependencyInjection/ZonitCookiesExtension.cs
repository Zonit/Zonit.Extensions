using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Website.Abstractions.Cookies.Models;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions;

/// <summary>
/// Blazor component that persists the user's cookie consent list across prerender → interactive boundary.
/// </summary>
/// <remarks>
/// <para><strong>AOT/Trimming:</strong> uses <see cref="PersistentComponentState.TryTakeFromJson"/> and
/// <see cref="PersistentComponentState.PersistAsJson"/> over <see cref="CookieModel"/>. Safe because
/// <see cref="CookieModel"/> is a public DTO and <c>[DynamicDependency]</c> on <see cref="OnInitialized"/>
/// instructs the trimmer to keep its public properties/constructors.</para>
/// </remarks>
public sealed class ZonitCookiesExtension : ComponentBase, IDisposable
{
    [Inject]
    ICookiesRepository Cookie { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    List<CookieModel> Cookies { get; set; } = null!;

    PersistingComponentStateSubscription persistingSubscription;

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CookieModel))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "CookieModel public members are kept via [DynamicDependency] above.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "CookieModel is a simple DTO; JSON source-generated serialization not required here.")]
    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<List<CookieModel>>("ZonitCookiesExtension", out var restored))
            Cookies = Cookie.GetCookies();
        else
            Cookies = restored!;
        
        Cookie.Inicjalize(Cookies);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "CookieModel is kept via [DynamicDependency] on the enclosing class.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "CookieModel is a simple DTO; JSON source-generated serialization not required here.")]
    private Task PersistData()
    {
        ApplicationState.PersistAsJson("ZonitCookiesExtension", Cookies);

        return Task.CompletedTask;
    }

    public void Dispose()
        => persistingSubscription.Dispose();
}