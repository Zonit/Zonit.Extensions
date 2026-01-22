# Value Objects

A collection of immutable value objects for common domain concepts with built-in validation, SEO optimization, and modern C# features.

## ?? Key Features

| Feature | Description |
|---------|-------------|
| ? **Safe implicit `string ? ValueObject`** | Auto truncate/empty for invalid values |
| ? **`IComparable<T>`** | Sorting and comparison (`<`, `>`, `<=`, `>=`) |
| ? **`IParsable<T>`** | Modern parsing (.NET 7+) |
| ? **`JsonConverter`** | Native System.Text.Json serialization |
| ? **`TypeConverter`** | ASP.NET model binding |
| ? **`Empty` property** | Safe default value |
| ? **`HasValue` property** | Check if has value |
| ? **Null-safe operations** | `GetHashCode()`, `ToString()`, `Length` |

## ?? Safe Implicit Conversion

All string-based value objects support **safe implicit conversion** from `string`:

```csharp
// ? Works - creates Title
Title title = "My Title";

// ? Works - returns Title.Empty (no exception!)
Title emptyTitle = "";
Title nullTitle = null;

// ? Works - auto truncates to 60 chars with "..."
Title longTitle = "Very long title that exceeds the maximum length for SEO...";
```

**Behavior:**
- `null` or whitespace ? returns `ValueObject.Empty`
- Too long text (Title, Description) ? auto truncate with "..."
- Invalid URL/Culture ? returns `ValueObject.Empty`

## ?? Sorting & Comparison (`IComparable<T>`)

```csharp
var titles = new List<Title> { "Zebra", "Apple", "Mango" };
titles.Sort(); // Apple, Mango, Zebra

if (title1 < title2) { ... }
if (title1 >= title2) { ... }
```

## ?? Modern Parsing (`IParsable<T>`)

```csharp
// Parse - throws FormatException for invalid values
Title title = Title.Parse("My Title", null);

// TryParse - safe parsing
if (Title.TryParse(input, null, out var title)) { ... }

// Works with generic constraints
T ParseValue<T>(string input) where T : IParsable<T>
    => T.Parse(input, null);
```

## ?? JSON Serialization

```csharp
// Automatic serialization/deserialization
var product = new Product { Title = "My Product" };
var json = JsonSerializer.Serialize(product);
// {"Title":"My Product"}

// Global configuration (optional):
services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ValueObjectJsonConverterFactory());
    });
```

## ? Automatic Validation (No Attributes Required!)

All value objects now have **automatic model binding validation** through `TypeConverter` integration. This means:

- ? **No `[StringLength]` attributes needed** in your models
- ? **Validation happens automatically** during model binding
- ? **User-friendly error messages** in `ModelState`
- ? **Single source of truth** - validation rules in value object only

### Example Usage in Blazor

```csharp
public class ArticleEditModel
{
    [Required]
    public Title Title { get; set; }
    
    [Required]
    public Description Description { get; set; }
    
    [Required]
    public UrlSlug Slug { get; set; }
}
```

**That's it!** Length validation is automatic based on `MinLength` and `MaxLength` constants.

See [Converters/README.md](Converters/README.md) for details.

---

## ?? String Conversion (implicit operators)

Most string-based value objects support **implicit conversion** from `string` for convenience:

```csharp
// ? Works with implicit operators (string-based VOs)
Title title = "Hello World";
Description desc = "A description";
UrlSlug slug = "my-article";
Url url = "https://example.com";
Culture culture = "en-US";
```

**Price and Money are different** - they're `decimal`-based with strong typing:

```csharp
// ✅ Price and Money use decimal (not string)
Price price = 19.99m;  // Non-negative only
Money balance = -50m;  // Allows negative

// Blazor InputNumber binds directly to decimal:
// <InputNumber @bind-Value="model.Price" />
// <InputNumber @bind-Value="model.Balance" />
```

