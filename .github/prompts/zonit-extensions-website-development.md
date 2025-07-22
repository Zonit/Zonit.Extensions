# Zonit.Extensions.Website Development Prompt

## Overview

This prompt provides comprehensive guidelines for developing pages and components using the Zonit.Extensions.Website library. The library follows a structured approach with base classes, providers, and standardized patterns for Blazor component development.

## Component Base Classes Hierarchy

### 1. Base (Core Foundation)
- **Purpose**: Fundamental component with disposal, cancellation tokens, and logging
- **Key Features**:
  - Automatic `CancellationTokenSource` management
  - Built-in `ILogger` with lazy initialization
  - `IsDisposed` state tracking
  - Extended lifecycle methods with cancellation token support
  - Proper disposal pattern implementation

**Usage Pattern**:
```csharp
public class MyComponent : Base
{
    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);
        // Your initialization logic here
        await base.OnInitializedAsync(cancellationToken);
    }
}
```

### 2. ExtensionsBase (Provider Access Layer)
- **Purpose**: Provides access to all library providers and services
- **Available Providers**:
  - `Culture` - Translation and localization
  - `Workspace` - Workspace management  
  - `Catalog` - Catalog operations
  - `Authenticated` - Authentication state
  - `BreadcrumbsProvider` - Navigation breadcrumbs
  - `Toast` - Toast notifications
  - `Cookie` - Cookie management
  - `Navigation` - Blazor NavigationManager

**Key Properties**:
- `ShowBreadcrumbs` - Controls breadcrumb behavior (true/false/null)
- `Breadcrumbs` - List of breadcrumb items

**Translation Methods**:
```csharp
@T("translation.key", param1, param2)
@Translate("another.key", param1)
```

### 3. PageBase (Simple Page Foundation)
- **Purpose**: Basic page component inheriting from ExtensionsBase
- **Use When**: Creating simple pages without complex data loading

### 4. PageViewBase<TViewModel> (Data Display Pages)
- **Purpose**: Pages that display data with loading states and persistence
- **Key Features**:
  - Generic `TViewModel` for strongly-typed data
  - `IsLoading` state management
  - `PersistentModel` for state preservation between renders
  - `LoadAsync()` method for data retrieval
  - `RefreshAsync()` for manual data refresh
  - Automatic persistence with `PersistentComponentState`

**Implementation Pattern**:
```csharp
public partial class MyViewPage : PageViewBase<MyDataModel>
{
    protected override bool? ShowBreadcrumbs => true;
    protected override List<BreadcrumbsModel>? Breadcrumbs => new()
    {
        new("Home", "/"),
        new("My Section", "/section"),
        new("Current Page", null, disabled: true)
    };

    protected override async Task<MyDataModel?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Load your data here
            var data = await SomeService.GetDataAsync(cancellationToken);
            return data;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading data");
            Toast.AddError(T("error.loading.data"));
            throw;
        }
    }
}
```

### 5. PageEditBase<TViewModel> (Form/Edit Pages)
- **Purpose**: Pages with forms, validation, and editing capabilities
- **Key Features**:
  - Form validation with `EditContext` and `ValidationMessageStore`
  - Auto-save functionality with `AutoSaveAttribute`
  - Change tracking with `HasChanges` property
  - Duplicate submission prevention
  - Automatic string trimming and whitespace normalization
  - `Processing` state for form submission

