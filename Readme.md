# Zonit.Extensions

## Useful tools for Blazor

---

### Abstractions Package

#### Zonit.Extensions.Abstractions - Interfaces and base abstractions

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Abstractions.svg)](https://www.nuget.org/packages/Zonit.Extensions.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Abstractions.svg)](https://www.nuget.org/packages/Zonit.Extensions.Abstractions/)

```bash
dotnet add package Zonit.Extensions.Abstractions
```

#### Zonit.Extensions - Core utilities and extensions

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)

```bash
dotnet add package Zonit.Extensions
```

**What's included:**
- [Exceptions](Source/Zonit.Extensions/Exceptions) - Structured exception handling with i18n support, error codes, and strongly-typed error parameters
- [Reflection](Source/Zonit.Extensions/Reflection) - Utility class for discovering assemblies and types that implement or inherit from a specified base type
- [Xml](Source/Zonit.Extensions/Xml) - Utility class for serializing objects to XML and deserializing XML back to objects
- [ValueObjects](Source/Zonit.Extensions/ValueObjects) - Immutable value objects for common domain concepts (Price, Title, Description, UrlSlug, Culture)

---

### Blazor Website Extensions

#### Zonit.Extensions.Website.Abstractions - Interfaces and abstractions for Blazor

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.Abstractions.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.Abstractions.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.Abstractions/)

```bash
dotnet add package Zonit.Extensions.Website.Abstractions 
```

#### Zonit.Extensions.Website - Blazor-specific components and utilities

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)

```bash
dotnet add package Zonit.Extensions.Website
```

**What's included:**
- [Components](Source/Zonit.Extensions.Website/Components) - Reusable Blazor components
- Cookie handling with Blazor support (see below)

---

### MudBlazor Integration

#### Zonit.Extensions.Website.MudBlazor - MudBlazor converters for Value Objects

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)

```bash
dotnet add package Zonit.Extensions.Website.MudBlazor
```

**What's included:**
- `ZonitTextField<T>` - MudTextField with automatic Value Object converter
- `ZonitTextArea<T>` - Multiline version for longer content
- Built-in exception handling with automatic error messages
- AOT and Trimming compatible (type inference at compile time)

**Supported Value Objects:** Title, Description, UrlSlug, Content, Url, Culture

**Usage (type is inferred from @bind-Value):**
```razor
@using Zonit.Extensions.MudBlazor

<ZonitTextField @bind-Value="Model.Title" Label="Title" />
<ZonitTextField @bind-Value="Model.Description" Label="Description" />
<ZonitTextField @bind-Value="Model.Slug" Label="URL Slug" />

@* For multiline content *@
<ZonitTextArea @bind-Value="Model.Content" Label="Content" />
```

No need to specify `T="Title"` - the compiler infers the type automatically!

---

## Cookie handling with support for Blazor

### Installation:
Add this in ``Routes.razor``
```razor
@using Zonit.Extensions

<ZonitCookiesExtension />
```

Services in ``Program.cs``
```cs
builder.Services.AddCookiesExtension();
```
App in ``Program.cs``
```cs
app.UseCookiesExtension();
```

### Example:

```razor
@page "/"
@rendermode InteractiveServer
@using Zonit.Extensions.Website
@inject ICookieProvider Cookie

@foreach (var cookie in Cookie.GetCookies())
{
    <p>@cookie.Name @cookie.Value</p>
}
```


**API**
```cs
    public CookieModel? Get(string key);
    public CookieModel Set(string key, string value, int days = 12 * 30);
    public CookieModel Set(CookieModel model);
    public Task<CookieModel> SetAsync(string key, string value, int days = 12 * 30);
    public Task<CookieModel> SetAsync(CookieModel model);
    public List<CookieModel> GetCookies();
```

We use SetAsync only in the Blazor circuit. It executes the JS code with the Cookies record.