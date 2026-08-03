# Zonit.Extensions

Framework-agnostic value-object foundation for the Zonit.Extensions ecosystem. Trim- and AOT-clean,
no ASP.NET Core dependency, **no DI registration of any kind** — you reference the package and name the
types.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)

```bash
dotnet add package Zonit.Extensions
```

There is no `AddZonitExtensions()`. Anything that tells you to call a setup method for this package is
wrong.

Every other `Zonit.Extensions.*` package depends on this one, so these types are always available once
any of them is installed.

## What's inside

Everything below is in namespace `Zonit.Extensions`.

| Category | Types |
|---|---|
| **Identity / auth** | `Identity`, `Credential`, `Permission`, `Role` |
| **Tenancy** | `Organization`, `Project` |
| **Localization / time** | `Culture`, `Zone` |
| **Money** | `Price`, `Money`, `Currency` |
| **Text** | `Title`, `Description`, `Content`, `UrlSlug`, `Url`, `UrlPath` |
| **Files / visual** | `Asset` (+ nested `FileName`, `MimeType`, `SignatureType`), `FileSize`, `Color` |
| **Time** | `Schedule` |

Plus a few utilities:

- `BaseException` / `BaseException<TErrorCode>` (also in `Zonit.Extensions`) — i18n-ready errors that
  keep key, template and parameters separable.
- `Zonit.Extensions.Text` — word/sentence counters, reading time, readability, whitespace and
  smart-quote normalization.
- `Zonit.Extensions.Xml` — `XmlConvertible`, a culture-invariant object ↔ XML helper for flat types.
- `Zonit.Extensions.Reflection` — `AssemblyProvider`, assembly/type scanning (explicitly **not** AOT-safe;
  both methods carry `[RequiresUnreferencedCode]`).

Sole third-party dependency: [`Diacritics`](https://www.nuget.org/packages/Diacritics), used by
`UrlSlug` to fold accented characters.

## The shape every value object shares

- `readonly struct` (`Url` is a plain `struct` so it can cache its parsed `Uri`; `Schedule` is a
  `readonly record struct`)
- `Empty` static — which *is* `default(T)` — plus a `HasValue` flag. `Price`, `Money` and `FileSize` use
  `Zero` instead; `Color` uses `Transparent`.
- A hand-written `JsonConverter` attached with `[JsonConverter]`: **no registration, no reflection, no
  source-generator context needed on your side.**
- A `TypeConverter` for ASP.NET Core model binding and `IConfiguration` — except `Price` and `Money`,
  which you bind as `decimal`.
- `IParsable<T>` where a string representation makes sense.

### The rule that matters most

Constructors and implicit string conversions **throw**. `TryCreate` / `TryParse` do not.

```csharp
Title t1 = untrustedInput;                    // ArgumentException past 60 characters
Title.TryCreate(untrustedInput, out var t2);  // false, t2 == Title.Empty
```

And because these are structs, `!= null` compiles and is always true. Test `HasValue`.

## Highlights

### Permission — wildcard authorization tokens

```csharp
Permission read     = "orders.read";
Permission writeAll = "orders.*";

writeAll.Implies(read);                              // true
writeAll.Implies(new Permission("orders"));          // true  — trailing * matches zero tokens
writeAll.Implies(new Permission("orders.read.all")); // false — and only one token
```

A trailing `*` matches **zero or one** token, not the whole subtree. Drives
`[RequirePermission("orders.read")]` in `Zonit.Extensions.Auth`.

### Identity — lightweight actor snapshot

```csharp
var actor = new Identity(
    id: userId,
    name: new Title("Alice"),
    roles: [new Role("admin")],
    permissions: [new Permission("orders.*")]);

actor.IsInRole(new Role("admin"));    // true
actor.HasPermission("orders.read");   // true (implicit string -> Permission)
actor.HasSnapshot;                    // true
```

Equality is by `Id` only, so a hydrated snapshot equals the bare `Identity(id)` it came from.

### Credential — kind auto-detected from the value

```csharp
new Credential("alice@example.com").Kind;   // CredentialKind.Email
new Credential("+48 600 100 200").Kind;     // CredentialKind.Phone   (Value "+48600100200")
new Credential("alice").Kind;               // CredentialKind.Username
new Credential(Guid.NewGuid()).Kind;        // CredentialKind.Id
```

Input longer than 254 characters is rejected before any regex runs.

### Money — culture-free parsing

```csharp
var inv = CultureInfo.InvariantCulture;

Price.TryParse("19,99", null, out var a);   // 19.99
Price.TryParse("19.99", null, out var b);   // 19.99  — same value, whatever the host culture
Currency.PLN.Format(19.99m, inv);           // "19.99 zł"   — symbol after
Currency.USD.Format(19.99m, inv);           // "$19.99"     — symbol before
Currency.JPY.Format(1999m, inv);            // "¥1,999"     — 0 decimal digits
```

Parsing is culture-free by design; **formatting is not** — `Format` defaults to
`CultureInfo.CurrentCulture` for the number part, as do `Price.ToString()` and `Money.ToString()`.

### Asset — MIME from the magic bytes, not the file name

```csharp
byte[] bytes = await File.ReadAllBytesAsync("upload.pdf");
var asset = new Asset(bytes, "upload.pdf");

asset.Signature;      // e.g. SignatureType.Png if the bytes are really a PNG
asset.MediaType;      // "image/png" — the content wins over the extension
asset.OriginalName;   // "upload.pdf" — preserved as supplied
asset.Validate(AssetValidationOptions.Documents()).Errors;
// "File content is 'image/png' but the name 'upload.pdf' claims 'application/pdf'."
```

`Asset` **takes ownership of the array you pass in** — it does not copy. Do not mutate that array
afterwards.

### Color — OKLCH

```csharp
Color c = "#3498db";
c.Lighten(0.1).Hex;   // "#58B8FD"
c.CssOklch;           // "oklch(65.31% 0.1347 242.69)"
c.Mix(Color.FromHex("#e74c3c"), 0.5);
```

Persist `Color.Hex`, not `CssOklch` — the OKLCH string does not currently parse back to the same colour.

## Persistence

`Identity`, `Organization` and `Project` persist as a single `Guid` column. Their name/slug/roles
snapshot is **not** stored and is **not** lazily loaded — after a plain read, `HasSnapshot` is `false`.
Hydration is an explicit, opt-in call in `Zonit.Extensions.Databases`; these value objects perform no
I/O of their own.

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Author)
    .HasConversion(v => v.Id, id => id == Guid.Empty ? Identity.Empty : new Identity(id));
