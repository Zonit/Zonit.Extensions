# Cultures — translations, active culture and time zone

`Zonit.Extensions.Cultures` (10.0.0-preview.10) owns three things: a **process-wide translation
registry**, **per-scope culture / time-zone state**, and the **render-side facade** that turns a key
into text. It has no ASP.NET dependency — the middleware, the prerender bridge and the `T()` helpers
all live in `Zonit.Extensions.Website`.

## Read this before you write code

| Trap | What actually happens |
| --- | --- |
| Re-registering in a Website host | `AddWebsite()` already calls `AddCulturesExtension()` and `UseWebsite<TApp>()` already installs `CultureMiddleware`. Do not add either again. |
| `<ZonitCulturesExtension />` | **Deleted.** The replacement is one `<WebsiteHydrator />` in `App.razor` (needs `@using Zonit.Extensions.Website.Hydration`), which drives every state bridge including culture. The old name survives only as a `PersistentComponentState` key. |
| `Current.Value == "pl-pl"` | Always `false`. `Culture` canonicalises through `CultureInfo.Name`, so `Current.Value` is `"pl-PL"` while every config value, cookie and URL prefix is lowercase. Compare with `OrdinalIgnoreCase`, or compare the VOs (`Current == (Culture)"pl-pl"` — `Culture.Equals` is already case-insensitive). |
| Translation keys | The key **is the English source string**, and lookup is `StringComparer.Ordinal`. `Translate("save")` does not find a `"Save"` entry. |
| `SetCulture` and number formats | `ICultureManager.SetCulture` moves the scope's culture only. It does **not** touch `CultureInfo.CurrentCulture`, which is what `string.Format` inside `Translate(content, args)` uses. In a Website host the middleware sets both; in a Blazor circuit or a worker, a language switcher changes words but not `{0:N2}`. |
| `AddRange` | Signature is `AddRange(List<Variable>)` — the concrete `List<T>`. An array or `IEnumerable<Variable>` does not compile. |
| `Culture` / `Translation` are structs | `!= null` compiles and is always true. Test `HasValue` (Culture) or `IsEmpty` (Translation). |
| The time-zone type is `Zone` | Renamed from `TimeZone` in 10.0.0-preview.10 — the old name was ambiguous with `System.TimeZone`. The **members** kept their names: `ICultureState.TimeZone`, `SetTimeZone(...)`, `CultureOption.DefaultTimeZone`. |

## Registration

### Website host — nothing to do

```csharp
builder.Services.AddWebsite();                        // already calls AddCulturesExtension()

var app = builder.Build();
app.UseWebsite<App>("/", o => o.AddArea<HomeArea>()); // already installs CultureMiddleware
```

To configure it, call `AddCulturesExtension` with a delegate **anywhere** in `Program.cs` — the
delegate is applied as `PostConfigure`, so it wins regardless of ordering, and every service
registration inside is `TryAdd`, so nothing is duplicated:

```csharp
builder.Services.AddWebsite();
builder.Services.AddCulturesExtension(o =>
{
    o.DefaultCulture    = "pl-pl";
    o.SupportedCultures = ["pl-pl", "en-us"];
});
```

The only cost of the second call is a second binding of the `Culture` configuration section
(one extra `IConfigureOptions` + change-token source, same values). If that bothers you, use
`builder.Services.PostConfigure<CultureOption>(o => …)` instead — do **not** use
`services.Configure<CultureOption>(…)`, which loses to `appsettings.json` when it is registered
before `AddWebsite()`.