**Implementation Pattern**:
```csharp
public partial class MyEditPage : PageEditBase<MyEditModel>
{
    [Inject] private IMyService MyService { get; set; } = default!;

    protected override bool? ShowBreadcrumbs => true;
    protected override List<BreadcrumbsModel>? Breadcrumbs => new()
    {
        new("Home", "/"),
        new("Edit", null, disabled: true)
    };

    protected override async Task<MyEditModel?> LoadAsync(CancellationToken cancellationToken)
    {
        // Load initial data for editing
        return await MyService.GetForEditAsync(Id, cancellationToken);
    }

    protected override async Task SubmitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await MyService.SaveAsync(Model, cancellationToken);
            Toast.AddSuccess(T("saved.successfully"));
            Navigation.NavigateTo("/success-page");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving data");
            Toast.AddError(T("error.saving.data"));
            throw;
        }
    }

    protected override void HandleInvalidSubmit(string message)
    {
        Toast.AddError(message);
    }

    protected override async Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue, CancellationToken cancellationToken)
    {
        // Implement auto-save logic for specific fields
        if (fieldName == nameof(Model.Title))
        {
            await MyService.AutoSaveTitleAsync(Model.Id, newValue?.ToString(), cancellationToken);
        }
    }
}
```

## Provider Usage Patterns

### Toast Notifications
```csharp
Toast.AddSuccess(T("operation.successful"));
Toast.AddError(T("operation.failed"));
Toast.AddWarning(T("validation.warning"));
Toast.AddInfo(T("information.message"));
Toast.AddNormal(T("standard.message"));
```

### Cookie Management
```csharp
// Get cookie
var userPreference = Cookie.Get("user-preference");

// Set cookie
Cookie.Set("user-preference", "value", days: 30);

// Async operations
await Cookie.SetAsync("async-cookie", "value", days: 7);
```

### Breadcrumb Management
```csharp
protected override bool? ShowBreadcrumbs => true; // Show breadcrumbs
// protected override bool? ShowBreadcrumbs => false; // Don't manage breadcrumbs
// protected override bool? ShowBreadcrumbs => null; // Clear breadcrumbs

protected override List<BreadcrumbsModel>? Breadcrumbs => new()
{
    new("Home", "/", icon: "home"),
    new("Category", "/category"),
    new("Current", null, disabled: true) // Current page - no link
};
```

### Translation/Localization
```csharp
// In Razor markup
<h1>@T("page.title")</h1>
<p>@T("welcome.message", userName)</p>

// In C# code
var message = Culture.Translate("error.message", errorCode);
Logger.LogInformation(Culture.Translate("operation.completed"));
```

## Model and Validation Patterns

### Auto-Save Attribute
```csharp
public class MyEditModel
{
    [AutoSave(DelayMs = 1000)] // Auto-save after 1 second of inactivity
    public string Title { get; set; } = "";

    [AutoSave] // Default 800ms delay
    public string Description { get; set; } = "";

    public string NotAutoSaved { get; set; } = "";
}
```

### Validation Setup
```csharp
public class MyEditModel
{
    [Required(ErrorMessage = "validation.required")]
    [StringLength(100, ErrorMessage = "validation.max.length")]
    public string Title { get; set; } = "";

    [EmailAddress(ErrorMessage = "validation.email")]
    public string Email { get; set; } = "";
}
```

## Form Markup Patterns

### Basic Form with PageEditBase
```html
<EditForm Model="Model" OnValidSubmit="HandleValidSubmit" OnInvalidSubmit="HandleInvalidSubmit">
    <DataAnnotationsValidator />
    
    <div class="form-group">
        <label for="title">@T("form.title"):</label>
        <InputText id="title" @bind-Value="Model.Title" class="form-control" />
        <ValidationMessage For="@(() => Model.Title)" />
    </div>

    <div class="form-group">
        <label for="description">@T("form.description"):</label>
        <InputTextArea id="description" @bind-Value="Model.Description" class="form-control" />
        <ValidationMessage For="@(() => Model.Description)" />
    </div>

    <div class="form-actions">
        <button type="submit" class="btn btn-primary" disabled="@(Processing || !IsValid)">
            @if (Processing)
            {
                <span class="spinner-border spinner-border-sm me-2"></span>
            }
            @T("button.save")
        </button>
        
        @if (HasChanges)
        {
            <span class="text-warning ms-2">@T("form.unsaved.changes")</span>
        }
    </div>
</EditForm>
```

