# Value objects

The value objects in this folder are documented in one place, and this is not it.

The authoritative docs live under `Instruction/extensions/core/` in this repository. They are packed into
the NuGet package and installed into a consumer's `.zonit/extensions/core/` at build time, so the same
text is what a consumer's AI assistant reads.

| Topic | Repo path | Installed as |
|---|---|---|
| The creation contract: strict ctors vs `TryCreate`, `Empty`/`HasValue`, JSON, EF Core | [`Instruction/extensions/core/value-objects.md`](../../../Instruction/extensions/core/value-objects.md) | `.zonit/extensions/core/value-objects.md` |
| `Identity`, `Permission`, `Role`, `Credential` | [`.../auth-value-objects.md`](../../../Instruction/extensions/core/auth-value-objects.md) | `.zonit/extensions/core/auth-value-objects.md` |
| `Price`, `Money`, `Currency` | [`.../money.md`](../../../Instruction/extensions/core/money.md) | `.zonit/extensions/core/money.md` |
| `Schedule` and its 20-byte binary format | [`.../schedule.md`](../../../Instruction/extensions/core/schedule.md) | `.zonit/extensions/core/schedule.md` |
| `Asset`, `FileSize`, `Color` | [`.../assets.md`](../../../Instruction/extensions/core/assets.md) | `.zonit/extensions/core/assets.md` |
| Blazor `EditForm` and model binding | [`.../binding.md`](../../../Instruction/extensions/core/binding.md) | `.zonit/extensions/core/binding.md` |

Folder layout:

```
ValueObjects/
  Auth/          Identity, Permission, Role, Credential
  Files/         Asset (+ nested FileName / MimeType / SignatureType), FileSize, Color
  Localization/  Culture
  Money/         Price, Money, Currency, NumericInputNormalizer (internal)
  Tenancy/       Organization, Project
  Text/          Title, Description, Content, Url, UrlPath, UrlSlug
  Time/          Schedule, Zone
```

When you change behaviour here, update the matching `Instruction/extensions/core/*.md` in the same
commit — that file is the one consumers actually read.
