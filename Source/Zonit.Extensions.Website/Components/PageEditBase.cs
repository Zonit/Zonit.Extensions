using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
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
            // Jeśli istnieje poprzedni EditContext, odsubskrybuj zdarzenia
            if (EditContext is not null)
            {
                EditContext.OnFieldChanged -= OnModelChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
            }

            EditContext = new EditContext(Model);
            EditContext.OnFieldChanged += OnModelChanged;
            EditContext.OnValidationRequested += HandleValidationRequested;
            ValidationMessages = new ValidationMessageStore(EditContext);

            if (TrackChanges)
                HasChanges = false;
        }
    }

    protected virtual void OnModelChanged(object? sender, FieldChangedEventArgs e)
    {
        if (TrackChanges)
            HasChanges = true;

        // Auto-save dla konkretnego pola
        HandleFieldAutoSave(e.FieldIdentifier);

        StateHasChanged();
    }

    protected virtual void HandleInvalidSubmit(string message) { }
    protected virtual async Task SubmitAsync() { await Task.CompletedTask; }
    protected virtual void OnBeforeSubmit() { }
    protected virtual void OnAfterSubmit(bool success) { }

    // Auto-save hooks - override w klasie pochodnej
    protected virtual async Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue)
    {
        await Task.CompletedTask;
    }

    protected virtual bool IsFieldAutoSaveEnabled(string fieldName)
    {
        // Sprawdź czy pole ma atrybut [AutoSave]
        var property = typeof(TViewModel).GetProperty(fieldName);
        return property?.GetCustomAttribute<AutoSaveAttribute>() != null;
    }

    public async Task HandleValidSubmit(EditContext editContext)
    {
        if (editContext.Validate() is false)
            return;

        // Sprawdź czy nie jest to duplikat submissji
        if (PreventDuplicateSubmissions && IsDuplicateSubmission())
            return;

        Processing = true;
        var success = false;

        try
        {
            OnBeforeSubmit();

            // Wyczyść i znormalizuj dane przed wysłaniem
            if (AutoTrimStrings || AutoNormalizeWhitespace)
                CleanModelData();

            await SubmitAsync();
            success = true;

            if (TrackChanges)
                HasChanges = false;
        }
        catch (Exception)
        {
            success = false;
            throw;
        }
        finally
        {
            Processing = false;
            OnAfterSubmit(success);

            if (PreventDuplicateSubmissions)
                _lastSubmitTime = DateTime.Now;
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
    }

    public void AddValidationMessage(string fieldName, string message)
    {
        if (EditContext is null)
            return;

        var field = EditContext.Field(fieldName);
        ValidationMessages?.Add(field, message);
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
        return _lastSubmitTime.HasValue &&
               DateTime.Now - _lastSubmitTime.Value < _duplicateSubmissionThreshold;
    }

    private void CleanModelData()
    {
        if (Model is null)
            return;

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
                property.SetValue(Model, cleanedValue);
        }
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

        // Anuluj poprzedni timer dla tego pola
        if (_fieldAutoSaveTimers.TryGetValue(fieldName, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Pobierz aktualną wartość pola
        var currentValue = GetFieldValue(fieldIdentifier);
        var previousValue = _lastFieldValues.TryGetValue(fieldName, out var prev) ? prev : null;

        // Pobierz delay z atrybutu lub użyj domyślnego
        var delay = GetFieldAutoSaveDelay(fieldName);

        // Ustaw nowy timer
        _fieldAutoSaveTimers[fieldName] = new Timer(async _ =>
        {
            try
            {
                await AutoSaveAsync(fieldName, previousValue, currentValue);
                _lastFieldValues[fieldName] = currentValue;
            }
            catch (Exception ex)
            {
                await HandleFieldAutoSaveError(fieldName, ex);
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
        catch
        {
            return null;
        }
    }

    protected virtual async Task HandleFieldAutoSaveError(string fieldName, Exception exception)
    {
        // Override w klasie pochodnej do obsługi błędów auto-save
        await Task.CompletedTask;
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        if (Model is null || EditContext is null)
            return;

        ValidationMessages?.Clear();

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(Model);

        bool isValid = Validator.TryValidateObject(Model, validationContext, validationResults, true);

        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                foreach (var memberName in validationResult.MemberNames)
                {
                    var field = EditContext.Field(memberName);
                    ValidationMessages?.Add(field, Culture.Translate(validationResult.ErrorMessage!));
                }
            }
        }

        EditContext.NotifyValidationStateChanged();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (EditContext is not null)
            {
                EditContext.OnFieldChanged -= OnModelChanged;
                EditContext.OnValidationRequested -= HandleValidationRequested;
                ValidationMessages?.Clear();
                ValidationMessages = null;
            }

            // Wyczyść timery auto-save
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