### Data Display with PageViewBase
```html
@if (IsLoading)
{
    <div class="loading-spinner">
        <span class="spinner-border"></span>
        @T("loading.data")
    </div>
}
else if (Model != null)
{
    <div class="data-display">
        <h2>@Model.Title</h2>
        <p>@Model.Description</p>
        
        <button @onclick="() => RefreshAsync()" class="btn btn-secondary">
            @T("button.refresh")
        </button>
    </div>
}
else
{
    <div class="no-data">
        @T("no.data.available")
    </div>
}
```

## Error Handling Patterns

### Standard Error Handling
```csharp
protected override async Task<MyDataModel?> LoadAsync(CancellationToken cancellationToken)
{
    try
    {
        ThrowIfCancellationRequested(cancellationToken);
        var data = await MyService.GetDataAsync(cancellationToken);
        return data;
    }
    catch (OperationCanceledException)
    {
        // Handle cancellation gracefully
        if (!IsDisposed)
            throw;
        return null;
    }
    catch (UnauthorizedAccessException ex)
    {
        Logger.LogWarning(ex, "Unauthorized access to data");
        Toast.AddError(T("error.unauthorized"));
        Navigation.NavigateTo("/login");
        return null;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error loading data for {ComponentType}", GetType().Name);
        Toast.AddError(T("error.loading.data"));
        throw;
    }
}
```

## Lifecycle and Disposal Patterns

### Proper Disposal Override
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // Dispose your custom resources here
        MyCustomService?.Dispose();
        
        // Clear collections
        MyCollection?.Clear();
    }
    
    base.Dispose(disposing);
}
```

### Cancellation Token Usage
```csharp
protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
{
    await base.OnInitializedAsync(cancellationToken);
    
    if (IsDisposed || cancellationToken.IsCancellationRequested)
        return;
        
    ThrowIfCancellationRequested(cancellationToken);
    
    // Your initialization logic
}
```

## Configuration and Options

### Using Options Pattern
```csharp
public partial class MyPage : PageViewBase<MyModel>
{
    protected override async Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        await base.OnInitializedAsync(cancellationToken);
        
        // Access configuration with automatic refresh on changes
        var config = Options<MyConfiguration>();
        
        // Use configuration
        var apiUrl = config.ApiBaseUrl;
    }
}
```

## Best Practices Summary

1. **Always inherit from appropriate base class**: Use PageViewBase for data display, PageEditBase for forms
2. **Implement proper error handling**: Log errors, show user-friendly messages via Toast
3. **Use cancellation tokens**: Check for cancellation in long-running operations
4. **Set up breadcrumbs**: Configure navigation breadcrumbs for better UX
5. **Leverage providers**: Use Culture for translations, Toast for notifications, Cookie for preferences
6. **Handle loading states**: Show loading indicators during data operations
7. **Implement validation**: Use DataAnnotations and ValidationMessageStore
8. **Use auto-save wisely**: Apply AutoSaveAttribute to appropriate fields
9. **Track changes**: Monitor HasChanges for form state management
10. **Dispose properly**: Clean up resources in Dispose method
11. **Log appropriately**: Use provided Logger for debugging and monitoring
12. **Handle persistence**: Configure PersistentModel for state preservation

## Common Patterns to Follow

- **Data Loading**: Always implement LoadAsync for data retrieval
- **Form Submission**: Use SubmitAsync for save operations  
- **Validation**: Combine DataAnnotations with ValidationMessageStore
- **Error Messages**: Use Toast notifications for user feedback
- **Navigation**: Set up breadcrumbs for page hierarchy
- **Translation**: Use T() or Culture.Translate() for all user-facing text
- **State Management**: Leverage PersistentComponentState for data preservation
- **Cancellation**: Check cancellation tokens in async operations
- **Logging**: Log errors and important operations for debugging

This structure ensures consistent, maintainable, and user-friendly Blazor components that follow the Zonit.Extensions.Website library conventions.