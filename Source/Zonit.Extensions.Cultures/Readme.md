# Zonit.Extensions.Cultures

Per-scope culture / time-zone state, translation registry and 17 built-in BCP-47 languages for ASP.NET Core and Blazor. Built on top of the `Culture` and `Translated` value objects from [Zonit.Extensions](../Zonit.Extensions/Readme.md).

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Cultures.svg)](https://www.nuget.org/packages/Zonit.Extensions.Cultures/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Cultures.svg)](https://www.nuget.org/packages/Zonit.Extensions.Cultures/)

```bash
dotnet add package Zonit.Extensions.Cultures
```

## What you get

- **`ICultureState`** — read-only view (`Current : Culture`, `TimeZone`, `Supported`) for renderers.
- **`ICultureManager : ICultureState`** — adds `SetCulture(Culture)` and `SetTimeZone(string)` for switchers.
- **`ICultureProvider`** — `Translate(content, args) : Translated`, `ClientTimeZone(utc)` and `DateTimeFormat`.
- **`ILanguageProvider`** — process-wide registry of 17 languages, O(1) exact lookup + O(1) primary-subtag fallback (so `en-gb` resolves to `en-us`).
- **`TranslationRepository` / `DefaultTranslationRepository` / `MissingTranslationRepository`** — concurrent dictionaries keyed by translation name; missing-key tracking for development.
- ASP.NET Core middleware that resolves the requested culture from URL path / cookie / `Accept-Language` and writes a one-year `Culture` cookie.

## Setup

```csharp
// Program.cs
builder.Services.AddCulturesExtension(o =>
{
    o.DefaultCulture = "en-us";
    o.DefaultTimeZone = "Europe/Warsaw";
    o.SupportedCultures = ["en-us", "pl-pl", "de-de"];
});

var app = builder.Build();
app.UseMiddleware<CultureMiddleware>();
```

For Blazor add the persistence bridge component once (e.g. in `Routes.razor`):

```razor
@using Zonit.Extensions
<ZonitCulturesExtension />
```

## Translating

```razor
@inject ICultureProvider Culture

<h1>@Culture.Translate("Hello, {0}!", User.Name)</h1>
<p>Created at @Culture.ClientTimeZone(order.CreatedAtUtc)
   in @Culture.Current.NativeName</p>
```

`Translate(...)` returns `Translated`, which implicitly converts to `string`, so existing call sites keep working.

## Switching culture

```razor
@inject ICultureManager Manager

<button @onclick="@(() => Manager.SetCulture("pl-pl"))">Polski</button>
```

The change raises `ICultureState.OnChange`, which `ICultureProvider` re-emits — components subscribed to it can re-render automatically (the Website integration's `ExtensionsBase` already handles this).

## Loading translations

```csharp
[Inject] ITranslationManager Translations { get; set; } = null!;

Translations.AddRange(new[]
{
    new Variable("Hello, {0}!", new List<Translate>
    {
        new() { Culture = "pl-pl", Content = "Cześć, {0}!" },
        new() { Culture = "de-de", Content = "Hallo, {0}!" },
    }),
});
```

Backed by a thread-safe `ConcurrentDictionary` keyed by the source string — translation lookups are O(1).

## Built-in languages

`ar-sa cs-cz da-dk nl-nl en-us fi-fi fr-fr de-de hu-hu it-it no-no pl-pl pt-pt ru-ru sk-sk es-es sv-se`

Resolved via `ILanguageProvider.GetByCode(code)`. The lookup falls back to the primary subtag (`en-gb` → `en-us`) and then to `en-us`. Each language is a `LanguageModel` with `Code`, `EnglishName` and an inline SVG `IconFlag`.

## Lifetimes

- Translation repositories — singleton (process-wide).
- `LanguageService` — singleton (immutable).
- `CultureStateService` — scoped, exposed under `ICultureState` / `ICultureManager` / `CultureStateService` pointing to the same instance, so writers and readers in one request observe the same state and `OnChange` notifications.
- `CultureService` (`ICultureProvider`) — scoped, subscribes to the state's `OnChange` and re-emits.

## License

MIT.