### Standalone host (console, worker, WASM client)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCulturesExtension(o =>
{
    o.DefaultCulture            = "pl-pl";
    o.DefaultTimeZone           = "Europe/Warsaw";
    o.SupportedCultures         = ["pl-pl", "en-us"];
    o.TrackMissingTranslations  = builder.Environment.IsDevelopment();
    o.MaxTrackedMissingTranslations = 5000;
});
builder.Services.AddHostedService<TranslationSeed>();
```

There is no middleware and no automatic detection here — culture is set programmatically through
`ICultureManager`.

> `AddCulturesExtension` binds `IOptions<CultureOption>` via `BindConfiguration("Culture")`, so the
> container **must** contain an `IConfiguration`. A bare `new ServiceCollection().AddCulturesExtension()`
> throws `InvalidOperationException: No service for type 'IConfiguration' has been registered` the
> first time anything culture-related is resolved. Use a host builder, or register a configuration
> yourself in unit tests.

### Configuration

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

| `CultureOption` member | Default | Behaviour |
| --- | --- | --- |
| `DefaultCulture` | `"en-US"` | Initial culture of every scope **and** the fallback language of the lookup. Compared `OrdinalIgnoreCase`. An unparseable value degrades to `en-US` instead of throwing. |
| `DefaultTimeZone` | `"Europe/Warsaw"` | IANA or Windows id, or a fixed offset (`"UTC-5"`). Unparseable → `Zone.Utc`. |
| `SupportedCultures` | all 17 built-ins, lowercase | The allow-list. `SetCulture` and the middleware reject anything not in it. Keep entries lowercase — `DetectCultureService` lowercases the URL segment before an **ordinal** `HashSet` lookup, so an uppercase entry is unreachable from URL detection. |
| `TrackMissingTranslations` | `false` | Opt-in recording of unresolved keys. |
| `MaxTrackedMissingTranslations` | `1000` (`MissingTranslationRepository.DefaultCapacity`) | Hard ceiling on distinct recorded keys. `<= 0` throws `ArgumentOutOfRangeException` on first resolve. |

## Lifetimes

| Service | Lifetime | Notes |
| --- | --- | --- |
| `TranslationRepository` | Singleton | The live registry. Process-wide, `ConcurrentDictionary`, ordinal keys. |
| `DefaultTranslationRepository` | Singleton | Registered but never read by the framework. Ignore it. |
| `MissingTranslationRepository` | Singleton | Bounded diagnostic buffer, see below. |
| `ITranslationManager` | Singleton | Safe to inject into an `IHostedService`. |
| `ILanguageProvider` | Singleton | Frozen registry of the 17 built-ins. |
| `DetectCultureService` | Singleton | Snapshots `SupportedCultures` **at construction** — later option changes do not affect URL detection. |
| `ICultureState` / `ICultureManager` | Scoped | The **same object** under both contracts, so a writer and a reader in one request/circuit share state and `OnChange`. |
| `ICultureProvider` | Scoped | Subscribes to the state's `OnChange` and re-emits it. |

## Registering translations

The registry is a **process-wide singleton keyed by the source string**. Seed it once at startup from
a hosted service — never from a component (`OnInitialized` runs per circuit and would re-add the same
keys on every page load).

```csharp
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Cultures.Models;

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
            new("Save", [new() { Culture = "pl-pl", Content = "Zapisz" }]),
        });

        // Third ctor argument is a description for your own tooling — nothing reads it.
        translations.Add(new Variable(
            "Delete",
            [new Translate { Culture = "pl-pl", Content = "Usuń" }],
            "Destructive action on the row toolbar"));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Rules that bite:

- **You do not register the source language.** A key with no rendition falls through to the input
  string, so `"Save"` renders as `Save` in `en-us` without any entry.
- `Add` / `AddRange` **overwrite by `Variable.Name`** — last write wins for the whole key, not per
  culture. Two seeders touching the same key clobber each other; merge them, or use
  `Variable.AddTranslate` on a fetched instance.
- Inside a `Variable`, culture matching **is** `OrdinalIgnoreCase`, and `AddTranslate` replaces an
  existing rendition for the same culture rather than appending a duplicate.
- `Variable` is thread-safe (`ImmutableArray` + CAS), so seeding from several modules concurrently is
  fine.

## Using translations

In a page deriving from `PageBase` (or `PageViewBase<T>` / `PageEditBase<T>`), three helpers come
from `ExtensionsBase` — no `@inject` needed:

```razor
@page "/orders"
@inherits PageBase

<h1>@T("Orders")</h1>
<p>@T("Showing {0} of {1}", _shown, _total)</p>

<MyGrid Title="@T("Recent")" />

@* raw HTML — only for content you control *@
@TM("Read the <b>terms</b>")

@code {
    private int _shown;
    private int _total;

    private bool HasLabel()
    {
        Translation label = Translate("Save");
        return !label.IsEmpty;
    }
}
```

| Helper | Returns | Use for |
| --- | --- | --- |
| `T(content, args)` | `string` | Component parameters, attributes, plain text. |
| `TM(content, args)` | `MarkupString` | Translations that contain markup. Bypasses Blazor encoding. |
| `Translate(content, args)` | `Translation` | When you need `IsEmpty` / equality. |

Anywhere else — a component that does not derive from `PageBase`, a service, a repository — inject
the provider directly:

