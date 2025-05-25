using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website;

/// <summary>
/// Prosta klasa bazowa dla komponentów wyświetlających dane
/// </summary>
/// <typeparam name="T">Typ modelu danych</typeparam>
public class PageViewBase<T> : ExtensionsBase
{
    /// <summary>
    /// Model danych do wyświetlenia
    /// </summary>
    public T? Model { get; set; }

    /// <summary>
    /// Czy dane są aktualnie ładowane
    /// </summary>
    protected bool IsLoading { get; private set; }

    [Inject]
    protected PersistentComponentState PersistentComponentState { get; set; } = default!;

    private PersistingComponentStateSubscription? _persistingSubscription;
    private string StateKey => $"{GetType().Name}_Model";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Automatyczna persystencja stanu
        _persistingSubscription = PersistentComponentState.RegisterOnPersisting(PersistState);

        // Próba przywrócenia stanu
        if (PersistentComponentState.TryTakeFromJson<T>(StateKey, out var restored))
        {
            Model = restored;
        }
        else
        {
            await LoadDataAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await LoadDataAsync();
    }

    /// <summary>
    /// Metoda do nadpisania - tutaj kod który musi się wykonać by pojawiły się dane na stronie
    /// </summary>
    protected virtual async Task<T?> LoadAsync()
    {
        await Task.CompletedTask;
        return default(T);
    }

    /// <summary>
    /// Ładuje dane i aktualizuje stan
    /// </summary>
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            Model = await LoadAsync();
            await PersistState();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Publiczna metoda do odświeżenia danych
    /// </summary>
    protected async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// Automatyczne zapisywanie stanu
    /// </summary>
    private Task PersistState()
    {
        if (Model != null)
        {
            PersistentComponentState.PersistAsJson(StateKey, Model);
        }
        return Task.CompletedTask;
    }

    protected override async void OnRefreshChangeAsync()
    {
        // Najpierw przeładuj dane
        await LoadDataAsync();

        // Potem wywołaj standardowe odświeżanie z klasy bazowej
        base.OnRefreshChangeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _persistingSubscription?.Dispose();
        }
        base.Dispose(disposing);
    }
}