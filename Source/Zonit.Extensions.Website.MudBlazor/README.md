# Zonit.Extensions.Website.MudBlazor

MudBlazor components with automatic Value Object converter support for Zonit.Extensions.

## Installation

```xml
<PackageReference Include="Zonit.Extensions.Website.MudBlazor" Version="1.0.0" />
```

Add to `_Imports.razor`:
```razor
@using Zonit.Extensions.MudBlazor
```

## Components

### ZonitTextField\<T\>

A `MudTextField` with automatic Value Object converter support. Type `T` is inferred from `@bind-Value`.

```razor
<ZonitTextField @bind-Value="Model.Title" Label="Title" />
```

#### Supported Value Objects

| Type | MaxLength | MinLength |
|------|-----------|-----------|
| `Title` | 60 | 1 |
| `Description` | 160 | 1 |
| `UrlSlug` | 250 | 1 |
| `Content` | 2500 | 1 |
| `Url` | - | - |
| `Culture` | - | - |

#### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Copyable` | `bool` | `false` | Shows a copy-to-clipboard button. Icon changes to green ✓ for 2 seconds after copying. |
| `Counter` | `int` | - | When set to `0`, automatically uses MaxLength from Value Object (e.g., 60 for Title). |

#### Features

- **Automatic Converter** - No need to create custom converters for Value Objects
- **Nullable Support** - Works with both `Title` and `Title?`
- **MaxLength + 1** - Allows user to exceed limit by 1 char to see validation error
- **Text Preservation** - Input text is preserved when validation fails (not cleared)
- **Clean Error Messages** - Removes technical details like `(Parameter 'value')` and `Current length: xxx.`
- **Auto Counter** - `Counter="0"` automatically detects MaxLength from Value Object

#### Usage Examples

```razor
@* Basic usage *@
<ZonitTextField @bind-Value="Model.Title" 
                Label="Title" 
                Variant="Variant.Outlined" />

@* With character counter (auto-detected from Value Object) *@
<ZonitTextField @bind-Value="Model.Title" 
                Label="Title" 
                Counter="0" />

@* With copy button *@
<ZonitTextField @bind-Value="Model.Title" 
                Label="Title" 
                Copyable />

@* All features combined *@
<ZonitTextField @bind-Value="Model.Title" 
                Label="Title" 
                Variant="Variant.Outlined"
                Counter="0"
                Copyable
                Immediate="true" />
```

---

### ZonitTextArea\<T\>

Multiline variant of `ZonitTextField`. Inherits all features.

```razor
<ZonitTextArea @bind-Value="Model.Content" 
               Label="Content" 
               Lines="5" />
```

#### Default Values

| Parameter | Default |
|-----------|---------|
| `Lines` | `3` |

---

## How It Works

### Validation Flow

1. User types text in input
2. `MaxLength + 1` allows exceeding the limit by 1 character
3. Value Object constructor throws exception on validation failure
4. `ValueObjectConverter` catches exception and throws `ConversionException`
5. MudBlazor displays error message under input
6. `UpdateTextPropertyAsync` override preserves text (doesn't clear on error)

### Copy to Clipboard

1. User clicks copy button (📋 icon)
2. Text is copied using MudBlazor's built-in `mudWindow.copyToClipboard`
3. Icon changes to green checkmark (✓) for 2 seconds
4. Icon returns to copy icon (📋)

---

## Requirements

- .NET 8.0 / 9.0 / 10.0
- MudBlazor 9.0.0-preview.2 or later
- Zonit.Extensions (for Value Objects)

---

## Architecture

```
Zonit.Extensions.Website.MudBlazor/
├── Components/
│   ├── ZonitTextField.cs    # Main component with converter logic
│   └── ZonitTextArea.cs     # Multiline variant
├── _Imports.razor           # Global usings
└── README.md
```

### Internal Classes

| Class | Description |
|-------|-------------|
| `ValueObjectConverter<T, TValueObject>` | Implements `IReversibleConverter<T?, string?>` for MudBlazor v9 |

---

## License

MIT License - see [LICENSE.txt](LICENSE.txt)