This design ensures:
- String-based VOs are convenient (implicit from string)
- Price and Money maintain type safety (no accidental string parsing)
- Blazor forms work naturally with appropriate input types

---

## Available Value Objects

### `Asset`
Represents a file asset as an immutable value object for transfer and storage.

**Features:**
- Stores file data (`byte[]`), file name, and MIME type
- SHA256 hash for integrity verification and deduplication
- Safe filename generation (GUID-based, timestamp-based)
- File signature (magic bytes) detection and validation
- Nested types: `Asset.MimeType`, `Asset.FileName`
- Categories: Image, Video, Audio, Document, Text, Archive
- EF Core integration (stored as `byte[]` with embedded header)

**For detailed examples, see [Asset.Examples.md](Asset.Examples.md)**

**Quick Usage:**
```csharp
// Create from bytes
var asset = new Asset(fileBytes, "document.pdf");

// Properties
Console.WriteLine(asset.Name);           // "document.pdf"
Console.WriteLine(asset.ContentType);    // "application/pdf"
Console.WriteLine(asset.Size);           // 1234567
Console.WriteLine(asset.SizeFormatted);  // "1.2 MB"
Console.WriteLine(asset.Hash);           // SHA256 hash
Console.WriteLine(asset.IsDocument);     // true
Console.WriteLine(asset.Category);       // AssetCategory.Document

// Safe filename for storage
var safeName = asset.GetSafeFileName();               // "7a3b9c4d-1234-5678-90ab-cdef12345678.pdf"
var timestamped = asset.GetSafeFileNameWithTimestamp(); // "20260120_143530_7a3b9c4d.pdf"

// Data conversions
var base64 = asset.ToBase64();
var dataUrl = asset.ToDataUrl();   // data:application/pdf;base64,...
var stream = asset.ToStream();
var memory = asset.AsMemory();     // Zero-copy

// Security validation
var validation = asset.Validate(AssetValidationOptions.Documents());
if (!validation.IsValid)
    Console.WriteLine(string.Join(", ", validation.Errors));
```

---

### `Culture`
Represents a culture in language format (e.g., "en-US", "pl-PL").

**Features:**
- Validates culture code using `CultureInfo`
- Default culture: `en-US` (use `Culture.Default`)
- `ValueOrDefault` property returns "en-US" if empty
- `ToCultureInfoOrDefault()` returns CultureInfo for "en-US" if empty
- Automatic conversion from/to string

**Important:** `default(Culture)` has `Value = null`. Use `Culture.Default` for "en-US".

**Usage:**
```csharp
var culture = new Culture("en-US");
var polish = new Culture("pl-PL");

// Default culture (en-US)
var defaultCulture = Culture.Default;

// Safe default value when empty
Culture empty = Culture.Empty;
string code = empty.ValueOrDefault; // "en-US"
CultureInfo info = empty.ToCultureInfoOrDefault(); // en-US CultureInfo

// String conversion
string code = culture; // Implicit conversion to string
```

---

### `Color`
Represents a color stored in OKLCH format for maximum precision and perceptual uniformity.

**Features:**
- **OKLCH storage** - perceptually uniform color space (CSS Color Level 4)
- Conversions to Hex, RGB, HSL formats
- Color manipulation - lighten, darken, saturate, desaturate
- Complementary colors, mixing, grayscale
- Wide gamut support (P3, Rec2020)
- Alpha/transparency support
- `IFormattable` - multiple output formats
- `IParsable<Color>` - parse from hex, rgb, hsl, oklch
- `JsonConverter` - stores as OKLCH for full precision

