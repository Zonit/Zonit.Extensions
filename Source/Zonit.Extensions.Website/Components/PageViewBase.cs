using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Website;

/// <summary>
/// Prosta klasa bazowa dla komponentów wyświetlających dane
/// </summary>
/// <typeparam name="TViewModel">Typ modelu danych</typeparam>
public class PageViewBase<TViewModel> : ExtensionsBase where TViewModel : class
{
    /// <summary>
    /// Model danych do wyświetlenia
    /// </summary>
    protected TViewModel? Model { get; set; }

    /// <summary>
    /// Czy dane są aktualnie ładowane
    /// </summary>
    protected bool IsLoading { get; private set; }
    
    protected virtual bool PersistentModel { get; } = true;

    [Inject]
    protected PersistentComponentState PersistentComponentState { get; set; } = default!;

    private PersistingComponentStateSubscription? _persistingSubscription;
    private string StateKey => $"{GetType().Name}_Model";

    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        await base.OnInitializedAsync(cancellationToken);

        // Nie ładuj danych jeśli komponent jest już disposed lub token anulowany
        if (IsDisposed || cancellationToken.IsCancellationRequested)
            return;

        ThrowIfCancellationRequested(cancellationToken);

        // Zarejestruj persystencję tylko gdy potrzebna
        if (PersistentModel)
        {
            _persistingSubscription = PersistentComponentState.RegisterOnPersisting(PersistState);

            // Próba odzyskania modelu z persystencji
            if (PersistentComponentState.TryTakeFromJson<TViewModel>(StateKey, out var restored))
            {
                Model = restored;
                // Nie ładuj danych ponownie jeśli już odzyskano z persystencji
                return;
            }
        }

        // Ładuj dane tylko jeśli nie odzyskano z persystencji
        await LoadDataAsync(cancellationToken);
    }

    protected override async Task OnParametersSetAsync(CancellationToken cancellationToken)
    {
        await base.OnParametersSetAsync(cancellationToken);

        // Ładuj dane tylko gdy model jest null (nie został odzyskany z persystencji)
        // lub gdy persystencja jest wyłączona
        if (!PersistentModel && Model == null)
        {
            await LoadDataAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Metoda do nadpisania - tutaj kod który musi się wykonać by pojawiły się dane na stronie
    /// </summary>
    protected virtual async Task<TViewModel?> LoadAsync(CancellationToken cancellationToken)
        => await Task.FromResult(default(TViewModel));

    /// <summary>
    /// Ładuje dane i aktualizuje stan
    /// </summary>
    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        // Sprawdź czy to nie jest prerendering, który zostanie anulowany
        if (cancellationToken.IsCancellationRequested)
            return;

        if(IsLoading)
            return;

        IsLoading = true;
        StateHasChanged();

        try
        {
            ThrowIfCancellationRequested(cancellationToken);
            Model = await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Nie rzucaj wyjątku jeśli to anulowanie podczas przejścia render modes
            if (!IsDisposed)
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
            if (!cancellationToken.IsCancellationRequested && !IsDisposed)
            {
                IsLoading = false;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Publiczna metoda do odświeżenia danych
    /// </summary>
    protected async Task RefreshAsync(CancellationToken cancellationToken = default)
        => await LoadDataAsync(cancellationToken);

    /// <summary>
    /// Automatyczne zapisywanie stanu
    /// </summary>
    private Task PersistState()
    {
        if (PersistentModel && Model != null)
            PersistentComponentState.PersistAsJson(StateKey, Model);

        return Task.CompletedTask;
    }

    protected override async void OnRefreshChangeAsync()
    {
        base.OnRefreshChangeAsync();

        try
        {
            if (IsLoading)
                return;

            await RefreshAsync(CancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Błąd podczas odświeżania komponentu {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_persistingSubscription != null)
            {
                _persistingSubscription?.Dispose();
                _persistingSubscription = null;
            }
        }
        base.Dispose(disposing);
    }
}