```

Note that `HasConversion` takes an `Expression`, so the read side cannot contain an `out var`
declaration. String-backed value objects need a small static helper — the full recipe set is in the
docs below.

## Documentation

The authoritative docs ship inside the package and are installed into a consuming repository's
`.zonit/extensions/core/` (plus `.cursor/rules/`, `.github/instructions/` and `.claude/skills/` when
those editors are detected) at build time:

| File | Covers |
|---|---|
| `value-objects.md` | the creation contract, `Empty`/`HasValue`, JSON, the EF Core recipes |
| `auth-value-objects.md` | `Identity`, `Permission`, `Role`, `Credential` |
| `money.md` | `Price`, `Money`, `Currency` |
| `schedule.md` | `Schedule`, the 20-byte binary format, cron parsing |
| `assets.md` | `Asset`, `FileSize`, `Color` |
| `binding.md` | Blazor `EditForm` and model binding |
| `exceptions.md` | `BaseException`, plus the Text / Xml / Reflection utilities |

In this repository they live under
[`Instruction/extensions/core/`](../../Instruction/extensions/core/). Disable the install with
`<ZonitExtInstructions>false</ZonitExtInstructions>`.

## See also

- [Zonit.Extensions.Auth](../Zonit.Extensions.Auth/Readme.md) — authorization built on `Permission` / `Role` / `Identity`.
- [Zonit.Extensions.Cultures](../Zonit.Extensions.Cultures/Readme.md) — translations and culture state built on `Culture`.
- [Zonit.Extensions.Organizations](../Zonit.Extensions.Organizations/Readme.md) — tenant context built on `Organization`.
- [Zonit.Extensions.Projects](../Zonit.Extensions.Projects/Readme.md) — project context built on `Project`.
- [Zonit.Extensions.Website](../Zonit.Extensions.Website/Readme.md) — Blazor / ASP.NET Core integration.

## License

MIT — see [LICENSE](../../LICENSE.txt).