**Usage:**
```csharp
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

// Color manipulation (perceptually uniform)
Color lighter = color.Lighten(0.1);
Color darker = color.Darken(0.1);
Color saturated = color.Saturate(0.05);
Color complement = color.Complementary;  // 180° hue rotation
Color gray = color.Grayscale;

// Mix colors
Color mixed = color1.Mix(color2, 0.5);  // 50% blend

// Alpha transparency
Color transparent = color.WithAlpha(0.5);
Console.WriteLine(transparent.HasAlpha);  // true

// IFormattable - format output
Console.WriteLine($"{color:hex}");    // "#3498DB"
Console.WriteLine($"{color:rgb}");    // "rgb(52, 152, 219)"
Console.WriteLine($"{color:hsl}");    // "hsl(204, 70%, 53%)"
Console.WriteLine($"{color:oklch}");  // "oklch(65% 0.15 250)" (default)
```

**Why OKLCH?**
- **Perceptually uniform** - equal changes produce equal perceived color differences
- **Better manipulation** - adjusting lightness doesn't shift hue (unlike HSL)
- **Wider gamut** - represents colors outside sRGB (P3, Rec2020)
- **Lossless** - converting from OKLCH preserves maximum color information
- **Modern standard** - CSS Color Level 4 native support

**When to use Color vs string:**
| Type | Use Case | Benefits |
|------|----------|----------|
| `Color` | Brand colors, themes, dynamic color manipulation | Type-safe, validated, perceptually uniform |
| `string` | Simple hex storage without manipulation | Simpler storage |

---

### `Price`
Represents a monetary price with high precision for calculations and standard rounding for display.
**Use for product prices, unit costs - values that should never be negative.**

**Features:**
- Internal precision: 8 decimal places (decimal(19,8))
- Display precision: 2 decimal places (accounting format)
- **Non-negative by default** - throws exception for negative values
- Arithmetic operators: `+`, `-`, `*`, `/`
- `IParsable<Price>` for modern parsing
- `IFormattable` for string formatting (`{price:C}`, `{price:N2}`)
- `JsonConverter` for automatic JSON serialization
- Comparison operators: `<`, `>`, `<=`, `>=`
- **Type-safe**: Always use `decimal`
- **No TypeConverter needed**: Blazor `InputNumber` binds directly to `decimal`

**Usage:**
```csharp
// ✅ In code - use decimal with 'm' suffix
Price price = 19.99m;
Price tax = 3.80m;

var total = price + tax; // 23.79
var discounted = price * 0.9m; // 17.991 (internally), 17.99 (display)

Console.WriteLine(price.Value);        // 19.99000000
Console.WriteLine(price.DisplayValue); // 19.99

// IFormattable - formatting support
Console.WriteLine($"{price:C}");     // "$19.99" (culture-dependent)
Console.WriteLine($"{price:N2}");    // "19.99"

// ❌ Negative values throw exception
var invalid = new Price(-10m); // Throws ArgumentOutOfRangeException
```

**Blazor Forms:**
```html
<!-- ✅ InputNumber binds directly to decimal - no string conversion! -->
<InputNumber @bind-Value="model.Price" />
```

Price is strongly typed and doesn't need string parsing like other value objects.

---

### `Money`
Represents a monetary amount that can be positive or negative.
**Use for balances, transactions, adjustments, refunds - values that may go negative.**

**Features:**
- Internal precision: 8 decimal places (decimal(19,8))
- Display precision: 2 decimal places (accounting format)
- **Allows negative values** - for debits, refunds, adjustments
- Arithmetic operators: `+`, `-`, `*`, `/`
- Unary minus operator: `-money`
- `IParsable<Money>` for modern parsing
- `IFormattable` for string formatting (`{money:C}`, `{money:N2}`)
- `JsonConverter` for automatic JSON serialization
- Comparison operators: `<`, `>`, `<=`, `>=`
- Helper properties: `IsNegative`, `IsPositive`, `IsZero`
- Conversion to/from `Price`

**Usage:**
```csharp
// ✅ Allows negative values
Money balance = 100m;
Money debit = -50m;  // Negative is OK

var newBalance = balance + debit; // 50

Console.WriteLine(debit.IsNegative); // true
Console.WriteLine(debit.Abs());      // 50.00

// Convert between Money and Price
Money amount = new Price(19.99m);  // Implicit conversion Price → Money
Price price = amount.ToPrice();    // Explicit conversion Money → Price (throws if negative)

// Safe conversion
if (amount.TryToPrice(out var safePrice))
{
    // Use safePrice
}
```

