# Exceptions, Text, Xml, Reflection

The non-value-object half of `Zonit.Extensions`: a translation-ready exception base, plus three small
utility namespaces. Nothing here is registered in DI. For the value objects themselves see
`.zonit/extensions/core/value-objects.md`.

## BaseException — errors that can be translated later

`Zonit.Extensions.BaseException` is an abstract `Exception` that carries three extra members:

| member | type | purpose |
|---|---|---|
| `ErrorKey` | `string` | stable localization key, e.g. `"Wallets.NotFound"` |
| `Template` | `string` | message template with `{0}`, `{1}` placeholders |
| `Parameters` | `object[]?` | the values to substitute — `null` when none were supplied |

`Message` is the template already formatted with the parameters, so the exception reads correctly in an
English log without any translation layer. `ErrorKey` + `Template` + `Parameters` let a UI re-render the
same error in the user's language later.

**The whole point is the placeholders.** Never interpolate:

```csharp
// WRONG — the message is baked in and can never be translated.
throw new WalletException("Wallets.NotFound", $"Wallet {walletId} was not found");

// RIGHT — key, template and parameters stay separable.
throw new WalletException("Wallets.NotFound", "Wallet {0} was not found", walletId);
```

### The non-generic form

```csharp
using Zonit.Extensions;

public sealed class WalletException : BaseException
{
    public WalletException(string errorKey, string template, params object[] parameters)
        : base(errorKey, template, parameters) { }
}

throw new WalletException("Wallets.NotFound", "Wallet {0} was not found", walletId);
// .Message    -> "Wallet 7f3b… was not found"
// .ErrorKey   -> "Wallets.NotFound"
// .Template   -> "Wallet {0} was not found"
// .Parameters -> [walletId]
```

`FormatTemplate` swallows formatting failures: if the template and the parameters disagree (a `{2}` with
one argument, a stray `{`), the raw template becomes the `Message` rather than a
`FormatException` escaping from a `throw` statement. That is deliberate, and it means a broken template
shows up as a placeholder-looking message rather than a crash.

### The generic form — one exception type per module

`BaseException<TErrorCode>` where `TErrorCode : Enum` derives the key and the template from
`[Display]` on the enum member:

```csharp
using System.ComponentModel.DataAnnotations;
using Zonit.Extensions;

public enum WalletErrorCode
{
    [Display(Name = "Wallets.NotFound", Description = "Wallet {0} was not found")]
    NotFound,

    [Display(Name = "Wallets.InsufficientFunds", Description = "Wallet {0} has {1}, needs {2}")]
    InsufficientFunds,
}

public sealed class WalletException : BaseException<WalletErrorCode>
{
    public WalletException(WalletErrorCode code, params object[] parameters)
        : base(code, parameters) { }
}

throw new WalletException(WalletErrorCode.InsufficientFunds, walletId, balance, required);
```

| source | falls back to |
|---|---|
| `ErrorKey` ← `Display.Name` | `code.ToString()` |
| `Template` ← `Display.Description` | `Display.Name`, then `code.ToString()` |

The `Code` property gives you the enum back, so a handler can `switch` on it instead of parsing strings:

```csharp
catch (WalletException ex) when (ex.Code == WalletErrorCode.NotFound)
{
    return Results.NotFound(new { key = ex.ErrorKey, ex.Message });
}
```

Rendering a translated message is your layer's job — take `Template`, run it through your translation
store keyed by `ErrorKey`, then `string.Format` with `Parameters`.

### Known limitations

- The generic form reads the `[Display]` attribute **reflectively** (`Type.GetField` +
  `GetCustomAttribute`) on every construction. It is not annotated for trimming, so under
  `PublishTrimmed`/`PublishAot` an enum whose metadata was trimmed silently degrades to
  `code.ToString()` for both key and template. Keep your error enums rooted, or use the non-generic
  `BaseException` with literal keys in a trimmed app.
- There is no caching: every `throw` re-reflects. Fine on an error path, not something to do in a loop.
- `BaseException` has no `(string message, Exception inner)` constructor. To wrap a cause, add the
  overload on your own derived type and pass the inner exception through `Exception`'s protected
  surface yourself.

## Zonit.Extensions.Text — counters and analyzers

Two fluent wrappers over a string, reached through the `Text` facade:

```csharp
using Zonit.Extensions.Text;

var counter = Text.Count(body);
counter.Characters; counter.Words; counter.Letters; counter.Numbers;
counter.SpecialChars; counter.Paragraphs; counter.Sentences; counter.AverageWord;

var analyzer = Text.Analyzer(body);
analyzer.ReadingTime;            // TimeSpan, 200 wpm + 0.5 s per punctuation mark
analyzer.ReadabilityScore;       // Flesch reading-ease, clamped to 0-100
analyzer.VocabularyComplexity;   // 0-100 composite
analyzer.CountWordOccurrences(caseSensitive: false);   // Dictionary<string, int>
```

Both derive from `TextBase<T>` and share the cleanup members and the configuration pair. **Every one of
them returns a new instance — none of them mutates the receiver:**

