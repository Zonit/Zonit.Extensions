using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Zonit.Extensions.Text;

namespace Zonit.Extensions.Website;

/// <summary>
/// Klasa bazowa dla edytorów (formularzy) pracujących na <typeparamref name="TViewModel"/>.
/// </summary>
/// <remarks>
/// <para><strong>AOT/Trimming:</strong> klasa preferuje statyczne metadane wygenerowane przez
/// <c>Zonit.Extensions.Website.SourceGenerators</c> (włączany automatycznie via paczka NuGet).
/// Dopóki metadata jest dostępna, wszystkie operacje na właściwościach <typeparamref name="TViewModel"/>
/// idą przez wygenerowane delegaty (zero refleksji). W przeciwnym razie używany jest fallback
/// refleksyjny — wciąż bezpieczny, bo trimmer zachowuje członków <typeparamref name="TViewModel"/>
/// dzięki <c>[DynamicallyAccessedMembers]</c>.</para>
/// <para><c>DataAnnotations.Validator.TryValidateObject</c> działa refleksyjnie i wymaga, by
/// wszystkie używane <see cref="ValidationAttribute"/> były zachowane przez trimmer. Wbudowane atrybuty
/// (Required, MinLength, ...) są root-owane przez .NET; <strong>własne</strong> atrybuty walidacji powinny
/// być deklarowane na publicznym typie aby trimmer je zatrzymał. Gdy atrybut zostanie usunięty, walidacja
/// nie wybucha — po prostu <em>przechodzi</em>, więc niepoprawny formularz zostanie przyjęty. To jedyna
/// pozostała cicha ścieżka w tej klasie i jedyna, której framework nie może zamknąć za konsumenta.</para>
///
/// <para><strong>Co NIE jest tu tłumione.</strong> W tej klasie zostały dokładnie dwie
/// <c>[UnconditionalSuppressMessage]</c> — obie IL2026, obie nad realnie
/// <c>[RequiresUnreferencedCode]</c>-owanym API (<c>Validator.TryValidateObject</c> i
/// <c>new ValidationContext(object)</c>). Sześć wcześniejszych supresji IL3050 usunięto:
/// <c>Type.GetProperty</c>/<c>GetProperties</c>, <c>PropertyInfo.Get/SetValue</c>,
/// <c>Validator.TryValidateObject</c> ani <c>ValidationContext..ctor</c> nie noszą
/// <c>[RequiresDynamicCode]</c> (sprawdzone w metadanych Microsoft.NETCore.App.Ref 10.0.9),
/// więc tłumiły diagnostykę, która nigdy nie padała.</para>
///
/// <para><strong>Pola modeli zagnieżdżonych.</strong> <c>@bind-Value="Model.Child.Name"</c> zgłasza
/// <see cref="FieldIdentifier"/> z <c>Model</c> ustawionym na obiekt <em>innego</em> typu niż
/// <typeparamref name="TViewModel"/>. Odczyt takiej wartości refleksją nie jest dowiedziony dla
/// trimmera, więc bazowa implementacja <see cref="GetNestedModelFieldValue"/> zwraca
/// <see langword="null"/> i loguje ostrzeżenie zamiast zgadywać — nadpisz ją, jeśli potrzebujesz
/// tych wartości w <c>OnModelChanged</c> / <c>AutoSaveAsync</c>.</para>
/// </remarks>
public abstract class PageEditBase<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties
      | DynamicallyAccessedMemberTypes.PublicFields
      | DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel>
    : PageViewBase<TViewModel> where TViewModel : class, new()
{
    [SupplyParameterFromForm]
#pragma warning disable CS8765 // Dopuszczanie wartości null dla typu parametru nie jest zgodne z przesłoniętą składową (prawdopodobnie z powodu atrybutów dopuszczania wartości null).
    // Using default! + lazy init in OnInitialized to avoid BL0008 (property initializer + [SupplyParameterFromForm]).
    protected override TViewModel Model { get; set; } = default!;
#pragma warning restore CS8765 // Dopuszczanie wartości null dla typu parametru nie jest zgodne z przesłoniętą składową (prawdopodobnie z powodu atrybutów dopuszczania wartości null).
    protected EditContext? EditContext { get; private set; }
    protected ValidationMessageStore? ValidationMessages { get; private set; }
    protected bool Processing { get; set; } = false;
    protected bool HasChanges { get; private set; } = false;
    public bool IsValid => EditContext?.GetValidationMessages().Any() is false;

    // Konfiguracja zachowań
    protected virtual bool AutoTrimStrings => true;
    protected virtual bool AutoNormalizeWhitespace => true;
    protected virtual bool TrackChanges => true;
    protected virtual bool PreventDuplicateSubmissions => true;

    private DateTime? _lastSubmitTime;
    private readonly TimeSpan _duplicateSubmissionThreshold = TimeSpan.FromSeconds(1);

    // Auto-save na polach
    private readonly Dictionary<string, Timer> _fieldAutoSaveTimers = [];
    private readonly Dictionary<string, object?> _lastFieldValues = [];

    // "typ.pole" już zgłoszone przez GetNestedModelFieldValue — patrz tam.
    private readonly HashSet<string> _reportedNestedFields = [];
    protected virtual TimeSpan AutoSaveDelay => TimeSpan.FromMilliseconds(800);

    protected override void OnInitialized()
    {
        InitializeEditContext();
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        InitializeEditContext();
        base.OnParametersSet();
    }

    private void InitializeEditContext()
    {
        Model ??= new TViewModel();

        if (EditContext == null || !ReferenceEquals(EditContext.Model, Model))
        {
            if (EditContext is not null)
            {
                EditContext.OnFieldChanged -= HandleModelFieldChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
            }

            EditContext = new EditContext(Model);
            EditContext.OnFieldChanged += HandleModelFieldChanged;
            EditContext.OnValidationRequested += HandleValidationRequested;
            ValidationMessages = new ValidationMessageStore(EditContext);

            if (TrackChanges)
                HasChanges = false;
        }
    }

    private async void HandleModelFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (TrackChanges)
            HasChanges = true;

        var fieldName = e.FieldIdentifier.FieldName;
        var currentValue = GetFieldValue(e.FieldIdentifier);
        var previousValue = _lastFieldValues.TryGetValue(fieldName, out var prev) ? prev : null;

        // Zachowaj aktualną wartość dla przyszłych porównań
        _lastFieldValues[fieldName] = currentValue;

        // Auto-save dla konkretnego pola
        HandleFieldAutoSave(e.FieldIdentifier);

        // Wywołaj OnModelChanged dla każdej zmiany modelu
        var cancellationToken = ComponentToken;
        await OnModelChanged(fieldName, previousValue, currentValue, cancellationToken);

        StateHasChanged();
    }

    protected virtual void HandleInvalidSubmit(string message)
        => Toast.AddError(message);

    protected virtual async Task SubmitAsync(CancellationToken cancellationToken = default)
        => await Task.CompletedTask;

    protected virtual void OnBeforeSubmit() { }

    protected virtual void OnAfterSubmit(bool success) { }

    protected virtual async Task AutoSaveAsync(
            string fieldName,
            object? oldValue,
            object? newValue,
            CancellationToken cancellationToken = default
        )
            => await Task.CompletedTask;

    protected virtual async Task OnModelChanged(
            string fieldName,
            object? oldValue,
            object? newValue,
            CancellationToken cancellationToken = default
        ) 
            => await Task.CompletedTask;

    protected virtual bool IsFieldAutoSaveEnabled(string fieldName)
    {
        // Fast path: source-generated metadata (AOT-safe, zero reflection).
        if (ViewModelMetadata<TViewModel>.Instance is { } metadata
            && metadata.Properties.TryGetValue(fieldName, out var accessor))
        {
            return accessor.AutoSave is not null;
        }

        // Fallback: reflection (backward compatibility when generator isn't hooked up).
        return IsFieldAutoSaveEnabledReflective(fieldName);
    }

    // Bez supresji i bez potrzeby jej: typeof(TViewModel) niesie DAM z parametru typu klasy,
    // więc wzorzec GetProperty jest dla analizatora dowiedziony. Ani Type.GetProperty, ani
    // MemberInfo.GetCustomAttribute<T>() nie są oznaczone [RequiresUnreferencedCode] /
    // [RequiresDynamicCode] (sprawdzone w metadanych Microsoft.NETCore.App.Ref 10.0.9), więc
    // IL2026/IL3050 nigdy tu nie padają — supresja tłumiłaby diagnostykę, która nie istnieje.
    private static bool IsFieldAutoSaveEnabledReflective(string fieldName)
    {
        var property = typeof(TViewModel).GetProperty(fieldName);
        return property?.GetCustomAttribute<AutoSaveAttribute>() != null;
    }

    public async Task HandleValidSubmit(EditContext editContext)
    {
        if (editContext.Validate() is false)
            return;
        

        if (PreventDuplicateSubmissions && IsDuplicateSubmission())
            return;

        Processing = true;
        var success = false;
        var cancellationToken = ComponentToken;

        try
        {
            OnBeforeSubmit();

            // Wyczyść i znormalizuj dane przed wysłaniem
            if (AutoTrimStrings || AutoNormalizeWhitespace)
            {
                CleanModelData();
            }

            await SubmitAsync(cancellationToken);
            success = true;

            if (TrackChanges)
                HasChanges = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            success = false;
            throw;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Processing = false;
                OnAfterSubmit(success);

                if (PreventDuplicateSubmissions)
                    _lastSubmitTime = DateTime.Now;
            }
        }
    }

    public void HandleInvalidSubmit()
    {
        if (EditContext is null)
            return;

        var messages = EditContext.GetValidationMessages();

        foreach (var error in messages)
        {
            if (error is null)
                continue;

            var message = Culture.Translate(error);
            HandleInvalidSubmit(message);
        }
    }

    public void ResetModel()
    {
        Model = new TViewModel();

        if (EditContext is not null)
        {
            EditContext.OnFieldChanged -= HandleModelFieldChanged;
            EditContext.OnValidationRequested -= HandleValidationRequested;
        }

        EditContext = new EditContext(Model);
        EditContext.OnFieldChanged += HandleModelFieldChanged;
        EditContext.OnValidationRequested += HandleValidationRequested;
        ValidationMessages = new ValidationMessageStore(EditContext);

        if (TrackChanges)
            HasChanges = false;
    }

    public void AddValidationMessage(string fieldName, string message)
    {
        if (EditContext is null)
            return;

        var field = EditContext.Field(fieldName);
        ValidationMessages?.Add(field, message);
        EditContext.NotifyValidationStateChanged();
        StateHasChanged();
    }

    public void ClearValidationMessages()
    {
        ValidationMessages?.Clear();
        EditContext?.NotifyValidationStateChanged();
    }

    public void MarkAsChanged()
    {
        if (TrackChanges)
            HasChanges = true;
    }

    public void MarkAsUnchanged()
    {
        if (TrackChanges)
            HasChanges = false;
    }

    private bool IsDuplicateSubmission()
    {
        var isDuplicate = _lastSubmitTime.HasValue &&
               DateTime.Now - _lastSubmitTime.Value < _duplicateSubmissionThreshold;

        return isDuplicate;
    }

    private void CleanModelData()
    {
        // Fast path: source-generated string accessors (AOT-safe).
        if (ViewModelMetadata<TViewModel>.Instance is { } metadata)
        {
            foreach (var accessor in metadata.StringProperties)
            {
                var value = accessor.Get(Model);
                if (value is null)
                    continue;

                var cleanedValue = value;

                if (AutoTrimStrings)
                    cleanedValue = cleanedValue.Trim();

                if (AutoNormalizeWhitespace)
                    cleanedValue = TextNormalizer.Whitespace(cleanedValue);

                if (cleanedValue != value)
                    accessor.Set(Model, cleanedValue);
            }
            return;
        }

        CleanModelDataReflective();
    }

    // Patrz komentarz przy IsFieldAutoSaveEnabledReflective — GetProperties(Public|Instance)
    // wymaga DAM(PublicProperties), które typeof(TViewModel) już ma; żadna z użytych metod
    // nie jest RUC/RDC, więc nie ma czego tłumić.
    private void CleanModelDataReflective()
    {
        var properties = typeof(TViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(string));

        foreach (var property in properties)
        {
            var value = property.GetValue(Model) as string;
            if (value is null)
                continue;

            var cleanedValue = value;

            if (AutoTrimStrings)
                cleanedValue = cleanedValue.Trim();

            if (AutoNormalizeWhitespace)
                cleanedValue = TextNormalizer.Whitespace(cleanedValue);

            if (cleanedValue != value)
            {
                property.SetValue(Model, cleanedValue);
            }
        }
    }

    private void HandleFieldAutoSave(FieldIdentifier fieldIdentifier)
    {
        var fieldName = fieldIdentifier.FieldName;

        if (!IsFieldAutoSaveEnabled(fieldName))
            return;

        if (_fieldAutoSaveTimers.TryGetValue(fieldName, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        var currentValue = GetFieldValue(fieldIdentifier);
        var previousValue = _lastFieldValues.TryGetValue(fieldName, out var prev) ? prev : null;

        var delay = GetFieldAutoSaveDelay(fieldName);
        var cancellationToken = ComponentToken;

        _fieldAutoSaveTimers[fieldName] = new Timer(async _ =>
        {
            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await AutoSaveAsync(fieldName, previousValue, currentValue, cancellationToken);
                    _lastFieldValues[fieldName] = currentValue;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {

                await HandleFieldAutoSaveError(fieldName, ex, cancellationToken);
            }
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private TimeSpan GetFieldAutoSaveDelay(string fieldName)
    {
        // Fast path: source-generated metadata.
        if (ViewModelMetadata<TViewModel>.Instance is { } metadata
            && metadata.Properties.TryGetValue(fieldName, out var accessor)
            && accessor.AutoSave is { } attr)
        {
            return TimeSpan.FromMilliseconds(attr.DelayMs);
        }

        return GetFieldAutoSaveDelayReflective(fieldName);
    }

    // Jak wyżej: refleksja po typeof(TViewModel), zero diagnostyk do stłumienia.
    private TimeSpan GetFieldAutoSaveDelayReflective(string fieldName)
    {
        var property = typeof(TViewModel).GetProperty(fieldName);
        var autoSaveAttr = property?.GetCustomAttribute<AutoSaveAttribute>();

        if (autoSaveAttr != null)
            return TimeSpan.FromMilliseconds(autoSaveAttr.DelayMs);

        return AutoSaveDelay;
    }

    private object? GetFieldValue(FieldIdentifier fieldIdentifier)
    {
        // Blazor ustawia FieldIdentifier.Model na obiekt, do którego faktycznie przypięto input.
        // Dla płaskiego formularza jest to nasz TViewModel; dla @bind-Value="Model.Child.Name"
        // jest to zagnieżdżony obiekt zupełnie innego typu. Rozdzielenie tych dwóch przypadków
        // jest tu KLUCZOWE, bo tylko pierwszy da się statycznie udowodnić trimmerowi.
        if (fieldIdentifier.Model is TViewModel typed)
            return GetViewModelFieldValue(typed, fieldIdentifier.FieldName);

        return GetNestedModelFieldValue(fieldIdentifier);
    }

    /// <summary>
    /// Odczyt pola należącego do <typeparamref name="TViewModel"/>: najpierw wygenerowany
    /// akcesor, potem refleksja po <c>typeof(TViewModel)</c>.
    /// </summary>
    /// <remarks>
    /// Refleksja jest tu bezpieczna <em>strukturalnie</em>, nie przypadkiem: parametr typu
    /// <typeparamref name="TViewModel"/> klasy niesie
    /// <c>[DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)]</c>,
    /// więc trimmer zachowuje te składowe przy każdej instancjacji <c>PageEditBase&lt;T&gt;</c>,
    /// niezależnie od kształtu tej metody. Dlatego nie ma tu żadnej supresji — analizator sam
    /// dowodzi wzorca.
    /// </remarks>
    private object? GetViewModelFieldValue(TViewModel model, string fieldName)
    {
        try
        {
            // Fast path: source-generated metadata (zero refleksji).
            if (ViewModelMetadata<TViewModel>.Instance is { } metadata
                && metadata.Properties.TryGetValue(fieldName, out var accessor))
            {
                return accessor.Get(model);
            }

            // Fallback: generator nieobecny albo właściwość dziedziczona (generator nie schodzi
            // po klasie bazowej) — GetProperty przeszukuje całą hierarchię.
            return typeof(TViewModel).GetProperty(fieldName)?.GetValue(model);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas pobierania wartości pola {FieldName} w {ComponentType}",
                fieldName, GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Odczyt pola, którego modelem <b>nie</b> jest <typeparamref name="TViewModel"/> — czyli
    /// pola przypiętego do zagnieżdżonego obiektu (<c>@bind-Value="Model.Child.Name"</c>).
    /// Bazowa implementacja zwraca <see langword="null"/> i loguje ostrzeżenie.
    /// </summary>
    /// <remarks>
    /// <para><b>Dlaczego nie refleksja.</b> Poprzednia wersja robiła
    /// <c>fieldIdentifier.Model.GetType().GetProperty(...)</c> pod supresją IL2075
    /// uzasadnioną słowami „Model jest formularzowym TViewModel”. To zdanie jest fałszywe
    /// dokładnie wtedy, gdy ta ścieżka się wykonuje — trafia się tu wyłącznie wtedy, gdy test
    /// <c>Model is TViewModel</c> zawiódł. <see cref="FieldIdentifier.Model"/> nie ma żadnej
    /// adnotacji <c>[DynamicallyAccessedMembers]</c> (sprawdzone w metadanych
    /// Microsoft.AspNetCore.Components.Forms 10.0.9), więc nic nie gwarantuje, że składowe tego
    /// typu przeżyły trimming. Zamiast zostawiać nieprawdziwe uzasadnienie, framework przestaje
    /// zgadywać i mówi to głośno.</para>
    /// <para><b>Jak to włączyć z powrotem.</b> Nadpisz tę metodę w swojej stronie i zwróć
    /// wartość jawnie — najlepiej bez refleksji, np. <c>switch</c> po
    /// <c>fieldIdentifier.FieldName</c>. Nadpisanie wyłącza też ostrzeżenie.</para>
    /// </remarks>
    /// <param name="fieldIdentifier">Pole zgłoszone przez <see cref="EditContext"/>.</param>
    /// <returns>Wartość pola albo <see langword="null"/>, gdy nie da się jej ustalić bezpiecznie.</returns>
    protected virtual object? GetNestedModelFieldValue(FieldIdentifier fieldIdentifier)
    {
        var modelType = fieldIdentifier.Model.GetType();

        // Deduplikacja per (typ modelu, pole): pojedyncze pole zmienia się przy każdym
        // naciśnięciu klawisza, a ostrzeżenie niesie informację tylko za pierwszym razem.
        if (_reportedNestedFields.Add($"{modelType.FullName}.{fieldIdentifier.FieldName}"))
        {
            Logger.LogWarning(
                "Pole {FieldName} należy do modelu {ModelType}, a nie do {ViewModelType} formularza. " +
                "Wartość nie została odczytana: refleksja po nieznanym typie nie jest bezpieczna dla trimmera " +
                "(FieldIdentifier.Model nie ma [DynamicallyAccessedMembers]). OnModelChanged i AutoSave dostaną " +
                "null. Nadpisz GetNestedModelFieldValue w {ComponentType}, aby podać wartość jawnie.",
                fieldIdentifier.FieldName, modelType.Name, typeof(TViewModel).Name, GetType().Name);
        }

        return null;
    }

    protected virtual async Task HandleFieldAutoSaveError(
        string fieldName,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        if (Model is null || EditContext is null)
            return;

        ValidationMessages?.Clear();

        var validationResults = new List<ValidationResult>();
        var validationContext = CreateValidationContext(Model);

        bool isValid = TryValidate(Model, validationContext, validationResults);

        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    var field = EditContext.Field(memberName);
                    var message = Culture.Translate(validationResult.ErrorMessage!);
                    ValidationMessages?.Add(field, message);
                    Logger.LogWarning("Błąd walidacji dla pola {FieldName} w {ComponentType}: {Message}",
                        memberName, GetType().Name, message);
                }
            }
        }

        EditContext.NotifyValidationStateChanged();
    }

    /// <summary>
    /// Tworzy EventCallback dla właściwości modelu, pozwalając na składnię OnValueChanged(Model.Property)
    /// </summary>
    /// <typeparam name="T">Typ wartości właściwości</typeparam>
    /// <param name="modelValue">Wartość właściwości modelu (np. Model.Property)</param>
    /// <returns>EventCallback do przypisania do ValueChanged</returns>
    protected EventCallback<T> OnValueChanged<T>(T modelValue)
    {
        return EventCallback.Factory.Create<T>(this, newValue =>
        {
            if (Model is null)
                return;

            // Fast path: source-generated metadata.
            if (ViewModelMetadata<TViewModel>.Instance is { } metadata)
            {
                foreach (var accessor in metadata.Properties.Values)
                {
                    if (accessor.PropertyType != typeof(T))
                        continue;

                    var currentValue = accessor.Get(Model);
                    if (EqualityComparer<T>.Default.Equals((T)currentValue!, modelValue))
                    {
                        accessor.Set(Model, newValue);
                        EditContext?.NotifyFieldChanged(EditContext.Field(accessor.Name));
                        break;
                    }
                }
                return;
            }

            OnValueChangedReflective(modelValue, newValue);
        });
    }

    // Jak pozostałe *Reflective: refleksja wyłącznie po typeof(TViewModel), zero RUC/RDC.
    private void OnValueChangedReflective<T>(T modelValue, T newValue)
    {
        // Znajdź właściwość w modelu, która odpowiada tej wartości
        var properties = typeof(TViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(T));

        foreach (var property in properties)
        {
            var currentValue = property.GetValue(Model);

            // Sprawdź czy wartości są równe - to jest właściwość, którą chcemy zaktualizować
            if (EqualityComparer<T>.Default.Equals((T)currentValue!, modelValue))
            {
                // Aktualizuj wartość właściwości
                property.SetValue(Model, newValue);

                // Powiadom EditContext o zmianie
                if (EditContext != null)
                {
                    EditContext.NotifyFieldChanged(EditContext.Field(property.Name));
                }

                // Znaleźliśmy pasującą właściwość, nie musimy sprawdzać dalej
                break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (EditContext is not null)
            {
                EditContext.OnFieldChanged -= HandleModelFieldChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
                ValidationMessages?.Clear();
                ValidationMessages = null;
            }

            foreach (var timer in _fieldAutoSaveTimers.Values)
            {
                timer.Dispose();
            }
            _fieldAutoSaveTimers.Clear();
            _lastFieldValues.Clear();
        }

        base.Dispose(disposing);
    }

    // Oba parametry są typu TViewModel, a nie object, i to jest istota poprawki: mechanizmem,
    // który utrzymuje te ścieżki przy życiu po trimmingu, jest DAM na PARAMETRZE TYPU klasy
    // (PublicProperties | PublicFields | PublicConstructors). Trimmer stosuje tę adnotację przy
    // każdej instancjacji PageEditBase<T>, więc jest niezależna od kształtu tych metod — ale
    // tylko dopóki typ przechodzi przez granicę jako TViewModel. Gdy sygnatura brzmiała
    // `object instance`, związek trzymał się wyłącznie na tym, że sąsiednia metoda przypadkiem
    // brała TViewModel; refaktor mógł go zerwać bez błędu kompilacji, a Validator po cichu
    // zwracał `valid = true` dla modelu łamiącego [MinLength]. Teraz zerwanie tego związku to
    // błąd kompilacji.
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "new ValidationContext(object) is [RequiresUnreferencedCode]: it reflects over instance.GetType() to resolve DisplayName. Mitigation: the runtime type is always TViewModel, whose PublicProperties|PublicFields|PublicConstructors the trimmer preserves because the class's TViewModel type parameter carries [DynamicallyAccessedMembers] — an annotation applied at every PageEditBase<T> instantiation, not something this method establishes. The TViewModel-typed parameter makes that link a compile-time requirement.")]
    private static ValidationContext CreateValidationContext(TViewModel instance)
        => new(instance);

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Validator.TryValidateObject is [RequiresUnreferencedCode]: it reflects over the instance's properties and their ValidationAttributes. Mitigation and its limits: (1) the instance is statically TViewModel, whose members the trimmer preserves via [DynamicallyAccessedMembers] on the class's type parameter; (2) built-in ValidationAttribute types are rooted by the framework. NOT covered — a custom ValidationAttribute reachable only through the attribute blob, and members of nested complex types (the annotation does not recurse). Root those yourself when publishing trimmed.")]
    private static bool TryValidate(
        TViewModel instance,
        ValidationContext validationContext,
        ICollection<ValidationResult> validationResults)
        => Validator.TryValidateObject(instance, validationContext, validationResults, validateAllProperties: true);
}