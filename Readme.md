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
- [ValueObjects](Source/Zonit.Extensions/ValueObjects) - Immutable value objects for common domain concepts (Price, Money, Title, Description, UrlSlug, Culture, **Asset**, **FileSize**, **Color**)

---

### Asset Value Object - File Handling

The `Asset` struct is a complete file container with embedded metadata. **MIME type is always detected from binary signature (magic bytes)** - never trust file extension alone.

```csharp
using Zonit.Extensions;

// From file
using var fileStream = File.OpenRead("document.pdf");
Asset asset = fileStream;  // Implicit conversion

// From bytes
byte[] data = await File.ReadAllBytesAsync("image.png");
Asset asset = data;

// Properties
Console.WriteLine(asset.Id);           // GUID - unique identifier
Console.WriteLine(asset.OriginalName); // "document.pdf"
Console.WriteLine(asset.UniqueName);   // "7a3b9c4d-1234-5678-90ab-cdef12345678.pdf"
Console.WriteLine(asset.MediaType);    // "application/pdf" (detected from signature!)
Console.WriteLine(asset.Signature);    // SignatureType.Pdf
Console.WriteLine(asset.Size);         // "1.5 MB" (FileSize VO)
Console.WriteLine(asset.Size.Megabytes); // 1.5
Console.WriteLine(asset.CreatedAt);    // 2026-01-21 12:34:56 UTC
Console.WriteLine(asset.Hash);         // SHA256 base64
Console.WriteLine(asset.Md5);          // MD5 base64

// Signature-based MIME detection (prevents extension spoofing)
// file.jpg that is actually WebP -> MediaType = "image/webp"
var uploaded = new Asset(bytes, "fake.jpg");
Console.WriteLine(uploaded.Signature);  // SignatureType.WebP
Console.WriteLine(uploaded.MediaType);  // "image/webp" (correct!)

// Implicit conversion back to Stream
Stream stream = asset;
await stream.CopyToAsync(outputStream);
```

### FileSize Value Object - Like TimeSpan for File Sizes

```csharp
// Create
var size = FileSize.FromMegabytes(1.5);
var size = new FileSize(1_500_000);  // bytes

// Convert
Console.WriteLine(size.Kilobytes);   // 1464.84
Console.WriteLine(size.Megabytes);   // 1.43
Console.WriteLine(size.Gigabytes);   // 0.0014
Console.WriteLine(size);           // "1.43 MB" (auto-formatted)

// Arithmetic
var total = size1 + size2;
var half = size / 2;

// Compare
if (fileSize > FileSize.FromMegabytes(10))
    Console.WriteLine("File too large!");

// Constants
FileSize.OneMegabyte      // 1 MB
FileSize.HundredMegabytes // 100 MB
FileSize.OneGigabyte      // 1 GB
```

See [Asset.Examples.md](Source/Zonit.Extensions/ValueObjects/Asset.Examples.md) for complete documentation.

---

### Price & Money Value Objects - Monetary Values with Precision

```csharp
using Zonit.Extensions;

// Price - always non-negative (products, costs)
Price price = 99.99m;
Price discounted = price.ApplyPercentage(-10);  // -10% discount
Console.WriteLine(price);                        // "99.99"
Console.WriteLine($"{price:C}");                 // "$99.99" (culture-dependent)

// Money - can be negative (balances, transactions)
Money balance = 500.00m;
Money withdrawal = -150.00m;
Money newBalance = balance + withdrawal;         // 350.00
Console.WriteLine(newBalance.IsPositive);        // true

// High precision internally (8 decimal places)
Console.WriteLine(price.ToFullPrecisionString()); // "99.99000000"

// IFormattable support
Console.WriteLine($"{price:C}");   // Currency format
Console.WriteLine($"{money:N2}");  // Number with 2 decimals
```

---

### Color Value Object - OKLCH Color Space

The `Color` struct stores colors in OKLCH format for maximum precision and perceptual uniformity, with easy conversion to other formats.

```csharp
using Zonit.Extensions;

// Create from various formats
Color color = Color.FromHex("#3498db");
Color color = Color.FromRgb(52, 152, 219);
Color color = Color.FromHsl(204, 0.7, 0.53);
Color color = Color.FromOklch(0.65, 0.15, 250);
Color color = "#3498db";  // Implicit conversion

// Access in different formats
Console.WriteLine(color.Hex);      // "#3498DB"
Console.WriteLine(color.CssRgb);   // "rgb(52, 152, 219)"
Console.WriteLine(color.CssHsl);   // "hsl(204, 70%, 53%)"
Console.WriteLine(color.CssOklch); // "oklch(65% 0.15 250)"

// RGB components
var (r, g, b) = color.Rgb;         // (52, 152, 219)
var (r, g, b, a) = color.Rgba;     // With alpha

// Color manipulation (OKLCH makes this perceptually uniform)
Color lighter = color.Lighten(0.1);
Color darker = color.Darken(0.1);
Color saturated = color.Saturate(0.05);
Color desaturated = color.Desaturate(0.05);
Color complement = color.Complementary;  // 180° hue rotation
Color gray = color.Grayscale;

// Mix colors
Color mixed = color1.Mix(color2, 0.5);  // 50% blend

// Alpha transparency
Color transparent = color.WithAlpha(0.5);
Console.WriteLine(transparent.HasAlpha);  // true

// Formatting
Console.WriteLine($"{color:hex}");    // "#3498DB"
Console.WriteLine($"{color:rgb}");    // "rgb(52, 152, 219)"
Console.WriteLine($"{color:hsl}");    // "hsl(204, 70%, 53%)"
Console.WriteLine($"{color:oklch}");  // "oklch(65% 0.15 250)"
```

**Why OKLCH?**
- Perceptually uniform - equal changes produce equal perceived color changes
- Better for color manipulation - adjusting lightness doesn't shift hue
- Wider gamut support - can represent P3, Rec2020 colors
- CSS Color Level 4 native support
- Lossless storage - converting from OKLCH to other formats preserves maximum information

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