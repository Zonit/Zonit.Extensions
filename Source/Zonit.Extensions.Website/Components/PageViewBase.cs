using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Website;

/// <summary>
/// Prosta klasa bazowa dla komponentów wyświetlających dane z opcjonalną persystencją modelu
/// między prerenderem a interactive renderem (Blazor SSR → WebAssembly/Server).
/// </summary>
/// <remarks>
/// <para><strong>Trimming (IL2026).</strong> <c>PersistAsJson</c> / <c>TryTakeFromJson</c> w
/// .NET 10 to wyłącznie refleksyjny serializator <c>System.Text.Json</c>. Adnotacja
/// <c>[DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)]</c>
/// na <typeparamref name="TViewModel"/> zachowuje wszystkie składowe wymagane przez STJ
/// — IL2026 jest realnie złagodzony i lokalna supresja jest uczciwa. Zagnieżdżone typy
/// (właściwość, której typem jest kolejne DTO) nie są objęte tą gwarancją: adnotacja
/// nie jest rekurencyjna, więc modele o zagnieżdżonym grafie trzeba samodzielnie
/// zakotwiczyć w konsumencie albo wyłączyć persystencję.</para>
///
/// <para><strong>Trimming / Native AOT w runtime.</strong> Gdy przełącznik
/// <c>System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault</c> jest wyłączony
/// (SDK ustawia go na <c>false</c> dla każdej publikacji <c>PublishTrimmed</c>, a więc
/// i dla <c>PublishAot</c>), refleksyjny STJ nie ma resolvera i każde
/// <c>PersistAsJson</c> rzuciłoby wyjątek. Dlatego oba wywołania są bramkowane tym
/// przełącznikiem: persystencja modelu wtedy cicho się wyłącza (komponent po prostu
/// ładuje dane ponownie po hydratacji) zamiast wywracać obwód. Blazor WebAssembly nie
/// jest tym dotknięty — jego SDK jawnie przywraca przełącznik na <c>true</c>.</para>
///
/// <para><strong>Dlaczego nie ma tu ścieżki z <c>JsonTypeInfo</c>.</strong>
/// <see cref="PersistentComponentState"/> w .NET 10 udostępnia publicznie, pod kluczem, wyłącznie
/// refleksyjne <c>PersistAsJson&lt;T&gt;</c> / <c>TryTakeFromJson&lt;T&gt;</c>;
/// <c>PersistAsBytes</c> i <c>TryTakeBytes</c> istnieją, ale są <c>internal</c>. Obie metody JSON
/// serializują na wbudowanej instancji <c>JsonSerializerOptions</c> bez <c>TypeInfoResolver</c>
/// (potwierdzone w IL <c>Microsoft.AspNetCore.Components</c> 10.0.9), więc nie ma parametru, do
/// którego dałoby się podać source-generowany <c>JsonTypeInfo</c>.</para>
///
/// <para><strong>Uściślenie:</strong> <c>PersistentComponentStateSerializer&lt;T&gt;</c> jest
/// w .NET 10 publiczny i nadpisywalny, ale <c>PersistAsJson</c> go <em>nie</em> konsultuje —
/// jedynym jego konsumentem jest deklaratywna ścieżka <c>[PersistentState]</c> na właściwościach
/// komponentu (<c>PersistentValueProviderComponentSubscription</c>), która rozwiązuje serializator
/// przez <c>MakeGenericType</c> + <c>IServiceProvider</c> i pisze wewnętrznym
/// <c>PersistAsBytes</c>. Persystencja modelu spod klucza <c>{Komponent}_Model</c> nie może z tego
/// skorzystać bez porzucenia kluczy. Szczegóły i pomiary:
/// <see cref="Zonit.Extensions.Website.Hydration.HydrationSerialization"/>.</para>
/// </remarks>
/// <typeparam name="TViewModel">Typ modelu danych.</typeparam>
public class PageViewBase<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties
      | DynamicallyAccessedMemberTypes.PublicFields
      | DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel>
    : PageBase where TViewModel : class
{
    /// <summary>
    /// Model danych do wyświetlenia
    /// </summary>
    protected virtual TViewModel? Model { get; set; }

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
            // ExtensionsBase.OnRefreshChangeAsync woła OnInitializedAsync ponownie przy
            // każdej zmianie Workspace/Catalog/Tenant. Bez zwolnienia poprzedniego uchwytu
            // każde odświeżenie zostawiałoby kolejny żywy callback persystencji na czas
            // życia obwodu — model byłby serializowany N razy w fazie persist.
            _persistingSubscription?.Dispose();
            _persistingSubscription = PersistentComponentState.RegisterOnPersisting(PersistState);

            // Próba odzyskania modelu z persystencji
            if (TryTakeModelFromState(out var restored))
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
    /// Publiczna metoda do odświeżenia danych — dla przeładowań inicjowanych przez samą stronę.
    /// </summary>
    /// <remarks>
    /// Odświeżenia wywołane zdarzeniami dostawców (Workspace / Catalog / Tenant) obsługuje
    /// <c>ExtensionsBase.OnRefreshChangeAsync</c>, które ponownie uruchamia
    /// <see cref="OnInitializedAsync(CancellationToken)"/>, a więc i <c>LoadAsync</c>.
    /// Ta klasa celowo <em>nie</em> nadpisuje już <c>OnRefreshChangeAsync</c>: poprzednia
    /// wersja wołała najpierw <c>base</c> (czyli <c>OnInitializedAsync</c> → ładowanie),
    /// a zaraz potem <see cref="RefreshAsync"/> (drugie ładowanie). Ponieważ <c>base</c>
    /// jest <c>async void</c> i wraca przy pierwszym <c>await</c>, strażnik
    /// <c>if (IsLoading) return;</c> zwykle jeszcze nie widział podniesionej flagi
    /// i backend dostawał dwa zapytania na jedną zmianę.
    /// </remarks>
    protected async Task RefreshAsync(CancellationToken cancellationToken = default)
        => await LoadDataAsync(cancellationToken);

    /// <summary>Zapisuje aktualny <see cref="Model"/> w <see cref="PersistentComponentState"/>.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Faktyczne złagodzenie: [DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)] na TViewModel gwarantuje, że trimmer zachowa składowe TViewModel wymagane przez refleksyjny System.Text.Json. Adnotacja nie jest rekurencyjna — ograniczenie opisano w remarks klasy.")]
    private Task PersistState()
    {
        if (!PersistentModel || Model is null)
            return Task.CompletedTask;

        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            // Bez resolvera refleksyjnego PersistAsJson rzuciłby wyjątkiem i wywrócił
            // fazę persist całego prerenderu. Degradacja jest bezpieczna: brak snapshotu
            // oznacza tylko, że komponent po hydratacji sam ponownie zawoła LoadAsync.
            Logger.LogDebug(
                "Persystencja modelu {ComponentType} pominięta: refleksyjny System.Text.Json jest wyłączony (PublishTrimmed/PublishAot).",
                GetType().Name);
            return Task.CompletedTask;
        }

        PersistentComponentState.PersistAsJson(StateKey, Model);
        return Task.CompletedTask;
    }

    /// <summary>Próbuje odtworzyć <see cref="Model"/> z <see cref="PersistentComponentState"/>.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Faktyczne złagodzenie: [DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)] na TViewModel gwarantuje, że trimmer zachowa składowe TViewModel wymagane przez refleksyjny System.Text.Json. Adnotacja nie jest rekurencyjna — ograniczenie opisano w remarks klasy.")]
    private bool TryTakeModelFromState(out TViewModel? value)
    {
        // Symetrycznie do PersistState: bez refleksyjnego STJ nie ma czego odczytać
        // (strona serwerowa też nic nie zapisała), więc odpowiadamy „brak snapshotu”.
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            value = null;
            return false;
        }

        return PersistentComponentState.TryTakeFromJson(StateKey, out value);
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