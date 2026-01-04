# Value Objects

A collection of immutable value objects for common domain concepts with built-in validation and SEO optimization.

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

**Usage:**
```csharp
var price = new Price(19.99m);
var tax = new Price(3.80m);

var total = price + tax; // 23.79
var discounted = price * 0.9m; // 17.991 (internally), 17.99 (display)

Console.WriteLine(price.Value);        // 19.99000000
Console.WriteLine(price.DisplayValue); // 19.99
```

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
