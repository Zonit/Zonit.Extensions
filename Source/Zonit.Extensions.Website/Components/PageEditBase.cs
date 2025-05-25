using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using System.ComponentModel.DataAnnotations;

namespace Zonit.Extensions.Website;

public abstract class PageEditBase<TViewModel> : PageBase where TViewModel : class, new()
{
    [SupplyParameterFromForm]
    protected TViewModel Model { get; private set; } = new TViewModel();
    protected EditContext? EditContext { get; private set; }
    protected ValidationMessageStore? ValidationMessages { get; private set; }
    protected bool Processing { get; set; } = false;
    public bool IsValid => EditContext?.GetValidationMessages().Any() is false;

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
        }
    }

    protected virtual void OnModelChanged(object? sender, FieldChangedEventArgs e)
        => StateHasChanged();

    protected virtual void HandleInvalidSubmit(string message) { }
    protected virtual async Task SubmitAsync() { await Task.CompletedTask; }

    public async Task HandleValidSubmit(EditContext editContext)
    {
        if (editContext.Validate() is false)
            return;

        Processing = true;

        await SubmitAsync();

        Processing = false;
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
    }

    public void AddValidationMessage(string fieldName, string message)
    {
        if (EditContext is null)
            return;

        var field = EditContext.Field(fieldName);
        ValidationMessages?.Add(field, message);
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
                    ValidationMessages?.Add(field, Culture.Translate(validationResult.ErrorMessage!));      // Translate
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