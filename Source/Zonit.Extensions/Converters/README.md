# Value Object Type Converters

## Overview

All value objects in `Zonit.Extensions` now have automatic validation through `TypeConverter` integration. This means **no validation attributes are needed** in your models - validation happens automatically during model binding.

## How It Works

When you use a value object in your Blazor EditModel:

```csharp
public class ArticleEditModel
{
    [Required]
    public Description Description { get; set; }
    
    [Required]
    public Title Title { get; set; }
    
    [Required]
    public UrlSlug Slug { get; set; }
}
```

**Without any `[StringLength]` attributes**, the framework will:

1. ? Validate **minimum length** (from `MinLength` constant)
2. ? Validate **maximum length** (from `MaxLength` constant)
3. ? Add errors to `ModelState` automatically
4. ? Show user-friendly error messages

## Example

### EditModel (Blazor)

```csharp
public class ArticleEditModel
{
    [Required(ErrorMessage = "Title is required")]
    public Title Title { get; set; }
    
    [Required(ErrorMessage = "Description is required")]
    public Description Description { get; set; }
}
```

### What Happens

**User enters 200 characters for Description:**

1. ? Framework detects it exceeds `Description.MaxLength` (160)
2. ? Adds error to `ModelState`: *"Description cannot exceed 160 characters."*
3. ? User sees the error **before** any constructor is called
4. ? No exceptions, just clean validation

## Supported Value Objects

| Value Object | Min Length | Max Length | Auto-Validated |
|--------------|------------|------------|----------------|
| `Description` | 1 | 160 | ? |
| `Title` | 1 | 60 | ? |
| `UrlSlug` | 1 | 200 | ? |

## How It's Implemented

Each value object uses the generic `ValueObjectTypeConverter<T>`:

```csharp
[TypeConverter(typeof(ValueObjectTypeConverter<Description>))]
public readonly struct Description : IEquatable<Description>
{
    public const int MaxLength = 160;
    public const int MinLength = 1;
    
    public static bool TryCreate(string? value, out Description description)
    {
        // Validation logic here
    }
}
```

The `ValueObjectTypeConverter`:
- Uses reflection to find `TryCreate` method
- Extracts `MinLength` and `MaxLength` constants
- Generates user-friendly error messages
- Integrates with ASP.NET Core model binding

## Benefits

? **No repetition** - validation rules in one place (value object)  
? **Type safety** - compiler enforces correct usage  
? **Clean models** - no validation attribute clutter  
? **Consistent** - same rules everywhere (UI, API, domain)  
? **User-friendly** - proper ModelState errors, not exceptions

## Adding to New Value Objects

To add automatic validation to a new value object:

1. Implement `TryCreate(string?, out T)` method
2. Add `MinLength` and `MaxLength` constants
3. Add `[TypeConverter]` attribute:

```csharp
[TypeConverter(typeof(ValueObjectTypeConverter<YourValueObject>))]
public readonly struct YourValueObject : IEquatable<YourValueObject>
{
    public const int MinLength = 1;
    public const int MaxLength = 100;
    
    public static bool TryCreate(string? value, out YourValueObject result)
    {
        // Your validation logic
    }
}
```

That's it! The converter handles the rest automatically.
