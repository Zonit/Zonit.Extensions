# Value Objects

A collection of immutable value objects for common domain concepts with built-in validation and SEO optimization.

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

**Price is different** - it's `decimal`-based with strong typing:

```csharp
// ? Price uses decimal (not string)
Price price = 19.99m;

// Blazor InputNumber binds directly to decimal:
// <InputNumber @bind-Value="model.Price" />
```

This design ensures:
- String-based VOs are convenient (implicit from string)
- Price maintains type safety (no accidental string parsing)
- Blazor forms work naturally with appropriate input types

---

## Available Value Objects

### `Culture`
Represents a culture in language format (e.g., "en-US", "pl-PL").

**Features:**
- Validates culture code using `CultureInfo`
- Default culture: `en-US`
- Automatic conversion from/to string

**Usage:**
```csharp
var culture = new Culture("en-US");
var polish = new Culture("pl-PL");

// Default culture
var defaultCulture = Culture.Default; // en-US

// String conversion
string code = culture; // Implicit conversion to string
```

---

### `Price`
Represents a monetary price with high precision for calculations and standard rounding for display.

**Features:**
- Internal precision: 8 decimal places (decimal(19,8))
- Display precision: 2 decimal places (accounting format)
- Arithmetic operators: `+`, `-`, `*`, `/`
- Comparison operators: `<`, `>`, `<=`, `>=`
- **Type-safe**: Always use `decimal`
- **No TypeConverter needed**: Blazor `InputNumber` binds directly to `decimal`

**Usage:**
```csharp
// ? In code - use decimal with 'm' suffix
Price price = 19.99m;
Price tax = 3.80m;

var total = price + tax; // 23.79
var discounted = price * 0.9m; // 17.991 (internally), 17.99 (display)

Console.WriteLine(price.Value);        // 19.99000000
Console.WriteLine(price.DisplayValue); // 19.99
```

**Blazor Forms:**
```html
<!-- ? InputNumber binds directly to decimal - no string conversion! -->
<InputNumber @bind-Value="model.Price" />
```

Price is strongly typed and doesn't need string parsing like other value objects.

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
- Removes diacritics (¹ ? a, ê ? e, etc.)
- Converts to lowercase
- Replaces spaces with hyphens
- Removes special characters
- No leading/trailing hyphens

**Usage:**
```csharp
var slug = new UrlSlug("Best Practices for .NET Development");
Console.WriteLine(slug.Value); // "best-practices-for-net-development"

// Polish characters
var polishSlug = new UrlSlug("¯ó³æ gêœl¹ jaŸñ");
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

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
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
