# Value objects in forms and model binding

How Zonit value objects behave inside a Blazor `EditForm`, ASP.NET Core model binding and
`IConfiguration` binding. Nothing here needs registering — the behaviour comes from `[TypeConverter]`
attributes on the types themselves.

## Read this first

- **`<InputText @bind-Value="Model.Title" />` does not compile** when `Title` is a value object.
  `InputText` is `InputBase<string?>` and is not generic. See the Blazor section for what to write
  instead.
- **Do not put `[StringLength]`, `[Url]` or `[Required]` on a value-object property.** They are typed
  for `string`/reference types and either throw or silently always pass.
- **A `TypeConverter` is not a validator.** It converts, and throws `FormatException` when it cannot.
  Only a component derived from `InputBase<T>` turns that into a field-level message.
- **`Price` and `Money` have no `TypeConverter`.** Bind them as `decimal` with `InputNumber`.
- **`Asset` cannot be converted from a string at all** — use `InputFile`.

## The converter matrix

### 1. Validating converters — `ValueObjectTypeConverter<T>`

These types carry `[TypeConverter(typeof(ValueObjectTypeConverter<T>))]`. The converter reflects for a
`public static bool TryCreate(string?, out T)` and for public static `MinLength`/`MaxLength` fields, and
builds its error message from them.

| type | `MinLength` | `MaxLength` | message on rejected input |
|---|---|---|---|
| `Title` | 1 | 60 | `Title cannot exceed 60 characters.` |
| `Description` | 1 | 160 | `Description cannot exceed 160 characters.` |
| `Permission` | 1 | 200 | `Permission cannot exceed 200 characters.` / `Permission is invalid.` |
| `Role` | 1 | 64 | `Role cannot exceed 64 characters.` / `Role is invalid.` |
| `Credential` | 3 | 254 | `Credential must be at least 3 characters long.` |
| `Currency` | 2 | 10 | `Currency cannot exceed 10 characters.` / `Currency is invalid.` |
| `Zone` | — | 64 | `Zone is invalid.` |
| `UrlPath` | — | 2048 | `UrlPath is invalid.` |
| `Culture` | — | — | `Culture is invalid.` |
| `Content` | — | — | `Content is required.` (blank only) |
| `UrlSlug` | — | — | **never fails** |
| `Url` | — | — | **never fails** |

The message ladder is: blank → `"X is required."`; **both** constants present and the trimmed length out
of range → the specific length message; otherwise → `"X is invalid."`. `Zone` and `UrlPath` reject
bad input correctly but cannot say why, because they declare `MaxLength` without a matching `MinLength`.

### 2. Converters that can never fail

`UrlSlug`, `Url` and `Content` accept effectively any non-blank string, so conversion never produces an
error and the model silently holds a useless value:

```csharp
UrlSlug.TryCreate("!!!", out var slug);   // true, but slug.HasValue is false
Url u = "total nonsense";                 // HasValue true, IsAbsolute false
```

**Do not reach for a string DataAnnotation to fix this.** The built-in attributes are typed for `string`
and misbehave on a value-object property:

| attribute on a VO property | what happens |
|---|---|
| `[StringLength(n)]` | `InvalidCastException: Unable to cast object of type 'Zonit.Extensions.UrlSlug' to type 'System.String'` — thrown out of `Validator.TryValidateObject` at runtime |
| `[Url]` | **always invalid** — `UrlAttribute` tests `value as string`, which is null for a struct |
| `[Required]` | **always valid** — a struct is never null |
| `[Range]`, `[RegularExpression]` | the same class of problem |

Validate the value object explicitly instead:

```csharp
private static string? Validate(PageModel model)
{
    if (!model.Slug.HasValue)
        return "Slug contains no usable characters.";

    if (model.Website.HasValue && !model.Website.IsAbsolute)
        return "Website must be an absolute URL.";

    return null;
}
```

For a reusable rule, write a `ValidationAttribute` that understands the type rather than casting:

```csharp
public sealed class HasValueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value switch
    {
        UrlSlug s => s.HasValue,
        Title t   => t.HasValue,
        Url u     => u.IsAbsolute,
        _         => value is not null,
    };
}
```

### 3. Dedicated converters

