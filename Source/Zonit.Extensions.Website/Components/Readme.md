# Zonit.Extensions.Website – Base Classes for Blazor Components

This repository provides a Blazor-focused set of base classes that help manage:

• Data loading and persistence (PageViewBase<T>).  
• Form editing, validation, and auto-saving (PageEditBase<TViewModel>).  
• Common behaviors and services for user interfaces, breadcrumbs, culture providers, etc. (ExtensionsBase).  

These classes are intended to streamline writing Blazor components by minimizing boilerplate code and enforcing best practices for data binding, state persistence, and user interactions.

--------------------------------------------------------------------------------

## Table of Contents

1. [Overview](#overview)  
2. [PageViewBase<T>](#pageviewbaset)  
    - [Key Features](#key-features)  
    - [Usage Example](#usage-example)  
3. [PageEditBase<TViewModel>](#pageeditbasetviewmodel)  
    - [Key Features](#key-features-1)  
    - [Usage Example](#usage-example-1)  
4. [ExtensionsBase](#extensionsbase)  
    - [Key Features](#key-features-2)  
    - [Usage Example](#usage-example-2)  
5. [Additional Notes](#additional-notes)  

--------------------------------------------------------------------------------

## Overview

This code provides an opinionated infrastructure for building robust Blazor components with:

• Automatic state persistence using Microsoft.AspNetCore.Components.PersistentComponentState.  
• A consistent approach to data handling, loading, and refreshing.  
• Simplified form editing with validation, change-tracking, and auto-save functionality.  
• Easy integration with dependency injection for services like culture providers, workspace providers, and more.  
• Breadcrumb management for improved site navigation.

--------------------------------------------------------------------------------

## PageViewBase<T>

Generic base class for components that need to load and display data of type T. It automatically persists and restores state, handles loading flags, and supports data refreshes.

### Key Features

• Generic Model property (T? Model) for reading data.  
• Automatic state persistence (PersistentComponentState).  
• IsLoading flag set while data is being fetched.  
• Async loading logic via LoadAsync().  
• RefreshAsync() method for manual data refresh.  
• OnRefreshChangeAsync() override for reacting to external refresh events.  

### Usage Example

Suppose you have a data model called “Product” that you want to display:

```csharp
@code {
    // Example of a model
    public class Product
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class ProductPage : PageViewBase<Product>
    {
        protected override async Task<Product?> LoadAsync()
        {
            // Simulate data loading
            await Task.Delay(500);

            // For demonstration, returning a new product
            return new Product { Id = 1, Name = "Sample Product" };
        }

        // Optionally override if needed
        protected override async void OnRefreshChangeAsync()
        {
            // First call base logic for reloading
            await RefreshAsync();

            // Then handle any additional logic
            Console.WriteLine("Data refreshed from external event!");
        }
    }
}
```

Key points in the above example:

• Model is defined by the generic parameter Product.  
• LoadAsync() is overridden to fetch data (your custom logic here).  
• The component automatically shows a loading indicator if you choose to reference IsLoading in the Razor markup.  
• State is automatically persisted and restored the next time the component is visited (if you configure Blazor’s Prerendering and PersistentComponentState properly).  

--------------------------------------------------------------------------------

## PageEditBase<TViewModel>

A base class for building Blazor forms and editing data. It manages validation, auto-saving, change tracking, and duplication prevention.

### Key Features

• EditContext for validation and form handling.  
• A TViewModel instance as the data context (Model).  
• ValidationMessageStore for dynamic error display.  
• AutoTrimStrings and AutoNormalizeWhitespace options to sanitize user input.  
• TrackChanges to detect if the user has unsaved changes.  
• Auto-save support for fields marked with a custom [AutoSave] attribute.  
• HandleValidSubmit and HandleInvalidSubmit overrides for improved form submission control.  

### Usage Example

Imagine you have a simple view model for editing user data:

```csharp
@code {
    public class UserViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
        public string Email { get; set; } = string.Empty;
    }

    public class UserEditPage : PageEditBase<UserViewModel>
    {
        protected override async Task SubmitAsync()
        {
            // This method is called when the form is valid and user clicks "Submit"
            // Implement your save logic, e.g., call an API to store the user data
            await Task.Delay(500);
            Console.WriteLine("User data has been saved!");
        }

        protected override void OnAfterSubmit(bool success)
        {
            if (success)
            {
                // e.g. Show a success message
                Console.WriteLine("Submission successful!");
            }
        }

        protected override bool IsFieldAutoSaveEnabled(string fieldName)
        {
            // Optionally decide at runtime if a field should be autosaved
            // For example, auto-save just the Email field
            return fieldName == nameof(UserViewModel.Email);
        }
    }
}
```

In your Razor file, you’d also have a form:

```razor
@page "/user-edit"

<EditForm EditContext="@EditContext" OnValidSubmit="@HandleValidSubmit" OnInvalidSubmit="@HandleInvalidSubmit">
    <div>
        <label>Username</label>
        <InputText @bind-Value="Model.Username" />
        <ValidationMessage For="@(() => Model.Username)" />
    </div>

    <div>
        <label>Email</label>
        <InputText @bind-Value="Model.Email" />
        <ValidationMessage For="@(() => Model.Email)" />
    </div>

    <button type="submit" disabled="@Processing">Save</button>
</EditForm>

@if (HasChanges)
{
    <p>You have unsaved changes.</p>
}
```

Key points to notice:  
• Model is created automatically (new()).  
• On form submission (OnValidSubmit), the component runs SubmitAsync().  
• Auto field trimming/whitespace normalization is configurable.  
• The user can see validation messages from ValidationMessageStore.  

--------------------------------------------------------------------------------

## ExtensionsBase

Common base class that provides services and functionalities used by both PageViewBase<T> and PageEditBase<TViewModel>. Key tasks include managing breadcrumbs, injecting common dependencies (like culture providers), and reacting to service changes.

### Key Features

• ShowBreadcrumbs property for controlling whether breadcrumbs should be displayed, not set, or cleared.  
• Lazy-loaded providers: ICultureProvider, IWorkspaceProvider, ICatalogProvider, etc.  
• OnRefreshChangeAsync() as a virtual method to reload data or refresh UI on external changes.  
• Automatic disposal of event handlers to avoid memory leaks.  

### Usage Example

You typically won’t inherit directly from ExtensionsBase unless you’re creating a specialized base class:

```cs
public class MyCustomBase : ExtensionsBase
{
    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Here you could override or extend the logic from ExtensionsBase
    }

    protected override async void OnRefreshChangeAsync()
    {
        // Do special refresh logic
        await base.OnInitializedAsync();

        // Then forcibly refresh UI (if needed)
        StateHasChanged();
    }
}
```

--------------------------------------------------------------------------------

## Additional Notes

1. Make sure breadcrumbs, culture providers, and other dependencies are properly registered in the service container.  
2. If you have Prerendering enabled on Blazor Server, the PersistentComponentState requires you to configure the system properly in your _Host.cshtml or equivalent to preserve and restore state.  
3. For auto-save field customizations, you can annotate your TViewModel properties with a custom [AutoSave] attribute (not shown in the code snippet above, but you can easily create one).  
4. Adjust the _duplicateSubmissionThreshold and AutoSaveDelay according to your preferences.  

--------------------------------------------------------------------------------

This codebase is designed to make building data-centric or form-heavy Blazor pages faster and more maintainable. Feel free to extend or modify it to suit your own application requirements and development patterns. If you encounter any issues or have suggestions, please open an issue or submit a pull request. Enjoy coding!
