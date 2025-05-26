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

    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        await base.OnInitializedAsync(cancellationToken);

        ThrowIfCancellationRequested(cancellationToken);

        // Automatyczna persystencja stanu
        _persistingSubscription = PersistentComponentState.RegisterOnPersisting(PersistState);

        // Próba przywrócenia stanu
        if (PersistentComponentState.TryTakeFromJson<T>(StateKey, out var restored))
        {
            Model = restored;
        }
        else
        {
            await LoadDataAsync(cancellationToken);
        }
    }

    protected override async Task OnParametersSetAsync(CancellationToken cancellationToken)
    {
        await base.OnParametersSetAsync(cancellationToken);
        await LoadDataAsync(cancellationToken);
    }

    /// <summary>
    /// Metoda do nadpisania - tutaj kod który musi się wykonać by pojawiły się dane na stronie
    /// </summary>
    protected virtual async Task<T?> LoadAsync(CancellationToken cancellationToken)
        => await Task.FromResult(default(T));

    /// <summary>
    /// Ładuje dane i aktualizuje stan
    /// </summary>
    private async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            ThrowIfCancellationRequested(cancellationToken);
            Model = await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Operacja została anulowana - normalna sytuacja
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
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
    {
        await LoadDataAsync(cancellationToken);
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
        try
        {
            var token = CancellationTokenSource?.Token ?? CancellationToken.None;
            // Najpierw przeładuj dane
            await LoadDataAsync(token);

            if (token.IsCancellationRequested)
                return;

            // Potem wywołaj standardowe odświeżanie z klasy bazowej
            base.OnRefreshChangeAsync();
        }
        catch (OperationCanceledException)
        {
            // Operacja została anulowana - normalny przebieg
        }
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