```razor
@inject ICultureProvider Culture

<h1>@Culture.Translate("Orders")</h1>
<p>@(Culture.ClientTimeZone(CreatedUtc).ToString(Culture.DateTimeFormat.ShortDatePattern))</p>

@code {
    [Parameter] public DateTime CreatedUtc { get; set; }
}
```

```csharp
internal sealed class Greeter(ICultureProvider culture, ICultureState state)
{
    public string Greet(string name) => culture.Translate("Hello, {0}!", name).Value;

    public DateTime ToUserClock(DateTime utc) => culture.ClientTimeZone(utc);

    public bool IsPolish =>
        string.Equals(state.Current.Value, "pl-pl", StringComparison.OrdinalIgnoreCase);
}
```

`ICultureProvider` exposes only `Current`, `DateTimeFormat`, `Translate`, `ClientTimeZone` and
`OnChange`. For `TimeZone` (a `Zone`) and `Supported`, inject `ICultureState`. To render a `Translation` as raw
HTML outside a `PageBase`, use `translation.ToMarkup()` (extension in `Zonit.Extensions.Website`).

## Lookup and fallback chain

`Translate(content, args)` runs exactly this, per call:

1. Empty / whitespace input → `Translation.Empty`, immediately.
2. Look `content` up in `TranslationRepository` (ordinal, O(1)), then scan that key's renditions for
   the **scope's current culture** (`OrdinalIgnoreCase`).
3. Miss, and the current culture is not the configured default → retry against
   `CultureOption.DefaultCulture`. *(This honours your configured default; earlier previews hardcoded
   `en-US` here, which silently disabled the fallback for non-English apps.)*
4. Still a miss → record it (if tracking is on) and return `content` verbatim.
5. If `args` is non-empty, the result goes through
   `string.Format(CultureInfo.CurrentCulture, …)`. A malformed format string is swallowed —
   `Translate("Hi {0", x)` returns `"Hi {0"` rather than throwing.

`ClientTimeZone(utc)` converts through the scope's `ICultureState.TimeZone` — a `Zone` value object,
so named zones *and* fixed offsets both work. It returns the input unchanged when the zone is empty or
unknown, and yields a `DateTimeKind.Unspecified` result.

## Switching culture and time zone

```razor
@inject ICultureManager Manager
@inject ICultureState State

@foreach (var language in State.Supported)
{
    <button @onclick="@(() => Manager.SetCulture(language.Code))">
        @((MarkupString)language.IconFlag) @language.EnglishName
    </button>
}
```

```csharp
manager.SetCulture("pl-pl");                 // implicit string → Culture
manager.SetTimeZone("America/New_York");     // IANA or Windows id
manager.SetTimeZone(new Zone(-5));           // fixed offset, UTC-5
manager.SetTimeZone(Zone.Empty);             // back to CultureOption.DefaultTimeZone
```

The type is `Zonit.Extensions.Zone`; the **member** is still called `SetTimeZone`, and the read side is
still `ICultureState.TimeZone`. Only the type was renamed (it was `TimeZone` up to preview.9, which
could not be written unqualified because `System.TimeZone` still exists in .NET 10). No alias is needed
now — `Zone` is unambiguous in a plain `using Zonit.Extensions;` file and in `_Imports.razor`.

- Neither setter throws. `SetCulture` with a tag outside `SupportedCultures` — or with
  `Culture.Empty` — **silently** falls back to `DefaultCulture`. If you need to know whether the
  switch took, re-read `Current`.
- `OnChange` fires only when the value actually changed; `ICultureProvider` re-emits it, and
  `ExtensionsBase` subscribes the first time the page touches `T()` / `Culture`, so a `PageBase` page
  re-renders on its own.
- `ICultureState.Supported` is `ImmutableArray<LanguageModel>`, one entry per configured tag, built
  through `ILanguageProvider.GetByCode`. That lookup **never fails**: an unknown tag silently yields
  the English model, so `SupportedCultures = ["pl-pl", "uk-ua"]` renders a picker with two entries
  where the second says "English". Only the 17 built-ins are real:
  `ar-sa cs-cz da-dk de-de en-us es-es fi-fi fr-fr hu-hu it-it nl-nl no-no pl-pl pt-pt ru-ru sk-sk sv-se`.
- `LanguageModel.NativeName` falls back to `EnglishName` on every built-in (`"Polish"`, not
  `"polski"`), and `IsRightToLeft` is `false` even for `ar-sa`. If you want a real endonym, use the
  `Culture` VO: `((Culture)"pl-pl").NativeName` → `"polski (Polska)"`.
