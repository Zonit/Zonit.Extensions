# Zonit.Extensions

A comprehensive collection of .NET extensions and utilities designed to enhance development productivity, particularly for Blazor applications. This library provides essential tools for reflection, XML processing, cookie management, and Blazor component development.

## 🚀 Overview

Zonit.Extensions is a modular library that offers:

- **Reflection utilities** for assembly and type discovery
- **XML serialization helpers** for easy object-to-XML conversion
- **Cookie management** with full Blazor support
- **Blazor component base classes** for rapid UI development
- **Cross-platform compatibility** with .NET 8.0, 9.0, and 10.0

## 📦 Packages

The library is distributed across several NuGet packages to ensure you only include what you need:

### Core Extensions

| Package | Description | Installation |
|---------|-------------|--------------|
| **Zonit.Extensions** | Core utilities including Reflection and XML helpers | `Install-Package Zonit.Extensions` |
| **Zonit.Extensions.Abstractions** | Core abstractions and interfaces | `Install-Package Zonit.Extensions.Abstractions` |

### Website/Blazor Extensions

| Package | Description | Installation |
|---------|-------------|--------------|
| **Zonit.Extensions.Website** | Blazor components, cookie management, and UI utilities | `Install-Package Zonit.Extensions.Website` |
| **Zonit.Extensions.Website.Abstractions** | Website-related abstractions and interfaces | `Install-Package Zonit.Extensions.Website.Abstractions` |

## 🛠 Features

### 🔍 Reflection Utilities
Discover assemblies and types that implement or inherit from specified base types with intelligent filtering.

**[📚 Learn more about Reflection utilities →](Source/Zonit.Extensions/Reflection/Readme.md)**

### 📄 XML Processing
Simplified XML serialization and deserialization with built-in error handling.

**[📚 Learn more about XML utilities →](Source/Zonit.Extensions/Xml/Readme.md)**

### 📝 Text Analysis
Comprehensive text analysis tools including readability scoring, word counting, and reading time estimation.

**[📚 Learn more about Text utilities →](Source/Zonit.Extensions/Text/Readme.md)**

### 🍪 Cookie Management for Blazor
Comprehensive cookie handling with full Blazor Server and WebAssembly support.

#### Quick Setup

1. **Add to Routes.razor:**
```razor
@using Zonit.Extensions

<ZonitCookiesExtension />
```

2. **Configure services in Program.cs:**
```cs
builder.Services.AddCookiesExtension();
```

3. **Configure app in Program.cs:**
```cs
app.UseCookiesExtension();
```

#### Usage Example

```razor
@page "/"
@rendermode InteractiveServer
@using Zonit.Extensions.Website
@inject ICookieProvider Cookie

@foreach (var cookie in Cookie.GetCookies())
{
    <p>@cookie.Name: @cookie.Value</p>
}
```

#### Cookie API
```cs
public CookieModel? Get(string key);
public CookieModel Set(string key, string value, int days = 12 * 30);
public CookieModel Set(CookieModel model);
public Task<CookieModel> SetAsync(string key, string value, int days = 12 * 30);
public Task<CookieModel> SetAsync(CookieModel model);
public List<CookieModel> GetCookies();
```

> **Note:** Use `SetAsync` methods in Blazor circuits as they execute JavaScript code for cookie management.

### 🎨 Blazor Component Base Classes
Advanced base classes for Blazor components with built-in data loading, form handling, and state management.

**[📚 Learn more about Blazor Components →](Source/Zonit.Extensions.Website/Components/Readme.md)**

### 🛠 Additional Website Features
The Website package also includes additional utilities for web development:

- **[Breadcrumbs Management](Source/Zonit.Extensions.Website/Breadcrumbs/Readme.md)** - Simple breadcrumb navigation system with customizable items
- **Response Compression** - Optimized compression settings for web applications including Brotli and Gzip
- **Proxy Support** - Forwarded headers configuration for reverse proxy scenarios
- **HSTS Configuration** - HTTP Strict Transport Security setup utilities

## 🎯 Getting Started

1. **Choose your packages** based on your needs
2. **Install via NuGet** using the commands above
3. **Follow the specific setup instructions** in each component's documentation
4. **Explore the examples** in each feature's README

## 📖 Documentation

Each component has detailed documentation with examples:

- [Reflection Utilities Documentation](Source/Zonit.Extensions/Reflection/Readme.md)
- [XML Processing Documentation](Source/Zonit.Extensions/Xml/Readme.md)
- [Text Analysis Documentation](Source/Zonit.Extensions/Text/Readme.md)
- [Blazor Components Documentation](Source/Zonit.Extensions.Website/Components/Readme.md)
- [Breadcrumbs Documentation](Source/Zonit.Extensions.Website/Breadcrumbs/Readme.md)

## 🤝 Contributing

We welcome contributions! Please feel free to submit issues, feature requests, or pull requests.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🏢 About Zonit

Zonit.Extensions is developed and maintained by [Zonit](https://github.com/Zonit), focusing on creating high-quality, developer-friendly tools for the .NET ecosystem.