**When to use Price vs Money:**
| Type | Use Case | Negative Values |
|------|----------|-----------------|
| `Price` | Product prices, unit costs | ❌ Not allowed |
| `Money` | Balances, transactions, adjustments | ✅ Allowed |

---

### `Title`
Represents a title for content (articles, products, categories, etc.).

**Features:**
- SEO optimized: max 60 characters (Google search results limit)
- Min length: 1 character
- Automatic validation and trimming

**Usage:**
```csharp
var title = new Title("Best Practices for .NET Development");

// Validation
Title.IsValid("My Title", out var error); // Returns true

// Invalid title throws exception
var invalid = new Title(""); // ? Throws ArgumentException
var tooLong = new Title(new string('A', 61)); // ? Throws ArgumentException
```

---

### `Description`
Represents a description for content (articles, products, categories, etc.).

**Features:**
- SEO optimized: max 160 characters (Google meta description limit)
- Min length: 1 character
- Automatic validation and trimming

**Usage:**
```csharp
var description = new Description("A comprehensive guide to modern .NET development practices and patterns.");

// Validation
Description.IsValid("My description", out var error); // Returns true

// Invalid description throws exception
var invalid = new Description(""); // ? Throws ArgumentException
var tooLong = new Description(new string('A', 161)); // ? Throws ArgumentException
```

---

### `UrlSlug`
Represents a URL-friendly slug generated from text.

**Features:**
- Removes diacritics (� ? a, � ? e, etc.)
- Converts to lowercase
- Replaces spaces with hyphens
- Removes special characters
- No leading/trailing hyphens

**Usage:**
```csharp
var slug = new UrlSlug("Best Practices for .NET Development");
Console.WriteLine(slug.Value); // "best-practices-for-net-development"

// Polish characters
var polishSlug = new UrlSlug("��� g�l� ja��");
Console.WriteLine(polishSlug.Value); // "zolc-gesla-jazn"

// Special characters removed
var cleanSlug = new UrlSlug("Hello, World! @#$%");
Console.WriteLine(cleanSlug.Value); // "hello-world"
```

---

### `Url`
Represents a valid absolute or relative URL with validation.

**Features:**
- Automatic URL validation
- Support for absolute and relative URLs
- Built-in URL manipulation (combine paths, query parameters)
- Properties: Scheme, Host, Port, Path, Query, IsHttps

**Usage:**
```csharp
var url = new Url("https://example.com/api/users");

// Properties
Console.WriteLine(url.Host);    // example.com
Console.WriteLine(url.IsHttps); // true
Console.WriteLine(url.Path);    // /api/users

// Combine paths
var detailUrl = url.Combine("123");
// https://example.com/api/users/123

// Query parameters
var pagedUrl = url
    .WithQueryParameter("page", "1")
    .WithQueryParameter("limit", "10");
// https://example.com/api/users?page=1&limit=10

// Safe creation
if (Url.TryCreate(userInput, out var validUrl))
{
    // Use validUrl
}
```

---

### `Schedule`
Represents a schedule rule with compact binary storage (16 bytes). Works like cron but with strong typing.

**Features:**
- **Binary Storage** - Compact 16-byte format for database storage
- **Two Modes** - Interval (every X time) or Calendar (cron-like)
- **Nullable Fields** - null = wildcard (any value)
- **Factory Methods** - `EveryMinutes(5)`, `EveryDay(15, 0)`, etc.
- **GetNextOccurrence** - Calculate next execution time
- **JsonConverter** and **TypeConverter** support

**For detailed examples, see [Schedule.README.md](Schedule.README.md)**

