# Converters

`ValueObjectTypeConverter<T>` — the generic `TypeConverter` that lets a value object take part in
ASP.NET Core model binding, `IConfiguration` binding and Blazor `EditForm` validation.

Full documentation, including which value objects actually get length validation and which can never
fail, is in [`Instruction/extensions/core/binding.md`](../../../Instruction/extensions/core/binding.md)
(installed into a consumer's repo as `.zonit/extensions/core/binding.md`).

The short version:

- The converter reflects for `public static bool TryCreate(string?, out T)`, a public `Value` property,
  and public static `MinLength` / `MaxLength` fields.
- **Both** length constants must be present for the converter to produce a length-specific error message.
  With only one, or neither, the message is `"<TypeName> is invalid."`.
- A value object whose `TryCreate` cannot fail (`UrlSlug`, `Url`, `Content` beyond the blank check)
  produces no validation error at all. Put an explicit `[StringLength]` on those properties.
- `ConvertFrom` throws `FormatException` on rejection — that is what `EditForm` turns into a field
  message. It does not return `false`.

Types with their own dedicated converter (`Identity`, `Organization`, `Project`, `Color`, `FileSize`,
`Schedule`, `Asset`) do not use this class; see `binding.md` for their conversion matrix.
