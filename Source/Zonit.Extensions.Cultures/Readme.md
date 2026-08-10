# Zonit.Extensions.Cultures

Per-scope culture / time-zone state and a process-wide translation registry. Framework-agnostic: this
package has **no ASP.NET Core dependency** — it references only `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Options.ConfigurationExtensions` and [Zonit.Extensions](../Zonit.Extensions/Readme.md)
(for the `Culture` and `Zone` value objects). It ships its own `Translation` value object.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Cultures.svg)](https://www.nuget.org/packages/Zonit.Extensions.Cultures/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Cultures.svg)](https://www.nuget.org/packages/Zonit.Extensions.Cultures/)

```bash
dotnet add package Zonit.Extensions.Cultures
```

## What you get

- **`ICultureState`** (scoped, read) — `Culture Current`, `Zone TimeZone`, `ImmutableArray<LanguageModel> Supported`, `event Action? OnChange`.
- **`ICultureManager : ICultureState`** (scoped, write) — `void SetCulture(Culture culture)` and `void SetTimeZone(Zone timeZone)`. Both accept plain strings through the value objects' implicit conversions; an unrecognised value **silently** falls back to the configured default instead of throwing.
- **`ICultureProvider`** (scoped, render side) — `Translation Translate(string content, params object?[] args)`, `DateTime ClientTimeZone(DateTime utcDateTime)`, `Culture Current`, `DateTimeFormatModel DateTimeFormat`, `event Action? OnChange`.
- **`ITranslationManager`** (singleton) — `void Add(Variable item)` and `void AddRange(List<Variable> items)`, writing the process-wide registry.
- **`ILanguageProvider`** (singleton) — 25 built-in `LanguageModel`s, O(1) exact lookup with a primary-subtag fallback (`en-gb` → `en-us`) and a hard fallback to `en-us`.
- **`DetectCultureService`** (singleton) — `HttpContext`-free parser: `GetUrl("/pl-pl/home")` → `PathCulture(Url: "home", Culture: "pl-pl")`, including the `/pl/` → `pl-pl` subtag fold.

The ASP.NET middleware that consumes all of this (URL → cookie → `Accept-Language` → default, plus a
one-year `Culture` cookie written only when the value changes) lives in **Zonit.Extensions.Website**
and is installed automatically by `app.UseWebsite<TApp>(...)`.

## Setup

Options are bound from the configuration section named `Culture`:

```json
{
  "Culture": {
    "DefaultCulture": "pl-pl",
    "DefaultTimeZone": "Europe/Warsaw",
    "SupportedCultures": [ "pl-pl", "en-us" ],
    "TrackMissingTranslations": true,
    "MaxTrackedMissingTranslations": 5000
  }
}
```

```csharp
builder.Services.AddCulturesExtension(o =>
{
    o.DefaultCulture    = "pl-pl";
    o.SupportedCultures = ["pl-pl", "en-us"];
});
```

The optional delegate is applied with `PostConfigure`, so **anything you set in code wins over
`appsettings.json`**, regardless of registration order. `IConfiguration` is optional: without one in
the container you get the defaults plus whatever the delegate sets, so a bare
`new ServiceCollection().AddCulturesExtension()` works in console apps and unit tests.

`SupportedCultures` in configuration **replaces** the built-in list instead of extending it — the
JSON above leaves exactly `pl-pl` and `en-us`, not those two appended to the 17 defaults. Omit the
key (or give it `[]`) to keep all 17. Narrowing the list does not adjust `DefaultCulture`: leave a
default outside the list and every request silently falls back to `en-us`.

Using **Zonit.Extensions.Website**? `AddWebsite()` already calls `AddCulturesExtension()` and
`UseWebsite<TApp>()` already installs the culture middleware and the prerender → circuit state bridge.
Call `AddCulturesExtension(o => …)` only to configure it, and do not add any middleware or bridge
component yourself — the old `<ZonitCulturesExtension />` component no longer exists, and the single
`<WebsiteHydrator />` in `App.razor` covers culture along with every other piece of scoped state.

## Loading translations

Keys are the source strings themselves and the registry is a **process-wide singleton**, so load it
once at startup — from an `IHostedService`, never from a component:

```csharp
internal sealed class TranslationSeed(ITranslationManager translations) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        translations.AddRange(new List<Variable>
        {
            new("Hello, {0}!",
            [
                new() { Culture = "pl-pl", Content = "Cześć, {0}!" },
                new() { Culture = "de-de", Content = "Hallo, {0}!" },
            ]),
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

`AddRange` takes a concrete `List<Variable>` — an array does not compile. Keys are compared with
`StringComparer.Ordinal` (case-sensitive); culture tags inside a `Variable` are compared
case-insensitively. `Add`/`AddRange` overwrite by `Variable.Name`, last write wins.

## Translating

```csharp
Translation label = culture.Translate("Hello, {0}!", name);
string text = label;                 // implicit conversion
DateTime local = culture.ClientTimeZone(order.CreatedUtc);
```

`Translate` looks the key up under the scope's current culture, then under
`CultureOption.DefaultCulture`, and finally returns the input verbatim. `Translation` is a readonly
struct with `Value` (never null), `IsEmpty`, `IsNullOrWhiteSpace`, `Translation.Empty` and implicit
conversions both ways to `string`.

In Blazor components deriving from `Zonit.Extensions.Website.PageBase` (or any `ExtensionsBase`
descendant) use the `T(key, args)` → `string`, `TM(key, args)` → `MarkupString` and
`Translate(key, args)` → `Translation` helpers instead of injecting the provider; those live in
**Zonit.Extensions.Website** and forward to `ICultureProvider.Translate`.

## Switching culture

```csharp
manager.SetCulture("pl-pl");                // must be in SupportedCultures, else silently ignored
manager.SetTimeZone("America/New_York");    // IANA / Windows id, or a fixed offset like "UTC-5"
manager.SetTimeZone(new Zone(-5));          // fixed offset, UTC-5
manager.SetTimeZone(Zone.Empty);            // back to CultureOption.DefaultTimeZone
```

> **The time-zone type is `Zonit.Extensions.Zone`, renamed from `TimeZone` in 10.0.0-preview.10** because
> `System.TimeZone` still exists in .NET 10 and `using System;` is implicit, so the old name could not be
> written unqualified. Member names are unchanged: `ICultureState.TimeZone`, `SetTimeZone(...)` and
> `CultureOption.DefaultTimeZone` all keep their spelling. No alias is needed for `Zone`.

Both raise `ICultureState.OnChange` only when the value actually changes; `ICultureProvider` re-emits
it, and `ExtensionsBase` already subscribes so pages re-render on their own.

> `Culture` canonicalises through `CultureInfo.Name`, so `Current.Value` is `"pl-PL"` while
> `SupportedCultures`, the cookie and the URL prefix are lowercase. Compare with
> `StringComparison.OrdinalIgnoreCase`, or compare the value objects (`Culture.Equals` is already
> case-insensitive).

## Missing translations

Recording unresolved keys is **opt-in and capped**, because the keys are arbitrary `Translate(...)`
arguments:

```csharp
services.AddCulturesExtension(o =>
{
    o.TrackMissingTranslations      = env.IsDevelopment();
    o.MaxTrackedMissingTranslations = 5000;   // default 1000
});
```

Nothing in the framework reads the result — inject `MissingTranslationRepository`, call `GetAll()`,
check `IsFull` to know whether the sample is truncated, and `Clear()` after each flush.

## Built-in languages

`ar-sa cs-cz da-dk de-de en-us es-es fi-fi fr-fr hu-hu it-it nl-nl no-no pl-pl pt-pt ru-ru sk-sk sv-se`

Each is a `LanguageModel` with `Code`, `EnglishName` and an inline SVG `IconFlag`. `NativeName`,
`IsRightToLeft` and `AlternativeCodes` exist as extension points but no built-in populates them
(`NativeName` always equals `EnglishName`, Arabic reports LTR) and `LanguageService` never reads
`AlternativeCodes`. The registry is a private static `FrozenDictionary` with no registration hook: to
add a language, register your own `ILanguageProvider` before `AddCulturesExtension()`.

## Lifetimes

| Registration | Lifetime |
| --- | --- |
| `TranslationRepository`, `DefaultTranslationRepository`, `MissingTranslationRepository` | Singleton |
| `ITranslationManager`, `ILanguageProvider` | Singleton |
| `DetectCultureService` | Singleton — rebuilds its `SupportedCultures` set on configuration reload |
| `ICultureState` / `ICultureManager` | Scoped — one internal instance exposed under both contracts |
| `ICultureProvider` | Scoped |

`DefaultTranslationRepository` is registered but never written to or read by the framework.

## License

MIT.