**Quick Usage:**
```csharp
// Interval mode: every 5 minutes
var schedule = Schedule.EveryMinutes(5);

// Calendar mode: daily at 15:00
var daily = Schedule.EveryDay(15, 0);

// Weekly on Monday at 9:00
var weekly = Schedule.EveryWeek(DayOfWeek.Monday, 9, 0);

// Monthly on 1st at midnight
var monthly = Schedule.EveryMonth(1, 0, 0);

// Get next occurrence
DateTimeOffset? next = schedule.GetNextOccurrence(DateTimeOffset.Now);

// Binary storage (16 bytes)
byte[] bytes = schedule.ToBytes();
Schedule restored = Schedule.FromBytes(bytes);

// Multiple schedules (e.g., 8:00 and 18:00)
Schedule[] morningAndEvening = [
    Schedule.EveryDay(8, 0),
    Schedule.EveryDay(18, 0)
];
```

---

## Common Features

All value objects implement:

? **Immutability** - Once created, values cannot be changed  
? **Equality** - Value-based equality (`IEquatable<T>`)  
? **Validation** - Built-in validation with clear error messages  
? **String conversion** - Implicit/explicit conversion to string  
? **Null safety** - Proper null handling

---

## Why Use Value Objects?

### ? Without Value Objects
```csharp
public class Product
{
    public string Title { get; set; } // No validation
    public decimal Price { get; set; } // No precision guarantee
    public string Culture { get; set; } // Invalid culture codes possible
}

// Problems:
var product = new Product 
{ 
    Title = new string('A', 1000), // Too long for SEO!
    Price = 19.999999m,             // Precision issues
    Culture = "invalid"             // Invalid culture
};
```

### ? With Value Objects
```csharp
public class Product
{
    public Title Title { get; set; }
    public Price Price { get; set; }
    public Culture Culture { get; set; }
}

// Benefits:
var product = new Product 
{ 
    Title = new Title("Best Product"), // ? Validated (max 60 chars)
    Price = new Price(19.99m),          // ? Correct precision
    Culture = new Culture("en-US")      // ? Valid culture
};

// Invalid values throw exceptions immediately:
var invalid = new Title(new string('A', 61)); // ? Exception at creation
```

---

## Best Practices

### ? DO:
1. Use value objects for domain concepts (money, titles, slugs, etc.)
2. Validate in constructor - fail fast
3. Make them immutable
4. Override `Equals` and `GetHashCode` for value-based comparison
5. Provide static validation methods (`IsValid`)

### ? DON'T:
1. Don't use primitive types for domain concepts
2. Don't allow invalid state
3. Don't make value objects mutable
4. Don't use value objects for simple IDs (use primitives)

---

## Integration with Entity Framework

Use `Zonit.Extensions.Databases.SqlServer` for automatic Value Object converters:

```csharp
// In DbContext.OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Automatically configures all Value Object converters
    modelBuilder.UseZonitDatabasesConverters();
}
```

### Manual Configuration (if needed)

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // Asset - stored as byte[] with embedded header
        builder.Property(p => p.Attachment)
            .HasConversion(
                v => v.ToStorageBytes(),
                v => Asset.FromStorageBytes(v)
            );
        
        builder.Property(p => p.Title)
            .HasConversion(
                v => v.Value,
                v => new Title(v)
            )
            .HasMaxLength(Title.MaxLength);

        builder.Property(p => p.Price)
            .HasConversion(
                v => v.Value,
                v => new Price(v)
            )
            .HasPrecision(19, 8);

        builder.Property(p => p.Culture)
            .HasConversion(
                v => v.Value,
                v => new Culture(v)
            )
            .HasMaxLength(10);
    }
}
```

---

## Summary

Value objects provide:
- **Type safety** - Compiler catches mistakes
- **Validation** - Invalid values cannot exist
- **Immutability** - No accidental changes
- **Domain clarity** - Clear business rules
- **SEO optimization** - Built-in best practices for web content

Use them for domain concepts that have business rules and constraints! ??
