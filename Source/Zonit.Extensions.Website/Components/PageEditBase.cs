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

        StateHasChanged();
    }

    protected virtual void HandleInvalidSubmit(string message) { }
    protected virtual async Task SubmitAsync() { await Task.CompletedTask; }
    protected virtual void OnBeforeSubmit() { }
    protected virtual void OnAfterSubmit(bool success) { }

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
        if (disposing && EditContext is not null)
        {
            EditContext.OnFieldChanged -= OnModelChanged;
            EditContext.OnValidationRequested -= HandleValidationRequested;
            ValidationMessages?.Clear();
            ValidationMessages = null;
        }

        base.Dispose(disposing);
    }
}