- `LanguageModel.IconFlag` is an inline `<svg>` string — wrap it in `(MarkupString)`.

### How the request culture is chosen (Website hosts)

`CultureMiddleware` runs before `UseRouting` and resolves, in order:

1. **URL prefix** — `/pl-pl/orders` or `/pl/orders`. The bare subtag folds to the first supported
   regional tag; an unsupported regional flavour (`/en-gb/…` when only `en-us` is supported) does
   **not** fold and simply falls through. On a match the path is rewritten to `/orders` before
   routing, so your `@page` templates never contain the prefix.
2. **`Culture` cookie**, when its value canonicalises into `SupportedCultures`.
3. **`Accept-Language`**, first entry only (no quality-factor negotiation).
4. `CultureOption.DefaultCulture`, else `"en-us"`.

The resolved value is pushed into `CultureInfo.CurrentCulture`, `CurrentUICulture` and the scoped
`ICultureManager`, and written back to the `Culture` cookie (1 year, `SameSite=Lax`,
`IsEssential=true`, `HttpOnly=false` so a JS switcher can read it) **only when it differs** from what
the browser sent. Static-asset and framework requests are skipped entirely.

## Missing-translation report

Recording is off by default and bounded, because the keys are whatever string reached `Translate` —
user names, exception messages, per-row labels — in a process-wide singleton.

```csharp
services.AddCulturesExtension(o =>
{
    o.TrackMissingTranslations      = env.IsDevelopment();
    o.MaxTrackedMissingTranslations = 5000;
});
```

```csharp
internal sealed class MissingReport(
    MissingTranslationRepository missing,
    ILogger<MissingReport> logger)
{
    public void Flush()
    {
        foreach (var variable in missing.GetAll())
            logger.LogWarning("Untranslated key: {Key}", variable.Name);

        if (missing.IsFull)
            logger.LogWarning(
                "Missing-key buffer is full at {Capacity} entries; the report is a truncated sample.",
                missing.Capacity);

        missing.Clear();   // without this the ceiling is hit once and every later miss is dropped
    }
}
```

Misses whose active culture **is** the configured default are not recorded (there, the source string
is the translation). Nothing in the framework reads this repository — it exists for your tooling.

## Known limitations

- **Culture does not survive prerender → circuit in a trimmed app.** `CultureStateBridge` is gated on
  `JsonSerializer.IsReflectionEnabledByDefault`, which the SDK turns off for **any** `PublishTrimmed`
  publish — not just `PublishAot`. When it is false, both `Restore` and the persist callback return
  silently, with **no log line**, so the interactive render starts on `DefaultCulture` while the SSR
  pass rendered correctly. Setting
  `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>`
  restores the bridge — the trimmer substitutes that property into the IL, so a clean publish is
  required for the change to take effect. The persisted payload is a plain `string`, so re-enabling
  it costs nothing at runtime. The same gate disables the Identity, workspace, catalog and cookie
  bridges; see `.zonit/extensions/website/hydration.md`.
- **You cannot add a language.** `LanguageService`'s registry is a `private static FrozenDictionary`
  with no registration hook. Adding a tag to `SupportedCultures` gets you translations, but its
  `LanguageModel` silently resolves to English. To fix the picker you must replace the whole
  `ILanguageProvider` — register your own **before** `AddCulturesExtension()` / `AddWebsite()`, since
  the framework uses `TryAddSingleton`.
- **`Culture` accepts more than you expect.** `CultureInfo` on ICU builds happily constructs
  unregistered tags, so `Culture.TryCreate("zz")` returns `true`. `SupportedCultures` is the real
  gate; a typo in `DefaultCulture` is not caught at startup.
- **The missing-key ceiling is soft.** The capacity check is deliberately lock-free, so N concurrent
  writers can overshoot by up to N-1 entries. It bounds memory; it is not an exact count.

## Where the rest lives

- `.zonit/extensions/website/hosting.md` — `AddWebsite` / `UseWebsite<TApp>`, middleware order.
- `.zonit/extensions/website/hydration.md` — `<WebsiteHydrator />` and the state bridges.
- `.zonit/extensions/website/pages.md` — `PageBase` and the `T()` / `TM()` helpers in context.
- `.zonit/extensions/core/value-objects.md` — the `Culture` and `Zone` value objects.