```csharp
Text.Count("a|b|c").Words;                          // 1  — default separators
Text.Count("a|b|c").WithSeparators('|').Words;      // 3  — use the RESULT

var c = Text.Count("a|b|c");
c.WithSeparators('|');                              // discarded — c is unchanged
c.Words;                                            // still 1
```

Configuration survives the cleanup chain, so `.WithSeparators('|').RemoveHtml.Words` keeps the custom
separators. `WithSeparators(null)` throws `ArgumentNullException`; `WithSeparators()` with an empty array
keeps the current set rather than silently falling back to whitespace splitting.

Cleanup members (`RemoveHtml`, `RemoveSpecialChars`, `NormalizeWhitespace`) are properties, not methods.

`TextNormalizer` is a separate static class with both static and extension forms:

```csharp
TextNormalizer.NormalizeWhitespace("a   b");     // "a b"
TextNormalizer.NormalizeSmartQuotes("“hi” don’t"); // "\"hi\" don't"
TextNormalizer.NormalizeHyphensToDash("a–b");     // "a-b"
```

`NormalizeSmartQuotes` folds U+201C/201D/201E/201F to `"` and U+2018/2019/201A/201B to `'`. Note it
rewrites the typographic apostrophe in *don't* as well — do not run it over prose you want to keep
typographically correct.

### Known limitations

- The default separator set is `' ' . ! ? ; : , " \r \n`. A hyphenated or slash-separated word counts as
  one word unless you add the character via `WithSeparators`.
- `ReadabilityScore` uses a vowel-run syllable estimate extended with Polish vowels; it is a rough
  heuristic, not a linguistic measure, and it allocates a non-source-generated `Regex` per call.
- `Paragraphs` counts blank-line runs plus one, so an empty string reports 1 paragraph.

## Zonit.Extensions.Xml — XmlConvertible

A small reflection-based object ↔ XML mapper for flat configuration-shaped types.

```csharp
using System.Xml.Serialization;
using Zonit.Extensions.Xml;

[XmlRoot("invoice")]
public sealed class Invoice : XmlConvertible
{
    public Invoice() { }
    public Invoice(string xml) : base(xml) { }

    [XmlElement("number")] public string Number { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime IssuedAt { get; set; }
}

var xml = new Invoice { Number = "FV/1", Total = 19.99m, IssuedAt = DateTime.UtcNow }.Serialize();
var back = XmlConvertible.FromXml<Invoice>(xml);
```

Output is culture-independent by construction: numbers use the invariant culture, `DateTime` and
`DateTimeOffset` use round-trip `"O"` (so `Kind` survives), `TimeSpan` uses `"c"`, and the declaration
says `utf-8`. Reads parse with `InvariantCulture` and `DateTimeStyles.RoundtripKind`. A document written
on a `pl-PL` host is byte-identical to one written on `en-US`.

`XmlRootAttribute.ElementName` names the root; `XmlElementAttribute.ElementName` names each element.
Element lookup on read is case-insensitive, and unknown elements are skipped.

### Known limitations

- **Flat scalar properties only.** The default `SerializeToXml` writes `WriteElementString(name,
  FormatValue(value))` for every readable+writable public property, so a nested object is emitted as its
  `ToString()` and a collection as `System.Collections.Generic.List\`1[…]`. Nested structures need you to
  override `SerializeToXml`/`DeserializeFromXml`.
- Supported property types on read: `string`, `int`, `long`, `decimal`, `double`, `float`, `bool`,
  `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, and their nullable forms; anything else goes through
  `Convert.ChangeType` and throws `XmlSerializationException` if that fails.
- Reflection-based, so it is **not trim- or AOT-safe** beyond the `[DynamicallyAccessedMembers(
  PublicProperties)]` annotation on the base class. Use `System.Text.Json` source generation if the app
  is published trimmed.
- Properties with no setter are skipped in both directions.

## Zonit.Extensions.Reflection — AssemblyProvider

```csharp
using Zonit.Extensions.Reflection;

IEnumerable<Type> handlers = AssemblyProvider.GetTypes<IHandler>();
IEnumerable<Assembly> owners = AssemblyProvider.GetAssemblies<IHandler>();
```

Both scan `AppDomain.CurrentDomain.GetAssemblies()`, skip assemblies whose full name contains
`Microsoft` or `System.` (pass `includeMicrosoftAssemblies: true` to include them), and return
non-abstract types assignable to `T`. `ReflectionTypeLoadException` is caught and the successfully
loaded types are returned.

### Known limitations

- **Not AOT-safe, and honest about it.** Both methods are marked `[RequiresUnreferencedCode]` and
  `[RequiresDynamicCode]`, so calling them from a trim-enabled project produces IL2026/IL3050 warnings.
  There is no suppression that makes this correct — assembly scanning cannot work after trimming.
- It sees only assemblies **already loaded**. A plugin assembly that nothing has touched yet is invisible;
  load it explicitly first.
- The Microsoft filter is a substring match on the assembly name, so a third-party assembly called
  `Contoso.System.Core` is excluded too.
