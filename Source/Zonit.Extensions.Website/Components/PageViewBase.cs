using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

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

    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Inicjalizacja PageViewBase<{ModelType}> dla {ComponentType}",
            typeof(T).Name, GetType().Name);

        await base.OnInitializedAsync(cancellationToken);

        ThrowIfCancellationRequested(cancellationToken);

        // Automatyczna persystencja stanu
        _persistingSubscription = PersistentComponentState.RegisterOnPersisting(PersistState);
        Logger.LogDebug("Zarejestrowano persystencję stanu dla {ComponentType}", GetType().Name);

        // Próba przywrócenia stanu
        if (PersistentComponentState.TryTakeFromJson<T>(StateKey, out var restored))
        {
            Logger.LogInformation("Przywrócono stan z pamięci podręcznej dla {ComponentType}", GetType().Name);
            Model = restored;
        }
        else
        {
            Logger.LogDebug("Brak zapisanego stanu, ładowanie nowych danych dla {ComponentType}", GetType().Name);
            await LoadDataAsync(cancellationToken);
        }
    }

    protected override async Task OnParametersSetAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Parametry ustawione, przeładowanie danych dla {ComponentType}", GetType().Name);
        await base.OnParametersSetAsync(cancellationToken);
        await LoadDataAsync(cancellationToken);
    }

    /// <summary>
    /// Metoda do nadpisania - tutaj kod który musi się wykonać by pojawiły się dane na stronie
    /// </summary>
    protected virtual async Task<T?> LoadAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Wywołanie domyślnej implementacji LoadAsync dla {ComponentType}", GetType().Name);
        return await Task.FromResult(default(T));
    }

    /// <summary>
    /// Ładuje dane i aktualizuje stan
    /// </summary>
    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        Logger.LogDebug("Rozpoczęcie ładowania danych dla {ComponentType}", GetType().Name);
        StateHasChanged();

        try
        {
            ThrowIfCancellationRequested(cancellationToken);
            Model = await LoadAsync(cancellationToken);
            Logger.LogInformation("Dane zostały pomyślnie załadowane dla {ComponentType}", GetType().Name);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Ładowanie danych anulowane dla {ComponentType}", GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas ładowania danych dla {ComponentType}: {Message}",
                GetType().Name, ex.Message);
            throw;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
                StateHasChanged();
                Logger.LogDebug("Zakończono ładowanie danych dla {ComponentType}", GetType().Name);
            }
        }
    }

    /// <summary>
    /// Publiczna metoda do odświeżenia danych
    /// </summary>
    protected async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Ręczne odświeżenie danych dla {ComponentType}", GetType().Name);
        await LoadDataAsync(cancellationToken);
    }

    /// <summary>
    /// Automatyczne zapisywanie stanu
    /// </summary>
    private Task PersistState()
    {
        if (Model != null)
        {
            Logger.LogDebug("Persystencja stanu dla {ComponentType}", GetType().Name);
            PersistentComponentState.PersistAsJson(StateKey, Model);
        }
        else
        {
            Logger.LogDebug("Brak modelu do zapisu stanu dla {ComponentType}", GetType().Name);
        }
        return Task.CompletedTask;
    }

    protected override async void OnRefreshChangeAsync()
    {
        try
        {
            var token = CancellationTokenSource?.Token ?? CancellationToken.None;
            Logger.LogDebug("OnRefreshChangeAsync: Przeładowanie danych dla {ComponentType}", GetType().Name);

            // Najpierw przeładuj dane
            await LoadDataAsync(token);

            if (token.IsCancellationRequested)
            {
                Logger.LogDebug("OnRefreshChangeAsync anulowany dla {ComponentType}", GetType().Name);
                return;
            }

            // Potem wywołaj standardowe odświeżanie z klasy bazowej
            base.OnRefreshChangeAsync();
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("OnRefreshChangeAsync anulowany przez token dla {ComponentType}", GetType().Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd w OnRefreshChangeAsync dla {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.LogDebug("Zwalnianie zasobów PageViewBase dla {ComponentType}", GetType().Name);

            if (_persistingSubscription != null)
            {
                Logger.LogTrace("Usuwanie subskrypcji persystencji stanu dla {ComponentType}", GetType().Name);
                _persistingSubscription?.Dispose();
                _persistingSubscription = null;
            }
        }
        base.Dispose(disposing);
    }
}