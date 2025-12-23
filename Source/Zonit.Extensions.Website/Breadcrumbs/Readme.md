# Zonit.Extensions.Website - Breadcrumbs

A simple and flexible breadcrumb navigation system for Blazor applications. This component helps users understand their current location within your application's hierarchy and provides easy navigation back to previous levels.

## Features

- **Dynamic Breadcrumb Management** - Add, remove, and update breadcrumbs programmatically
- **Customizable Items** - Support for text, links, icons, and disabled states  
- **Event-Driven Updates** - Automatic UI updates when breadcrumb data changes
- **Template Support** - Custom templates for interactive breadcrumb items
- **Dependency Injection Ready** - Seamlessly integrates with .NET DI container

## Installation

The breadcrumbs functionality is included in the `Zonit.Extensions.Website` package:

```bash
Install-Package Zonit.Extensions.Website
```

## Usage

### 1. Service Registration

Register the breadcrumbs service in your `Program.cs`:

```csharp
using Zonit.Extensions.Website;

var builder = WebApplication.CreateBuilder(args);

// Register breadcrumbs service
builder.Services.AddScoped<IBreadcrumbsProvider, BreadcrumbsService>();

var app = builder.Build();
```

### 2. Basic Usage

Inject and use the breadcrumbs provider in your Blazor components:

```razor
@page "/products/{categoryId}/details/{productId}"
@inject IBreadcrumbsProvider Breadcrumbs

@code {
    [Parameter] public string CategoryId { get; set; } = string.Empty;
    [Parameter] public string ProductId { get; set; } = string.Empty;

    protected override void OnInitialized()
    {
        // Create breadcrumb trail
        var breadcrumbs = new List<BreadcrumbsModel>
        {
            new("Home", "/"),
            new("Products", "/products"),
            new($"Category {CategoryId}", $"/products/{CategoryId}"),
            new($"Product {ProductId}", null, disabled: true) // Current page - disabled
        };

        Breadcrumbs.Initialize(breadcrumbs);
    }
}
```

### 3. Display Breadcrumbs

Create a component to display the breadcrumbs:

```razor
@implements IDisposable
@inject IBreadcrumbsProvider BreadcrumbsProvider

<nav aria-label="breadcrumb">
    <ol class="breadcrumb">
        @if (breadcrumbs != null)
        {
            @foreach (var breadcrumb in breadcrumbs)
            {
                <li class="breadcrumb-item @(breadcrumb.Disabled ? "active" : "")">
                    @if (!breadcrumb.Disabled && !string.IsNullOrEmpty(breadcrumb.Href))
                    {
                        <a href="@breadcrumb.Href">
                            @if (!string.IsNullOrEmpty(breadcrumb.Icon))
                            {
                                <i class="@breadcrumb.Icon"></i>
                            }
                            @breadcrumb.Text
                        </a>
                    }
                    else
                    {
                        @if (!string.IsNullOrEmpty(breadcrumb.Icon))
                        {
                            <i class="@breadcrumb.Icon"></i>
                        }
                        @breadcrumb.Text
                    }
                </li>
            }
        }
    </ol>
</nav>

@code {
    private IReadOnlyList<BreadcrumbsModel>? breadcrumbs;

    protected override void OnInitialized()
    {
        BreadcrumbsProvider.OnChange += HandleBreadcrumbsChanged;
        breadcrumbs = BreadcrumbsProvider.Get();
    }

    private void HandleBreadcrumbsChanged()
    {
        breadcrumbs = BreadcrumbsProvider.Get();
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        BreadcrumbsProvider.OnChange -= HandleBreadcrumbsChanged;
    }
}
```

## API Reference

### IBreadcrumbsProvider Interface

```csharp
public interface IBreadcrumbsProvider
{
    /// <summary>
    /// Initializes the breadcrumbs with a new collection.
    /// </summary>
    void Initialize(IList<BreadcrumbsModel>? model);
    
    /// <summary>
    /// Gets the current breadcrumbs.
    /// </summary>
    IReadOnlyList<BreadcrumbsModel>? Get();
    
    /// <summary>
    /// Event triggered when breadcrumbs change.
    /// </summary>
    event Action? OnChange;
}
```

### BreadcrumbsModel Class

```csharp
public class BreadcrumbsModel
{
    public BreadcrumbsModel(string text, string? href, bool disabled = false, string? icon = null)
    
    /// <summary>
    /// The text to display.
    /// </summary>
    public string Text { get; }
    
    /// <summary>
    /// The URL to navigate to when clicked.
    /// </summary>
    public string? Href { get; }
    
    /// <summary>
    /// Prevents this item from being clicked.
    /// </summary>
    public bool Disabled { get; }
    
    /// <summary>
    /// The custom icon for this item.
    /// </summary>
    public string? Icon { get; }
    
    /// <summary>
    /// Custom template for interactive scenarios.
    /// </summary>
    public string? Template { get; set; }
}
```

## Advanced Examples

### Dynamic Breadcrumbs with Icons

```csharp
var breadcrumbs = new List<BreadcrumbsModel>
{
    new("Dashboard", "/dashboard", icon: "fas fa-home"),
    new("Users", "/users", icon: "fas fa-users"),
    new("User Profile", "/users/123", icon: "fas fa-user"),
    new("Edit Profile", null, disabled: true, icon: "fas fa-edit")
};

Breadcrumbs.Initialize(breadcrumbs);
```

### Conditional Breadcrumbs

```csharp
var breadcrumbs = new List<BreadcrumbsModel>();

breadcrumbs.Add(new("Home", "/"));

if (User.IsInRole("Admin"))
{
    breadcrumbs.Add(new("Admin Panel", "/admin"));
}

breadcrumbs.Add(new("Current Page", null, disabled: true));

Breadcrumbs.Initialize(breadcrumbs);
```

## Best Practices

1. **Always disable the current page** breadcrumb to indicate the user's location
2. **Provide meaningful text** that clearly describes each level
3. **Use icons sparingly** to avoid cluttering the navigation
4. **Keep breadcrumb trails short** - typically no more than 5 levels
5. **Update breadcrumbs on navigation** to maintain accurate state

## Integration with Blazor Routing

For automatic breadcrumb management based on routes, consider creating a service that listens to navigation events:

```csharp
@inject NavigationManager Navigation
@inject IBreadcrumbsProvider Breadcrumbs

@code {
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        UpdateBreadcrumbs();
    }
    
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateBreadcrumbs();
    }
    
    private void UpdateBreadcrumbs()
    {
        var uri = new Uri(Navigation.Uri);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Build breadcrumbs based on URL segments
        // Implementation depends on your routing structure
    }
}
```

This breadcrumb system provides a solid foundation for navigation within your Blazor applications while remaining flexible enough to adapt to various UI frameworks and design systems.