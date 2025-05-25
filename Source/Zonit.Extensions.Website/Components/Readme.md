# PageEditBase<TViewModel> Class - README

## Overview
The `PageEditBase<TViewModel>` is an abstract Blazor component intended to streamline the process of creating forms bound to a ViewModel. It provides:

1. A standardized way to handle form validation, including dynamic validation message storage.  
2. Automatic string trimming and whitespace normalization.  
3. Automatic data-tracking, including a “HasChanges” flag to determine if the form has unsaved changes.  
4. A mechanism to prevent duplicate submissions (e.g., double-clicks).  
5. Field-level auto-saving for properties annotated with a `[AutoSave]` attribute.  

By inheriting from `PageEditBase<TViewModel>`, you can quickly build sophisticated forms that automatically handle many common scenarios. The class is designed to be flexible, so you can override and customize behaviors as needed.

## Key Properties

1. **Model** (TViewModel):  
   - An instance of your ViewModel class (must have a parameterless constructor).  
   - The data-bound instance that the form reads from and writes to.  

2. **EditContext** (EditContext?):  
   - Manages the state of the form, including field-level validation and notifications.  
   - Automatically created for your Model.  

3. **ValidationMessages** (ValidationMessageStore?):  
   - Stores error messages for the form and its fields.  
   - Automatically cleared and re-populated during validation.  

4. **Processing** (bool):  
   - Indicates if a form submission is in progress.  
   - Useful for disabling UI elements while an operation is running.  

5. **HasChanges** (bool):  
   - Indicates if the form data has been modified.  
   - Automatically sets to true on any field change when TrackChanges is enabled.  

6. **IsValid** (bool):  
   - True if no validation errors currently exist; otherwise, false.  

7. **AutoTrimStrings** (bool):  
   - Determines if strings should be automatically trimmed. (Default: true)  

8. **AutoNormalizeWhitespace** (bool):  
   - Determines if whitespace should be normalized to single spaces. (Default: true)  

9. **TrackChanges** (bool):  
   - Indicates if the component should track changes in the form. (Default: true)  

10. **PreventDuplicateSubmissions** (bool):  
    - Prevents submitting the form multiple times in quick succession. (Default: true)  

11. **AutoSaveDelay** (TimeSpan):  
    - Default delay for auto-save timers if a `[AutoSave]` attribute is found on a property.  

## Key Methods

1. **HandleValidSubmit** (EditContext editContext):  
   - Called when the form is valid (after validation passes).  
   - Performs the following:  
     - Cleans and normalizes Model data (if configured).  
     - Invokes `SubmitAsync`.  
     - Updates `HasChanges`.  

2. **HandleInvalidSubmit**():  
   - Called when the form is invalid.  
   - Iterates through validation errors and calls `HandleInvalidSubmit(string message)` for each.  

3. **SubmitAsync**():  
   - Override this method to define the actual form submission logic.  
   - By default, it does nothing.  

4. **OnBeforeSubmit**() and **OnAfterSubmit(bool success)**:  
   - Hook methods called before and after `SubmitAsync`, respectively.  
   - Allows you to perform setup and teardown logic surrounding a submission.  

5. **ResetModel**():  
   - Resets the entire Model with a new instance of TViewModel and creates a new `EditContext`.  
   - Clears all validation messages.  

6. **AddValidationMessage(string fieldName, string message)**:  
   - Programmatically adds a validation message to a specific field.  

7. **ClearValidationMessages**():  
   - Clears all validation messages.  

8. **MarkAsChanged()** and **MarkAsUnchanged()**:  
   - Methods to manually mark the form as having or not having changes.  

9. **AutoSaveAsync(string fieldName, object? oldValue, object? newValue)**:  
   - Override to define custom logic for automatically saving a specific field after a delay.  

10. **IsFieldAutoSaveEnabled(string fieldName)**:  
   - Checks if a property is adorned with `[AutoSave]` and is eligible for auto saving.  

11. **HandleFieldAutoSaveError(string fieldName, Exception exception)**:  
   - Override for custom error handling in case an auto-save operation fails.  

## Basic Usage Example

Below is a minimal example showing how to derive a page from `PageEditBase<TViewModel>`.

### Step 1: Define Your ViewModel

You can apply a `[AutoSave]` attribute on fields that should auto-save after they are changed. For properties you don’t want to auto-save automatically, omit the attribute.

```cs
using System.ComponentModel.DataAnnotations;

public class ExampleViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [AutoSave]
    public string Description { get; set; } = string.Empty;
}
```

### Step 2: Create Your Page

Inherit from `PageEditBase<ExampleViewModel>` and override or customize methods if needed.

```razor
@page "/example-edit"

@inherits PageEditBase<ExampleViewModel>

<h3>Edit Example</h3>

<EditForm Model="Model" OnValidSubmit="HandleValidSubmit" OnInvalidSubmit="HandleInvalidSubmit">
    <ValidationSummary />

    <div>
        <label>Name:</label>
        <InputText @bind-Value="Model.Name" />
        <ValidationMessage For="@(() => Model.Name)" />
    </div>

    <div>
        <label>Description (Auto-Saved):</label>
        <InputText @bind-Value="Model.Description" />
        <ValidationMessage For="@(() => Model.Description)" />
    </div>

    <button type="submit" disabled="@(!IsValid || Processing)">
        @((Processing) ? "Saving..." : "Save")
    </button>
</EditForm>

@code {
    protected override async Task SubmitAsync()
    {
        // Insert your custom save logic here, e.g. database save, API call, etc.
        // This method is invoked only when the form is valid and not a duplicate submission.

        // Example:
        // await SomeService.SaveDataAsync(Model);
    }

    protected override void HandleInvalidSubmit(string message)
    {
        // Here, you could show a custom message or log the validation error.
        Console.WriteLine($"Invalid submit: {message}");
    }
}
```
--------------------------------------------------------------------------------

### Step 3: Customize Behavior
1. Override properties like `AutoTrimStrings` or `TrackChanges` if you want to change default behavior:

```cs
protected override bool AutoTrimStrings => false;
protected override bool TrackChanges => true;
```

2. Implement `AutoSaveAsync` if you want to take a different action when auto-saving fields:

```cs
protected override async Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue)
{
    // Your custom auto-save logic, for instance saving to a local store or calling an API
    Console.WriteLine($"{fieldName} changed from '{oldValue}' to '{newValue}'. Auto-saving...");
    await Task.Delay(100); // Simulate actual save
}
```
--------------------------------------------------------------------------------

## Additional Tips
- If you need to reset the form, call `ResetModel()`. This completely wipes the current `Model` and creates a fresh instance, clearing all validation messages.  
- To prevent UI from allowing repeated submissions while a process is running, you can check `Processing` and disable any button or form control.  
- The `HasChanges` property can help you decide whether to warn the user about unsaved changes when they navigate away.  

## Conclusion
`PageEditBase<TViewModel>` is designed to reduce boilerplate in user-input scenarios by providing robust, extensible features. By inheriting from it, you can focus more on domain logic and less on repeating form-related patterns across pages.  