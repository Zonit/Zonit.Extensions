using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Website.Abstractions.Cookies.Models;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions;

public sealed class ZonitCookiesExtension : ComponentBase, IDisposable
{
    [Inject]
    ICookiesRepository Cookie { get; set; } = default!;

    [Inject]
    PersistentComponentState ApplicationState { get; set; } = default!;

    List<CookieModel> Cookies { get; set; } = null!;

    PersistingComponentStateSubscription persistingSubscription;

    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistData);

        if (!ApplicationState.TryTakeFromJson<List<CookieModel>>("ZonitCookiesExtension", out var restored))
            Cookies = Cookie.GetCookies();
        else
            Cookies = restored!;
        
        Cookie.Inicjalize(Cookies);
    }

    private Task PersistData()
    {
        ApplicationState.PersistAsJson("ZonitCookiesExtension", Cookies);

        return Task.CompletedTask;
    }

    public void Dispose()
        => persistingSubscription.Dispose();
}