| type | converter | converts from | converts to |
|---|---|---|---|
| `Identity` | `IdentityTypeConverter` | `string` (Guid format), `Guid` | `string`, `Guid` |
| `Organization` | `OrganizationTypeConverter` | `string` (Guid), `Guid` | `string`, `Guid` |
| `Project` | `ProjectTypeConverter` | `string` (Guid), `Guid` | `string`, `Guid` |
| `Color` | `ColorTypeConverter` | `string` (hex / rgb / hsl / oklch) | `string` (`CssOklch`) |
| `FileSize` | `FileSizeTypeConverter` | `string` (`"1.5 MB"`), `long`, `int` | `string`, `long` |
| `Schedule` | `ScheduleTypeConverter` | `byte[]`, cron-like `string` | `byte[]`, `string` |
| `Asset` | `AssetTypeConverter` | `byte[]`, `Stream`, `MemoryStream` | `byte[]`, `Stream` |

`IdentityTypeConverter` produces an **Id-only** identity — it never hydrates `Name`/`Roles`. Blank input
maps to `Identity.Empty`; a non-Guid string throws `FormatException`.

`ColorTypeConverter` maps blank to `Color.Transparent` and throws `FormatException` for an unparseable
string. Its `ConvertTo(string)` emits `CssOklch`, which the converter cannot read back to the same colour
— bind the hex form when the value round-trips through the UI.

`ScheduleTypeConverter`'s `ConvertTo(string)` emits the human-readable `ToString()`
(`"Hour=15, Minute=0"`), which `ConvertFrom` cannot parse. Round-trip a `Schedule` through `byte[]`.

All of the above are reachable from Blazor's `BindConverter`, which falls back to
`TypeDescriptor.GetConverter(typeof(T))` for any type it does not handle natively. That is why
`<input @bind="…" />` and `<InputSelect @bind-Value="…" />` work on a value object. Note that
`BindConverter.TryConvertTo<T>` **propagates** the converter's `FormatException` instead of returning
`false`, so keep the candidate values valid (a fixed `<option>` list, an already-validated string).

## Blazor: what actually compiles

| markup | value-object property | result |
|---|---|---|
| `<InputText @bind-Value="…" />` | any VO | **compile error** (CS1503 / CS1662) |
| `<InputTextArea @bind-Value="…" />` | any VO | **compile error** |
| `<input @bind="…" />` | any VO with a `TypeConverter` | compiles; converts; **no validation message** |
| `<InputSelect @bind-Value="…" />` | any VO with a `TypeConverter` | compiles (`InputSelect<TValue>` is generic) |
| `<InputNumber @bind-Value="…" />` | `decimal` property | compiles — the `Price`/`Money` route |
| `<ValidationMessage For="() => …" />` | any VO | compiles |
| custom `InputBase<T>` component | any VO | compiles; **full validation** |

### Plain `@bind` — works, but silently

```razor
<input @bind="Model.Title" />
```

Round-trips correctly through `ValueObjectTypeConverter<Title>`. But `<input>` is not an `InputBase`, so
it never registers a field with the `EditContext`: a rejected value produces no `ValidationMessage`, only
a `FormatException` inside the binder. Use it for a display-only or already-validated field, not for
input you need to validate.

### The validated pattern: a small `InputBase<T>` component

Reach for this when a value-object field needs a real error message. `InputTitle.razor`:

```razor
@using System.Diagnostics.CodeAnalysis
@inherits InputBase<Title>

<input class="@CssClass" value="@CurrentValueAsString"
       @onchange="e => CurrentValueAsString = e.Value?.ToString()" />

@code {
    protected override string? FormatValueAsString(Title value) => value.Value;

    protected override bool TryParseValueFromString(
        string? value,
        [MaybeNullWhen(false)] out Title result,
        [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (Title.TryCreate(value, out result))
        {
            validationErrorMessage = null;
            return true;
        }

        result = Title.Empty;
        validationErrorMessage = $"{DisplayName ?? FieldIdentifier.FieldName} must be 1-{Title.MaxLength} characters.";
        return false;
    }
}
```

```razor
<EditForm Model="Model" OnValidSubmit="SaveAsync">
    <DataAnnotationsValidator />
    <InputTitle @bind-Value="Model.Title" DisplayName="Title" />
    <ValidationMessage For="() => Model.Title" />
</EditForm>
```

`TryParseValueFromString` calls `TryCreate`, so no constructor ever throws and the message comes from the
value object's own `MaxLength`.

### The simple alternative: bind a string, convert at the boundary

If a custom component is more than the field is worth, keep the edit model in `string`:

```csharp
public sealed class ArticleModel
{
    // Note the qualification: inside a class with a property named `Title`, the bare name
    // `Title` binds to that property, not to the value-object type -> CS0120.
    [Required, StringLength(Zonit.Extensions.Title.MaxLength,
                            MinimumLength = Zonit.Extensions.Title.MinLength)]
    public string Title { get; set; } = "";

    [Required, StringLength(Zonit.Extensions.Description.MaxLength)]
    public string Description { get; set; } = "";
}
```

