using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Zonit.Extensions.Website;
public abstract class PageEditBase<TViewModel> : PageBase where TViewModel : class, new()
{
    [SupplyParameterFromForm]
    protected TViewModel Model { get; private set; } = new TViewModel();
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
        Logger.LogDebug("Inicjalizacja PageEditBase<{ViewModelType}> dla {ComponentType}",
            typeof(TViewModel).Name, GetType().Name);

        InitializeEditContext();
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        Logger.LogDebug("Parametry ustawione, aktualizacja EditContext dla {ComponentType}", GetType().Name);
        InitializeEditContext();
        base.OnParametersSet();
    }

    private void InitializeEditContext()
    {
        Model ??= new TViewModel();

        if (EditContext == null || !ReferenceEquals(EditContext.Model, Model))
        {
            Logger.LogDebug("Inicjalizacja nowego EditContext dla {ComponentType}", GetType().Name);

            // Jeśli istnieje poprzedni EditContext, odsubskrybuj zdarzenia
            if (EditContext is not null)
            {
                Logger.LogTrace("Odsubskrypcja zdarzeń poprzedniego EditContext dla {ComponentType}", GetType().Name);
                EditContext.OnFieldChanged -= OnModelChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
            }

            EditContext = new EditContext(Model);
            EditContext.OnFieldChanged += OnModelChanged;
            EditContext.OnValidationRequested += HandleValidationRequested;
            ValidationMessages = new ValidationMessageStore(EditContext);
            Logger.LogDebug("Utworzono nowy EditContext i ValidationMessageStore dla {ComponentType}", GetType().Name);

            if (TrackChanges)
                HasChanges = false;
        }
    }

    protected virtual void OnModelChanged(object? sender, FieldChangedEventArgs e)
    {
        Logger.LogDebug("Zmiana pola {FieldName} dla {ComponentType}",
            e.FieldIdentifier.FieldName, GetType().Name);

        if (TrackChanges)
            HasChanges = true;

        // Auto-save dla konkretnego pola
        HandleFieldAutoSave(e.FieldIdentifier);

        StateHasChanged();
    }

    protected virtual void HandleInvalidSubmit(string message)
    {
        Logger.LogWarning("Nieprawidłowa submisja dla {ComponentType}: {Message}",
            GetType().Name, message);
    }

    protected virtual async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Domyślna implementacja SubmitAsync dla {ComponentType}", GetType().Name);
        await Task.CompletedTask;
    }

    protected virtual void OnBeforeSubmit()
    {
        Logger.LogDebug("Przygotowanie do submisji dla {ComponentType}", GetType().Name);
    }

    protected virtual void OnAfterSubmit(bool success)
    {
        Logger.LogDebug("Zakończenie submisji dla {ComponentType} - Status: {Success}",
            GetType().Name, success ? "Sukces" : "Błąd");
    }

    // Auto-save hooks - override w klasie pochodnej
    protected virtual async Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Domyślna implementacja AutoSaveAsync dla pola {FieldName} w {ComponentType}",
            fieldName, GetType().Name);
        await Task.CompletedTask;
    }

    protected virtual bool IsFieldAutoSaveEnabled(string fieldName)
    {
        // Sprawdź czy pole ma atrybut [AutoSave]
        var property = typeof(TViewModel).GetProperty(fieldName);
        var isEnabled = property?.GetCustomAttribute<AutoSaveAttribute>() != null;

        Logger.LogTrace("Sprawdzenie auto-save dla pola {FieldName} w {ComponentType}: {IsEnabled}",
            fieldName, GetType().Name, isEnabled);

        return isEnabled;
    }

    public async Task HandleValidSubmit(EditContext editContext)
    {
        Logger.LogDebug("Obsługa prawidłowej submisji dla {ComponentType}", GetType().Name);

        if (editContext.Validate() is false)
        {
            Logger.LogWarning("Walidacja nie powiodła się dla {ComponentType}", GetType().Name);
            return;
        }

        // Sprawdź czy nie jest to duplikat submissji
        if (PreventDuplicateSubmissions && IsDuplicateSubmission())
        {
            Logger.LogWarning("Wykryto duplikat submisji dla {ComponentType}", GetType().Name);
            return;
        }

        Processing = true;
        var success = false;
        var cancellationToken = CancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            Logger.LogInformation("Rozpoczęcie przetwarzania formularza dla {ComponentType}", GetType().Name);
            OnBeforeSubmit();

            // Wyczyść i znormalizuj dane przed wysłaniem
            if (AutoTrimStrings || AutoNormalizeWhitespace)
            {
                Logger.LogDebug("Czyszczenie danych modelu dla {ComponentType}", GetType().Name);
                CleanModelData();
            }

            await SubmitAsync(cancellationToken);
            success = true;
            Logger.LogInformation("Formularz pomyślnie przetworzony dla {ComponentType}", GetType().Name);

            if (TrackChanges)
                HasChanges = false;
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Submisja anulowana dla {ComponentType}", GetType().Name);
        }
        catch (Exception ex)
        {
            success = false;
            Logger.LogError(ex, "Błąd podczas przetwarzania formularza dla {ComponentType}: {Message}",
                GetType().Name, ex.Message);
            throw;
        }
        finally
        {
            // Jeśli nie było anulowania, aktualizuj stan
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
        Logger.LogWarning("Obsługa nieprawidłowej submisji dla {ComponentType}", GetType().Name);

        if (EditContext is null)
            return;

        var messages = EditContext.GetValidationMessages();
        Logger.LogDebug("Liczba komunikatów walidacyjnych: {Count} dla {ComponentType}",
            messages.Count(), GetType().Name);

        foreach (var error in messages)
        {
            if (error is null)
                continue;

            var message = Culture.Translate(error);
            Logger.LogWarning("Błąd walidacji dla {ComponentType}: {Message}", GetType().Name, message);
            HandleInvalidSubmit(message);
        }
    }

    public void ResetModel()
    {
        Logger.LogInformation("Resetowanie modelu dla {ComponentType}", GetType().Name);

        if (EditContext is not null)
        {
            EditContext.OnFieldChanged -= OnModelChanged;
            EditContext.OnValidationRequested -= HandleValidationRequested;
        }

        Model = new TViewModel();
        EditContext = new EditContext(Model);
        EditContext.OnFieldChanged += OnModelChanged;
        EditContext.OnValidationRequested += HandleValidationRequested;
        ValidationMessages = new ValidationMessageStore(EditContext);

        if (TrackChanges)
            HasChanges = false;

        Logger.LogDebug("Model i EditContext zresetowane dla {ComponentType}", GetType().Name);
    }

    public void AddValidationMessage(string fieldName, string message)
    {
        Logger.LogDebug("Dodawanie komunikatu walidacyjnego dla pola {FieldName} w {ComponentType}: {Message}",
            fieldName, GetType().Name, message);

        if (EditContext is null)
            return;

        var field = EditContext.Field(fieldName);
        ValidationMessages?.Add(field, message);
    }

    public void ClearValidationMessages()
    {
        Logger.LogDebug("Czyszczenie wszystkich komunikatów walidacyjnych dla {ComponentType}", GetType().Name);
        ValidationMessages?.Clear();
        EditContext?.NotifyValidationStateChanged();
    }

    public void MarkAsChanged()
    {
        if (TrackChanges)
        {
            Logger.LogDebug("Oznaczenie jako zmienione dla {ComponentType}", GetType().Name);
            HasChanges = true;
        }
    }

    public void MarkAsUnchanged()
    {
        if (TrackChanges)
        {
            Logger.LogDebug("Oznaczenie jako niezmienione dla {ComponentType}", GetType().Name);
            HasChanges = false;
        }
    }

    private bool IsDuplicateSubmission()
    {
        var isDuplicate = _lastSubmitTime.HasValue &&
               DateTime.Now - _lastSubmitTime.Value < _duplicateSubmissionThreshold;

        if (isDuplicate)
        {
            Logger.LogWarning("Wykryto duplikat submisji dla {ComponentType} - ostatnia: {LastSubmitTime}",
                GetType().Name, _lastSubmitTime);
        }

        return isDuplicate;
    }

    private void CleanModelData()
    {
        if (Model is null)
            return;

        Logger.LogDebug("Czyszczenie danych modelu dla {ComponentType}", GetType().Name);
        var changedCount = 0;

        var properties = typeof(TViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
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
                cleanedValue = NormalizeWhitespace(cleanedValue);

            // Ustaw z powrotem tylko jeśli wartość się zmieniła
            if (cleanedValue != value)
            {
                property.SetValue(Model, cleanedValue);
                changedCount++;
            }
        }

        Logger.LogDebug("Zakończono czyszczenie modelu dla {ComponentType} - zmieniono {Count} wartości",
            GetType().Name, changedCount);
    }

    private static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Zamień wielokrotne spacje na pojedynczą
        return System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ");
    }

    private void HandleFieldAutoSave(FieldIdentifier fieldIdentifier)
    {
        var fieldName = fieldIdentifier.FieldName;

        if (!IsFieldAutoSaveEnabled(fieldName))
            return;

        Logger.LogDebug("Auto-save dla pola {FieldName} w {ComponentType}", fieldName, GetType().Name);

        // Anuluj poprzedni timer dla tego pola
        if (_fieldAutoSaveTimers.TryGetValue(fieldName, out var existingTimer))
        {
            existingTimer.Dispose();
            Logger.LogTrace("Anulowano poprzedni timer auto-save dla pola {FieldName} w {ComponentType}",
                fieldName, GetType().Name);
        }

        // Pobierz aktualną wartość pola
        var currentValue = GetFieldValue(fieldIdentifier);
        var previousValue = _lastFieldValues.TryGetValue(fieldName, out var prev) ? prev : null;

        // Pobierz delay z atrybutu lub użyj domyślnego
        var delay = GetFieldAutoSaveDelay(fieldName);
        Logger.LogTrace("Ustawiono opóźnienie auto-save {Delay}ms dla pola {FieldName} w {ComponentType}",
            delay.TotalMilliseconds, fieldName, GetType().Name);

        // Zapisz referencję do bieżącego tokenu anulowania
        var cancellationToken = CancellationTokenSource?.Token ?? CancellationToken.None;

        // Ustaw nowy timer
        _fieldAutoSaveTimers[fieldName] = new Timer(async _ =>
        {
            try
            {
                // Sprawdź czy token jest wciąż aktywny
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogDebug("Wykonywanie auto-save dla pola {FieldName} w {ComponentType}",
                        fieldName, GetType().Name);
                    await AutoSaveAsync(fieldName, previousValue, currentValue, cancellationToken);
                    _lastFieldValues[fieldName] = currentValue;
                    Logger.LogDebug("Zakończono auto-save dla pola {FieldName} w {ComponentType}",
                        fieldName, GetType().Name);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Auto-save anulowany dla pola {FieldName} w {ComponentType}",
                    fieldName, GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Błąd podczas auto-save dla pola {FieldName} w {ComponentType}: {Message}",
                    fieldName, GetType().Name, ex.Message);

                // Wykorzystaj token anulowania również przy błędach
                await HandleFieldAutoSaveError(fieldName, ex, cancellationToken);
            }
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private TimeSpan GetFieldAutoSaveDelay(string fieldName)
    {
        var property = typeof(TViewModel).GetProperty(fieldName);
        var autoSaveAttr = property?.GetCustomAttribute<AutoSaveAttribute>();

        if (autoSaveAttr != null)
            return TimeSpan.FromMilliseconds(autoSaveAttr.DelayMs);

        return AutoSaveDelay; // Fallback do domyślnego
    }

    private object? GetFieldValue(FieldIdentifier fieldIdentifier)
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

    protected virtual async Task HandleFieldAutoSaveError(string fieldName, Exception exception, CancellationToken cancellationToken = default)
    {
        Logger.LogError(exception, "Błąd auto-save dla pola {FieldName} w {ComponentType}: {Message}",
            fieldName, GetType().Name, exception.Message);
        await Task.CompletedTask;
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        Logger.LogDebug("Walidacja żądana dla {ComponentType}", GetType().Name);

        if (Model is null || EditContext is null)
            return;

        ValidationMessages?.Clear();

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(Model);

        bool isValid = Validator.TryValidateObject(Model, validationContext, validationResults, true);
        Logger.LogDebug("Wynik walidacji dla {ComponentType}: {IsValid}", GetType().Name, isValid ? "Poprawny" : "Niepoprawny");

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.LogDebug("Zwalnianie zasobów PageEditBase dla {ComponentType}", GetType().Name);

            if (EditContext is not null)
            {
                Logger.LogTrace("Usuwanie subskrypcji EditContext dla {ComponentType}", GetType().Name);
                EditContext.OnFieldChanged -= OnModelChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
                ValidationMessages?.Clear();
                ValidationMessages = null;
            }

            // Wyczyść timery auto-save
            Logger.LogTrace("Usuwanie timerów auto-save dla {ComponentType}", GetType().Name);
            foreach (var timer in _fieldAutoSaveTimers.Values)
            {
                timer.Dispose();
            }
            _fieldAutoSaveTimers.Clear();
            _lastFieldValues.Clear();
        }

        base.Dispose(disposing);
    }
}