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
/// być deklarowane na publicznym typie aby trimmer je zatrzymał.</para>
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
        var cancellationToken = CancellationTokenSource?.Token ?? CancellationToken.None;
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "TViewModel is annotated with DAM(PublicProperties|PublicFields|PublicConstructors); fallback path used only when source generator is absent.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Reflection over TViewModel properties; trimmer/AOT keeps the members via DAM annotations on TViewModel.")]
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
        var cancellationToken = CancellationTokenSource?.Token ?? CancellationToken.None;

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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "TViewModel is annotated with DAM(PublicProperties|PublicFields|PublicConstructors); fallback path used only when source generator is absent.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Reflection over TViewModel properties; trimmer/AOT keeps the members via DAM annotations on TViewModel.")]
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
        var cancellationToken = CancellationTokenSource?.Token ?? CancellationToken.None;

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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "TViewModel is annotated with DAM(PublicProperties|PublicFields|PublicConstructors); fallback path used only when source generator is absent.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Reflection over TViewModel properties; trimmer/AOT keeps the members via DAM annotations on TViewModel.")]
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
        // Fast path: if the FieldIdentifier's model is our TViewModel and we have metadata.
        if (fieldIdentifier.Model is TViewModel typed
            && ViewModelMetadata<TViewModel>.Instance is { } metadata
            && metadata.Properties.TryGetValue(fieldIdentifier.FieldName, out var accessor))
        {
            try
            {
                return accessor.Get(typed);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Błąd podczas pobierania wartości pola {FieldName} w {ComponentType}",
                    fieldIdentifier.FieldName, GetType().Name);
                return null;
            }
        }

        return GetFieldValueReflective(fieldIdentifier);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "FieldIdentifier.Model.GetType() may be any type; reflection used to read a property by name. Fallback path used only when source generator is absent.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Reflection-based property read; safe because the runtime model is the trimmer-rooted form-bound TViewModel.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern",
        Justification = "FieldIdentifier.Model is the form-bound TViewModel whose properties are preserved via DAM annotations on the type parameter.")]
    private object? GetFieldValueReflective(FieldIdentifier fieldIdentifier)
    {
        try
        {
            var property = fieldIdentifier.Model.GetType().GetProperty(fieldIdentifier.FieldName);
            return property?.GetValue(fieldIdentifier.Model);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas pobierania wartości pola {FieldName} w {ComponentType}",
                fieldIdentifier.FieldName, GetType().Name);
            return null;
        }
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

        bool isValid = TryValidate(Model!, validationContext, validationResults);

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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "TViewModel is annotated with DAM(PublicProperties|PublicFields|PublicConstructors); fallback path used only when source generator is absent.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Reflection over TViewModel properties; trimmer/AOT keeps the members via DAM annotations on TViewModel.")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "ValidationContext(object) reflects over instance.GetType() to resolve DisplayName. TViewModel members are preserved via DAM annotations; resolved Type is the trimmer-rooted TViewModel.")]
    private static ValidationContext CreateValidationContext(TViewModel instance)
        => new ValidationContext(instance);

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Validator.TryValidateObject reflects over Model's properties and validation attributes. TViewModel's public members are preserved via DAM; built-in ValidationAttribute types are rooted by the framework. Custom ValidationAttributes must be public to be kept by the trimmer.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Validator.TryValidateObject uses reflection; AOT safe because validation attributes are referenced statically and trimmer keeps TViewModel members.")]
    private static bool TryValidate(
        object instance,
        ValidationContext validationContext,
        ICollection<ValidationResult> validationResults)
        => Validator.TryValidateObject(instance, validationContext, validationResults, validateAllProperties: true);
}