```razor
<InputText @bind-Value="Model.Title" />
<ValidationMessage For="() => Model.Title" />
```

```csharp
// safe: validation already passed
var article = new Article { Title = new Zonit.Extensions.Title(Model.Title) };
```

`MaxLength` and `MinLength` are `const`, so they are legal in an attribute argument — the rule still
lives in exactly one place.

### `Price` and `Money`: bind as decimal

Neither type carries a `[TypeConverter]`, so string binding does nothing useful. Both convert implicitly
to and from `decimal`, which is exactly what `InputNumber<decimal>` wants:

```csharp
public sealed class ProductModel
{
    [Range(0, 1_000_000)]
    public decimal Price { get; set; }          // bind this
    public Currency Currency { get; set; } = Currency.PLN;
}
```

```razor
<InputNumber @bind-Value="Model.Price" step="0.01" />
<InputSelect @bind-Value="Model.Currency">
    @foreach (var c in Currency.GetKnownCurrencies())
    {
        <option value="@c.Code">@c.ToDisplayString()</option>
    }
</InputSelect>
```

```csharp
var product = new Product { Price = new Price(Model.Price), Currency = Model.Currency };
```

If you must accept a free-text amount (an import field, a CSV cell), parse it explicitly — `Price.Parse`
and `Money.Parse` accept both `,` and `.` regardless of culture and reject input over 512 characters.
See `.zonit/extensions/core/money.md`.

### `Asset`: use `InputFile`

```razor
<InputFile OnChange="OnFileSelected" accept="image/*" />
```

```csharp
private Asset _file = Asset.Empty;
private string? _error;

private async Task OnFileSelected(InputFileChangeEventArgs e)
{
    await using var stream = e.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);

    if (!Asset.TryCreate(buffer.ToArray(), e.File.Name, out _file))
    {
        _error = "File is empty or exceeds 100 MB.";
        return;
    }

    var result = _file.Validate(AssetValidationOptions.Images());
    _error = result.IsValid ? null : string.Join(" ", result.Errors);
}
```

`buffer.ToArray()` produces a fresh array, which the `Asset` then owns — do not write into it afterwards.
See `.zonit/extensions/core/assets.md`.

## Making your own value object participate

Three requirements, no registration:

```csharp
using System.ComponentModel;
using Zonit.Extensions.Converters;

[TypeConverter(typeof(ValueObjectTypeConverter<Sku>))]
public readonly struct Sku : IEquatable<Sku>
{
    public const int MinLength = 3;      // 1. public static/const, BOTH of them, or you get
    public const int MaxLength = 32;     //    "Sku is invalid." instead of a real message

    public static readonly Sku Empty = default;

    private readonly string? _value;
    public string Value => _value ?? string.Empty;   // 2. a public "Value" property for ConvertTo
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    private Sku(string value) => _value = value;

    // 3. exactly this signature: public static bool TryCreate(string?, out Sku)
    public static bool TryCreate(string? value, out Sku sku)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            sku = Empty;
            return false;
        }

        sku = new Sku(trimmed);
        return true;
    }

    public bool Equals(Sku other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Sku s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;
}
```

The converter looks up `TryCreate` by exact signature, so `TryCreate(ReadOnlySpan<char>, out T)` or an
extra parameter will not be found and `ConvertFrom` throws
`InvalidOperationException: Type Sku does not have a TryCreate method.`

For JSON, add your own `JsonConverter<Sku>` and put `[JsonConverter(typeof(SkuJsonConverter))]` on the
struct — do not reach for `ValueObjectJsonConverterFactory`, which only knows the built-in types.

## Known limitations

- `ValueObjectTypeConverter<T>` resolves `TryCreate`, `Value`, `MinLength` and `MaxLength` **by
  reflection**, guarded with `[DynamicallyAccessedMembers]` on the generic parameter. It works under
  trimming for the types in this package; a custom value object gets the same protection as long as the
  converter is referenced through the `[TypeConverter]` attribute.
- The converter measures length in UTF-16 chars when building the *message*, while `Title` and
  `Description` enforce `MaxLength` in graphemes. For text with combining marks or emoji the two can
  disagree by a character or two; the accept/reject decision always comes from `TryCreate`, so only the
  wording is affected.
- There is no Blazor input component for value objects in this package. `Zonit.Extensions.Website` and
  `Zonit.Extensions.Website.MudBlazor` are where UI components live — see
  `.zonit/extensions/website/pages.md`.
