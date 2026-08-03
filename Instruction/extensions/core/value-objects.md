# Zonit value objects — the creation contract

`Zonit.Extensions` is the foundation package. It **registers nothing in DI**: there is no
`AddZonitExtensions()`, no `UseValueObjects()`, no options class. You reference the package and name
the types. Anything that tells you to call a setup method for this package is wrong.

Every other Zonit.Extensions package depends on this one, so these types are always available.

## Read this first: the three traps

**1. Most VOs are `readonly struct`.** `!= null` compiles and is *always true*. The emptiness test is
`HasValue`, never a null check.

```csharp
Title t = default;
// if (t != null)     // compiles, always true, always wrong
if (t.HasValue) { }   // correct
```

**2. Constructors and implicit string conversion throw. `TryCreate`/`TryParse` do not.**

```csharp
Title bad = userInput;                   // ArgumentException if userInput is 61+ chars
Title.TryCreate(userInput, out var t);   // false, t == Title.Empty
```

Never assign untrusted input through an implicit conversion. `Title x = someString;` is a throw site.

**3. Not every VO is strict.** Several of them swallow bad input and hand you `Empty` instead — see the
table below. Silent `Empty` is its own bug class: you get no exception and no value.

**Naming.** Every value-object name resolves on its own; the one that collides in practice is `Color`,
which is ambiguous with `MudBlazor.Color` in a MudBlazor project — see
`.zonit/extensions/mudblazor/mudblazor.md`.

> **Renamed in 10.0.0-preview.10:** the time-zone value object is `Zone`, not `TimeZone`. The old name
> was ambiguous with `System.TimeZone` (which still exists in .NET 10, and `using System;` is implicit
> via `ImplicitUsings`), so it could not be written unqualified. `Zone` needs no alias.

## Which VOs exist

All of them live in namespace `Zonit.Extensions` — one namespace, no sub-namespaces to import.

| Type | Kind | `MaxLength` | Bad input via ctor | Bad input via implicit `string` |
|---|---|---|---|---|
| `Title` | `readonly struct` | 60 | throws `ArgumentException` | **throws** |
| `Description` | `readonly struct` | 160 | throws `ArgumentException` | **throws** |
| `Content` | `readonly struct` | — (unbounded) | throws only on null/whitespace | `Empty` on blank |
| `UrlSlug` | `readonly struct` | — | never fails | never fails |
| `Url` | `struct` (not readonly) | — | throws `UriFormatException` | → `Empty` only when blank |
| `UrlPath` | `readonly struct` | 2048 | throws `ArgumentException` | → `Empty` |
| `Culture` | `readonly struct` | — | throws `CultureNotFoundException` | → `Empty` |
| `Zone` | `readonly struct` | 64 | throws `ArgumentException` | → `Empty` |
| `Currency` | `readonly struct` | 10 (min 2) | throws `ArgumentException` | → `Empty` |
| `Color` | `readonly struct` | — | clamps, never throws | → `Color.Transparent` |
| `Price` / `Money` | `readonly struct` | — | `Price(-1)` throws | numeric — see `money.md` |
| `FileSize` | `readonly struct` | — | negative throws | n/a (from `long`/`int`) |
| `Asset` (+ nested `FileName`, `MimeType`) | `readonly struct` | 255 each | throws | see `assets.md` |
| `Schedule` | `readonly record struct` | — | out-of-range field throws | see `schedule.md` |
| `Identity`, `Permission`, `Role`, `Credential` | `readonly struct` | — / 200 / 64 / 254 | throws | see `auth-value-objects.md` |
| `Organization`, `Project` | `readonly struct` | — | `Guid.Empty` throws | n/a (from `Guid`) |

`Title`, `Description`, `Permission`, `Role`, `Credential`, `Currency`, `Zone`, `UrlPath` expose a
`public const int MaxLength`. `Content`, `UrlSlug`, `Url`, `Culture`, `Color` **do not** — do not write
`UrlSlug.MaxLength`, it does not compile.

## Empty vs HasValue vs default

For every string-backed VO, `Empty` *is* `default(T)` and reads back as an empty string, never null:

```csharp
Title.Empty == default(Title);   // true
default(Title).Value;            // "" — never null, safe for EF and string.Concat
default(Title).HasValue;         // false
```

`Price` and `Money` wrap a `decimal`, so "nothing" is a number, not a missing string. They expose
`Zero` instead of `Empty` and have no `HasValue` at all:

```csharp
Price.Zero.Value;   // 0m
Money.Zero.Value;   // 0m
```

`FileSize` also uses `Zero`, but it *does* have `HasValue` (true when `Bytes > 0`). `Color`'s
"nothing" is `Color.Transparent`, which equals `default(Color)`.

## The strict path vs the safe path

Three ways in. Pick by whether the input is trusted.

```csharp
// Trusted (constants, seeds, values you already validated) — throws on violation.
var title  = new Title("Quarterly report");
var title2 = Title.Create("Quarterly report");   // identical, just a named factory

// Untrusted (form posts, API bodies, config, imported data) — never throws.
if (!Title.TryCreate(input, out var title3))
    return "Title must be 1-60 characters.";

// IParsable — same behaviour as TryCreate, for generic code and model binding.
Title.TryParse(input, provider: null, out var title4);
var title5 = Title.Parse(input, provider: null);   // FormatException on failure
```

`Title` and `Description` trim and collapse internal whitespace, and measure `MaxLength` in **graphemes**,
not UTF-16 chars — `new Title("ábc").Length` is 3 even when the accent is a combining mark.

### The VOs that never fail (and why that bites)

`UrlSlug` and `Content` cannot reject non-blank input, so `TryCreate` returning `true` proves nothing
about the result:

```csharp
UrlSlug.TryCreate("!!!", out var slug);   // returns TRUE
slug.HasValue;                            // false — every character was stripped
```

`Url` is the same story in a different shape:

```csharp
Url u = "not a url at all";
u.HasValue;     // true  — accepted as a *relative* URI
u.IsAbsolute;   // false
```

The implicit conversion and `Url.TryCreate(string, out Url)` both pass `allowRelative: true`, which
`Uri.TryCreate` accepts for almost any non-blank string. If you need an absolute address, use the strict
constructor or the explicit overload:

```csharp
var link = new Url(input);                                   // UriFormatException unless absolute
Url.TryCreate(input, allowRelative: false, out var link2);   // the non-throwing form
```

`Culture` is looser than it looks: the runtime accepts synthetic names, so `new Culture("zz-ZZ")` and
`new Culture("en_US")` both succeed. Only genuinely malformed input (`"not a culture"`, `"1234"`) throws
`CultureNotFoundException` — and the implicit conversion turns exactly that case into `Culture.Empty`.
Use `Culture.Default` (`en-US`) or `ValueOrDefault` when you need a guaranteed-usable code.

`UrlPath` inverts the usual `TryCreate` convention: **blank input returns `true`** with `UrlPath.Empty`,
because "no path" is a legitimate value. Only a malformed path (too long, or an absolute URL) returns
`false`.

```csharp
UrlPath.TryCreate(null, out var p);                      // true,  p == UrlPath.Empty
UrlPath.TryCreate("https://cdn.example/x", out var p2);  // false — use Url for absolute addresses
```

`Url` is deliberately **not** a `readonly struct` — it lazily caches the parsed `Uri`. Storing one in a
`readonly` field forces a defensive copy on every property read and throws the cache away. Use a normal
field or a local if you read `Scheme`/`Host`/`Path` repeatedly.

## JSON: zero registration

Every VO carries `[JsonConverter(...)]` on the type. `System.Text.Json` picks it up with no options
setup, no `JsonSerializerOptions.Converters.Add`, no source-generator context of your own.

```csharp
JsonSerializer.Serialize(new Title("Hello"));   // "Hello"
JsonSerializer.Serialize(new Price(19.99m));    // 19.99   (a number, not a string)
JsonSerializer.Serialize(Currency.PLN);         // "PLN"
```

Reads are **lenient by design** — a payload you do not control must not throw mid-deserialization:

```csharp
// 200 characters into a 60-character Title:
JsonSerializer.Deserialize<Title>("\"" + new string('x', 200) + "\"").HasValue;   // false, no exception
```

So a round-trip through JSON can silently drop an over-long value. Validate after deserializing when the
field is required.

`ValueObjectJsonConverterFactory` is public but you never need it — every type it covers already carries
its own attribute, so registering the factory changes nothing. **Do not add it to `JsonSerializerOptions`.**

## EF Core mapping

Nothing in this package touches EF Core; you write the conversion.

**`HasConversion` takes an `Expression`, and an expression tree cannot contain an `out var`
declaration — `v => Title.TryCreate(v, out var t) ? t : Title.Empty` does not compile (CS8198).**
Put the lenient read in a static method and call that instead:

```csharp
/// Lenient read-side conversions for EF Core value converters.
public static class VoRead
{
    public static Title    Title(string? v)    => Zonit.Extensions.Title.TryCreate(v, out var x)    ? x : Zonit.Extensions.Title.Empty;
    public static Description Description(string? v) => Zonit.Extensions.Description.TryCreate(v, out var x) ? x : Zonit.Extensions.Description.Empty;
    public static UrlSlug  Slug(string? v)     => UrlSlug.TryCreate(v, out var x)  ? x : UrlSlug.Empty;
    public static Currency Currency(string? v) => Zonit.Extensions.Currency.TryCreate(v, out var x) ? x : Zonit.Extensions.Currency.Empty;
}
```

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // String-backed VOs: convert to string, size the column from the VO's own constant.
    modelBuilder.Entity<Article>()
        .Property(a => a.Title)
        .HasConversion(v => v.Value, v => VoRead.Title(v))
        .HasMaxLength(Title.MaxLength);

    modelBuilder.Entity<Article>()
        .Property(a => a.Description)
        .HasConversion(v => v.Value, v => VoRead.Description(v))
        .HasMaxLength(Description.MaxLength);

    // No MaxLength constant -> pick your own ceiling; the VO will not enforce one.
    modelBuilder.Entity<Article>()
        .Property(a => a.Slug)
        .HasConversion(v => v.Value, v => VoRead.Slug(v))
        .HasMaxLength(200);

    // Guid-backed snapshot VOs: persist the Id only. A ternary is fine in an expression tree.
    modelBuilder.Entity<Article>()
        .Property(a => a.Author)
        .HasConversion(v => v.Id, id => id == Guid.Empty ? Identity.Empty : new Identity(id));

    // Money: decimal(19,8) matches the internal precision.
    modelBuilder.Entity<Article>()
        .Property(a => a.Price)
        .HasConversion(v => v.Value, v => new Price(v))
        .HasPrecision(19, 8);

    // Schedule: fixed-width binary, sized from the static field (never a literal).
    modelBuilder.Entity<Article>()
        .Property(a => a.Schedule)
        .HasConversion(v => v.ToBytes(), v => Schedule.FromBytes(v))
        .HasMaxLength(Schedule.StorageSize);
}
```

Use the **lenient `TryCreate` form on the way back from the database**, not `new Title(v)`. A row written
before a constraint tightened, or by another system, would otherwise throw during materialization and
take the whole query down with it. The `Guid.Empty` check on `Identity` matters for the same reason —
the constructor throws on an empty Guid.

`Identity`, `Organization` and `Project` store only a `Guid`. Their `Name`/`Slug`/`Roles` snapshot is not
persisted and is **not** lazily loaded — after a plain read, `HasSnapshot` is `false`. Hydration is an
explicit, opt-in call in `Zonit.Extensions.Databases`; these VOs perform no I/O of their own.

## Where to go next

| Topic | File |
|---|---|
| `Identity` / `Permission` / `Role` / `Credential` | `.zonit/extensions/core/auth-value-objects.md` |
| `Price` / `Money` / `Currency` | `.zonit/extensions/core/money.md` |
| `Schedule` and its binary format | `.zonit/extensions/core/schedule.md` |
| `Asset` / `FileSize` / `Color` | `.zonit/extensions/core/assets.md` |
| Blazor `EditForm` and model binding | `.zonit/extensions/core/binding.md` |
| `BaseException`, Text / Xml / Reflection utilities | `.zonit/extensions/core/exceptions.md` |

## Known limitations

- **`Color` does not survive its own string round-trip.** `Color.CssOklch` (also what
  `ColorJsonConverter` writes) emits percentage lightness — `oklch(65.31% 0.1347 242.69)` — but
  `Color.TryParse` captures the number *without* the `%` and reads `65.31` as a 0-1 lightness, which
  clamps to 1. `#3498DB` serialized and deserialized comes back `#AAFFFF`. Persist `Color.Hex`, or the
  raw `L`/`C`/`H`/`Alpha` doubles, when the value has to survive storage. More in
  `.zonit/extensions/core/assets.md`.
- **`Culture` validation depends on the host's ICU data**, so `new Culture("zz-ZZ")` succeeds on a normal
  runtime and may not on one built with invariant globalization. Do not rely on the constructor to reject
  a bad language tag — check the result against your own allow-list.
- `Content` has no length ceiling at all. A 1 MB string is a valid `Content`; if you map it to a column,
  set the length yourself.
