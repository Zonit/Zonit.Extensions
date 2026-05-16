using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

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
/// — IL2026 jest realnie złagodzony i lokalna supresja jest uczciwa.</para>
///
/// <para><strong>NativeAOT (IL3050).</strong> Refleksyjny STJ <em>nie działa</em> pod
/// pełnym Native AOT (rzuca <see cref="NotSupportedException"/>). Konsumenci publikujący
/// AOT muszą albo nadpisać <see cref="PersistentModel"/> na <c>false</c>, albo zaczekać
/// na overload <c>PersistAsJson(string, T, JsonTypeInfo&lt;T&gt;)</c> obiecywany w .NET 11.</para>
///
/// <para><strong>Stan na .NET 11 ready.</strong> Generator
/// <c>Zonit.Extensions.Website.SourceGenerators</c> emituje per-VM
/// <c>JsonSerializerContext</c> i nadpisuje <see cref="ViewModelMetadata{T}.JsonTypeInfo"/>
/// — gdy framework doda overload akceptujący <c>JsonTypeInfo</c>, wystarczy zmienić wywołania
/// w <c>PersistState</c> / <c>TryTakeModelFromState</c> i obie supresje znikają.
/// Patrz <c>Docs/NET11-Migration.md</c>.</para>
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
    /// Publiczna metoda do odświeżenia danych
    /// </summary>
    protected async Task RefreshAsync(CancellationToken cancellationToken = default)
        => await LoadDataAsync(cancellationToken);

    /// <summary>Zapisuje aktualny <see cref="Model"/> w <see cref="PersistentComponentState"/>.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Faktyczne złagodzenie: [DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)] na TViewModel gwarantuje, że trimmer zachowa wszystkie składowe wymagane przez refleksyjny System.Text.Json. Pod pełnym AOT pozostaje IL3050 — patrz adnotacja niżej.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Świadomy kompromis: w .NET 10 PersistentComponentState ma wyłącznie refleksyjne PersistAsJson/TryTakeFromJson. Pod Native AOT operacja rzuci NotSupportedException — konsumenci AOT mają ustawić PersistentModel=false. Generator Zonit.Extensions.Website.SourceGenerators emituje już JsonSerializerContext per VM (ViewModelMetadata<T>.JsonTypeInfo), więc gdy .NET 11 doda PersistAsJson(JsonTypeInfo<T>) overload, ta supresja znika jednym refactorem — patrz Docs/NET11-Migration.md.")]
    private Task PersistState()
    {
        if (PersistentModel && Model is not null)
            PersistentComponentState.PersistAsJson(StateKey, Model);

        return Task.CompletedTask;
    }

    /// <summary>Próbuje odtworzyć <see cref="Model"/> z <see cref="PersistentComponentState"/>.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Faktyczne złagodzenie: [DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)] na TViewModel gwarantuje, że trimmer zachowa wszystkie składowe wymagane przez refleksyjny System.Text.Json. Pod pełnym AOT pozostaje IL3050 — patrz adnotacja niżej.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Świadomy kompromis: w .NET 10 PersistentComponentState ma wyłącznie refleksyjne PersistAsJson/TryTakeFromJson. Pod Native AOT operacja rzuci NotSupportedException — konsumenci AOT mają ustawić PersistentModel=false. Generator Zonit.Extensions.Website.SourceGenerators emituje już JsonSerializerContext per VM (ViewModelMetadata<T>.JsonTypeInfo), więc gdy .NET 11 doda TryTakeFromJson(JsonTypeInfo<T>) overload, ta supresja znika jednym refactorem — patrz Docs/NET11-Migration.md.")]
    private bool TryTakeModelFromState(out TViewModel? value)
        => PersistentComponentState.TryTakeFromJson(StateKey, out value);

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