# Zonit.Extensions

Lightweight, **framework-agnostic** value-object foundation for the Zonit.Extensions ecosystem. AOT-safe, trim-safe, no external dependencies beyond the BCL.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.svg)](https://www.nuget.org/packages/Zonit.Extensions/)

```bash
dotnet add package Zonit.Extensions
```

## What's inside

| Category | Types |
|---|---|
| **Identity / Auth** | `Identity`, `Credential`, `Permission`, `Role` |
| **Tenancy** | `Organization`, `Project` |
| **Localization** | `Culture`, `Translated` |
| **Money** | `Price`, `Money`, `Currency` |
| **Text** | `Title`, `Description`, `Content`, `UrlSlug`, `Url` |
| **Visual / files** | `Color` (OKLCH), `Asset` (signature-based MIME), `FileSize` |
| **Time** | `Schedule` |

Plus utilities:

- **`Exceptions/`** — Structured exception handling with i18n hooks and strongly-typed parameters.
- **`Reflection/`** — Discovery of assemblies / types implementing a given base.
- **`Xml/`** — Lossless object ↔ XML serialization helper.

## Value object pattern

Every VO follows the same shape so that consumers can treat them uniformly:

- `readonly struct`, `IEquatable<T>`, `IParsable<T>`
- `Empty` static, `HasValue` flag (semantically distinct from `default`)
- Hand-written `JsonConverter` (no reflection, AOT-safe)
- `TypeConverter` for ASP.NET Core model binding / `IConfiguration`
- Implicit conversion to/from primitive (`Guid`, `string`) where it makes sense
- Persist *only* the Id for composite VOs; rehydrate the snapshot via the consumer's database extension

## Highlights

### Permission (wildcard authorization tokens)

```csharp
Permission read   = "orders.read";
Permission writeAll = "orders.*";
bool ok = writeAll.Implies(read);   // true — wildcards expand
```

Used by `Zonit.Extensions.Auth` to drive `[RequirePermission("orders.read")]`.

### Identity (lightweight actor snapshot)

```csharp
var actor = new Identity(
    id: userId,
    name: new Title("Alice"),
    roles: [new Role("admin")],
    permissions: [Permission.Create("orders.*")]);

actor.IsInRole(new Role("admin"));        // true
actor.HasPermission("orders.read");       // true (via implicit string→Permission)
```

Equality by `Id` only — two snapshots referring to the same user compare equal regardless of hydration completeness.

### Credential (auto-detect kind)

```csharp
new Credential("alice@example.com").Kind;        // CredentialKind.Email
new Credential("+48 600 100 200").Kind;          // CredentialKind.Phone
new Credential(Guid.NewGuid()).Kind;             // CredentialKind.Id
new Credential("alice").Kind;                    // CredentialKind.Username
```

### Color (OKLCH)

```csharp
Color c = "#3498db";                  // implicit conversion
c.Lighten(0.1).Hex;                   // "#5BB0E5"
c.Mix(Color.FromHex("#e74c3c"), 0.5); // perceptually uniform blend
```

### Asset (signature-based MIME)

```csharp
using var fs = File.OpenRead("upload.jpg");
Asset a = fs;
a.Signature;   // SignatureType.WebP — file is actually WebP, despite extension
a.MediaType;   // "image/webp"
a.Size.Megabytes;
```

## Persistence (EF Core)

VOs that wrap a `Guid` (`Identity`, `Organization`, `Project`) are designed to persist as a single column. Use a `ValueConverter` to extract the id, and a domain-side `IDatabaseExtension<T>` to rehydrate the snapshot when needed:

```csharp
// store
modelBuilder.Entity<Order>()
    .Property(o => o.Author)
    .HasConversion(v => v.Id, id => new Identity(id));

// hydrate on demand
await repo.Extension(o => o.Author).LoadAsync();
```

Without the explicit `Extension(...)` call, `Author.HasSnapshot` is `false` and only `Author.Id` is populated — VOs do **no implicit I/O** and have no lazy-load surprise.

## See also

- [Zonit.Extensions.Auth](../Zonit.Extensions.Auth/Readme.md) — authorization stack built on `Permission`/`Role`/`Identity`.
- [Zonit.Extensions.Cultures](../Zonit.Extensions.Cultures/Readme.md) — translations and culture state using `Culture`/`Translated`.
- [Zonit.Extensions.Organizations](../Zonit.Extensions.Organizations/Readme.md) — tenant context using `Organization`.
- [Zonit.Extensions.Projects](../Zonit.Extensions.Projects/Readme.md) — project context using `Project`.
- [Zonit.Extensions.Website](../Zonit.Extensions.Website/Readme.md) — Blazor / ASP.NET Core integration.

## License

MIT — see [LICENSE](../../LICENSE.txt).
