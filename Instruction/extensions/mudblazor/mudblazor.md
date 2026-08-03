# MudBlazor components (`Zonit.Extensions.Website.MudBlazor`)

Two unrelated things in one package:

1. **`ZonitTextField<T>` / `ZonitTextArea<T>`** — `MudTextField` subclasses that install a
   compile-time-selected converter for six Zonit value objects.
2. **`PageHeader` / `EmptyState` / `LoadingSpinner`** — three stateless MudBlazor layout
   primitives with no relationship to value objects at all.

They live in **two different namespaces**. Despite the name, the package does **not** reference
`Zonit.Extensions.Website` — its only dependencies are `Zonit.Extensions` (the value objects) and
`MudBlazor`.

## Read this first

**This package registers nothing.** There is no `AddZonitMudBlazor()`, no `IServiceCollection`
extension, no middleware. Nothing in the assembly touches DI. If you go looking for a registration
call you will not find one, and you must not invent one — you wire **MudBlazor's own** services and
providers yourself.

**`Color` is ambiguous.** `Zonit.Extensions.Color` (an OKLCH value object) and `MudBlazor.Color`
(the theme enum) collide the moment both namespaces are imported. Every MudBlazor `Color=` in a file
that also has `@using Zonit.Extensions` must be qualified. See below.

## Setup

```bash
dotnet add package Zonit.Extensions.Website.MudBlazor
```

The package depends on **MudBlazor 9.7.0** (a NuGet floor — a higher 9.x in the graph wins). The
components subclass `MudTextField<T>` and use `IReversibleConverter<,>`, which is the MudBlazor 9
converter API; MudBlazor 8 will not work.

```csharp
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor's own registration. Zonit.Extensions.Website.MudBlazor has no AddXxx() of its own.
builder.Services.AddMudServices();
```

Host layout — the providers and MudBlazor's static assets, again all MudBlazor's, none ours:

```razor
@using MudBlazor

<MudThemeProvider />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />
```

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

`MudBlazor.min.js` is not optional if you use `Copyable` — the copy button calls
`window.mudWindow.copyToClipboard`, which that script defines. It also goes through
`navigator.clipboard`, so it silently does nothing outside a secure context (https or localhost).

Pages using these inputs need an **interactive render mode** (`@rendermode InteractiveServer` or
`InteractiveWebAssembly`). Under static SSR the fields render but nothing converts, validates or
copies.

## The `@using` block

Put these in the consumer's `_Imports.razor` — the package's own `_Imports.razor` is internal to its
compilation and does not flow to you. Importing one Zonit namespace does not give you the other.

```razor
@using Zonit.Extensions                    @* Title, Description, Content, UrlSlug, Url, Culture *@
@using Zonit.Extensions.MudBlazor          @* ZonitTextField<T>, ZonitTextArea<T>                *@
@using Zonit.Extensions.Website.MudBlazor  @* PageHeader, EmptyState, LoadingSpinner             *@
@using MudBlazor                           @* MudButton, Icons, Variant, Typo, …                *@
```

## The `Color` collision

With `@using Zonit.Extensions` and `@using MudBlazor` both in scope, `Color` is a **compile error**,
not a silent shadowing:

```text
error CS0104: 'Color' is an ambiguous reference between 'Zonit.Extensions.Color' and 'MudBlazor.Color'
```

`Color` is the *only* name shared between the public surfaces of the two namespaces (57 public types
in `Zonit.Extensions`, 546 in `MudBlazor`) — `Typo`, `Variant`, `Size`, `Severity`, `Align` and the
rest need no qualification.

Two fixes. Fully-qualify at the use site:

```razor
<MudButton ButtonType="global::MudBlazor.ButtonType.Submit"
           Variant="global::MudBlazor.Variant.Filled"
           Color="global::MudBlazor.Color.Primary">Save</MudButton>
```

…or alias once at the top of the file (or in `_Imports.razor`) and write `Color` normally:

```razor
@using Zonit.Extensions
@using MudBlazor
@using Color = MudBlazor.Color

<MudButton Color="Color.Primary">Save</MudButton>
```

The alias wins over both imports, so the Zonit `Color` value object then needs
`Zonit.Extensions.Color` if you use it in the same file.

## `ZonitTextField<T>` and `ZonitTextArea<T>`

`ZonitTextField<T> : MudTextField<T>` — everything on `MudTextField` (`Label`, `HelperText`,
`Variant`, `Immediate`, `DebounceInterval`, `Required`, `Disabled`, …) is inherited and works as
documented by MudBlazor. `ZonitTextArea<T> : ZonitTextField<T>` adds only a `Lines` default.

```razor
@using Zonit.Extensions
@using Zonit.Extensions.MudBlazor
@using MudBlazor

<EditForm Model="_model" OnValidSubmit="SaveAsync" FormName="article">
    <DataAnnotationsValidator />

    <ZonitTextField @bind-Value="_model.Title"       Label="Title"    Counter="0" />
    <ZonitTextField @bind-Value="_model.Slug"        Label="URL slug" Copyable />
    <ZonitTextField @bind-Value="_model.HomeUrl"     Label="Homepage" OpenNewTab />
    <ZonitTextArea  @bind-Value="_model.Description" Label="Summary"  Counter="0" />
    <ZonitTextArea  @bind-Value="_model.Body"        Label="Body"     Lines="12" />

    <MudButton ButtonType="global::MudBlazor.ButtonType.Submit"
               Variant="global::MudBlazor.Variant.Filled"
               Color="global::MudBlazor.Color.Primary">Save</MudButton>
</EditForm>

@code {
    private readonly ArticleModel _model = new();

    private Task SaveAsync() => Task.CompletedTask;

    public sealed class ArticleModel
    {
        public Title       Title       { get; set; } = "Hello Zonit";
        public UrlSlug     Slug        { get; set; } = "hello-zonit";
        public Url         HomeUrl     { get; set; } = "https://zonit.dev";
        public Description Description { get; set; } = "Short summary.";
        public Content     Body        { get; set; } = "Long body text.";
    }
}
```

**Never write `T="…"`.** `T` is inferred from the `@bind-Value` expression.

### Supported `T`, `MaxLength` and `Counter`

The converter table is a closed `typeof(T)` switch in the constructor. `Nullable<>` forms of each
are supported too.

| Bound `T` | Auto `MaxLength` | `Counter="0"` resolves to | Conversion can fail? |
|---|---|---|---|
| `Title` | `61` (= `Title.MaxLength` + 1) | `60` | yes — length |
| `Description` | `161` (= `Description.MaxLength` + 1) | `160` | yes — length |
| `Content` | MudBlazor default `524288` | `0` (plain char count) | no |
| `UrlSlug` | MudBlazor default `524288` | `0` | no — input is rewritten instead |
| `Url` | MudBlazor default `524288` | `0` | yes — `UriFormatException` |
| `Culture` | MudBlazor default `524288` | `0` | yes — `CultureNotFoundException` |

Two conventions worth knowing:

- **`Counter="0"` means "use the value object's `MaxLength`."** It is rewritten to `60`/`160` for
  `Title`/`Description`. For the other four there is no `MaxLength`, so `0` keeps MudBlazor's own
  meaning: show the current character count with no target.
- **`MaxLength` is set to VO max + 1 on purpose.** The browser stops the user one character *past*
  the limit so the value object throws and the error message actually appears. If it were set to
  the exact max the input would just refuse the keystroke and the user would get no explanation.
  Passing `MaxLength` explicitly overrides this.

### How validation errors surface

The converter catches every exception from the value object's constructor, strips the technical
tail, and rethrows it as MudBlazor's `ConversionException`. MudBlazor puts the result in
`ConversionErrorMessage` and renders it under the field.

```text
Title("…63 chars…")  throws  "Title cannot exceed 60 characters. Current length: 63. (Parameter 'value')"
the field shows        →      "Title cannot exceed 60 characters."
Url("nope")            →      "'nope' is not a valid absolute URL."
```

`ZonitTextField` overrides `UpdateTextPropertyAsync` so **the rejected text stays in the box** while
`ConversionError` is true. The user fixes it in place instead of losing what they typed.

The message text is handed to MudBlazor's localizer as a *key*. With no `MudLocalizer` registered
(or under an `en` UI culture) it renders verbatim in English. Registering a `MudLocalizer` whose
resources contain the English sentence as a key is the only way to translate it.

### Traps

**A cleared field is not an error, and never becomes `null`.** Whitespace-only input converts to the
value object's `Empty` — `Title.Empty`, `Url.Empty`, … — including when you bind `Title?`. The
converter never produces `null`. Consequences:

- `[Required]` on a value-object property never fails: a struct is not `null`, and a nullable-bound
  field is handed `Title.Empty` rather than `null`.
- Test emptiness with `HasValue`, never `!= null`.

```razor
@using Zonit.Extensions
@using Zonit.Extensions.MudBlazor

<ZonitTextField @bind-Value="_model.Subtitle" Label="Subtitle" />

@if (_model.Subtitle?.HasValue == true)
{
    <p>@_model.Subtitle.Value.Value</p>
}

@code {
    private readonly Draft _model = new();

    public sealed class Draft
    {
        // Bound as Title? — after the user clears the box this is Title.Empty, not null.
        public Title? Subtitle { get; set; }
    }
}
```

**The box shows the canonical value, not what was typed.** After a successful conversion the text is
regenerated from the value object. `UrlSlug` rewrites `Hello World!` to `hello-world`, `Title`
collapses runs of whitespace, `Content` trims the ends, and `Url` canonicalises
`  HTTPS://Example.COM/Path  ` to `https://example.com/Path`. With the default
`Immediate="false"` this happens on blur.

**An unsupported `T` fails silently.** `Price`, `Money`, `Currency`, `UrlPath`, `Color`, `FileSize`,
`Zone`, `Schedule`, `Identity`, … compile fine and produce no warning — you simply get
MudBlazor's reflection-based `DefaultConverter<T>` with no value-object validation. The converter
table is `internal` and not extensible; for anything outside the six, use a plain `MudTextField`
with your own converter, or bind a primitive and construct the value object on submit.

```razor
@* Compiles, no diagnostic, no VO validation — DefaultConverter<Price> is used. *@
<ZonitTextField @bind-Value="_price" Label="Price" />

@* Do this instead. *@
<MudTextField T="string" @bind-Value="_raw" Label="Price" />
```

**`ZonitTextArea` cannot be single-line.** `OnInitialized` rewrites `Lines == 1` to `3`, so
`Lines="1"` does not give you a one-line box. Use `ZonitTextField` for that. Any other value
(`Lines="12"`) is respected.

**Deriving from `ZonitTextField<T>`?** It overrides `BuildRenderTree` to refresh the adornment
before rendering. An override that does not call `base.BuildRenderTree(builder)` loses both the
adornment refresh and MudTextField's markup.

### `Copyable` and `OpenNewTab`

Both are `bool` parameters that fill MudBlazor's single end-adornment slot.

| Parameter | Applies to | Effect |
|---|---|---|
| `Copyable` | any `T` | Copy-to-clipboard button; icon flips to a green check for 2 s after a click |
| `OpenNewTab` | `Url` / `Url?` only | "Open in new tab" icon; opens the current value with `noopener,noreferrer` |

`OpenNewTab` **wins over** `Copyable` when both are set — the slot holds one button; if you need
both, keep `Copyable` and put a separate control next to the field. On a non-`Url` field
`OpenNewTab` is a silent no-op (opening a `Title` is meaningless).

**`OpenNewTab` only opens absolute `http`/`https` addresses.** The text in the box is re-parsed
through the `Url` value object on every render *and* again on click; only a result whose `Scheme` is
`http` or `https` gets a click handler. Empty text, text the value object rejects, a relative path,
and any other scheme (`javascript:`, `data:`, `file:`, `vbscript:`) leave the callback unwired.

That state is how the icon disables itself: MudBlazor's adornment renders a clickable
`<button class="mud-input-adornment-icon-button">` only when the click callback carries a delegate,
and an inert `<span class="mud-icon-root … mud-input-adornment-icon">` otherwise. **CSS and E2E
selectors must tolerate both** — match
`.mud-input-adornment-icon, button.mud-input-adornment-icon-button`.

What reaches `window.open` is the value object's canonical string (`Uri.AbsoluteUri`), not the raw
keystrokes — `http://plain.example.org` opens as `http://plain.example.org/`, so interop mocks
asserting the exact argument must expect the canonical form. The adornment `aria-label`s
(`"Open in new tab"`, `"Copy to clipboard"`, `"Copied!"`) are hard-coded English.

## Layout primitives

`@namespace Zonit.Extensions.Website.MudBlazor`. No value objects, no DI, and no CSS ships with the
package — `page-header` is a bare class hook and all spacing comes from MudBlazor utility classes.
**All strings must arrive pre-translated**; unlike the similarly-named components in
`Zonit.Services.Dashboard`, these do not inject a culture provider.

```razor
@using Zonit.Extensions.Website.MudBlazor
@using MudBlazor

<PageHeader Title="Articles" Subtitle="Draft, review and publish">
    <Actions>
        <MudButton Variant="Variant.Filled"
                   Color="global::MudBlazor.Color.Primary">New article</MudButton>
    </Actions>
</PageHeader>

<LoadingSpinner IsLoading="_loading" Message="Loading articles…" MinHeight="240px">
    @if (_articles.Count == 0)
    {
        <EmptyState Icon="@Icons.Material.Outlined.Article"
                    Title="No articles yet"
                    Description="Create your first article to see it here.">
            <Actions>
                <MudButton Variant="Variant.Filled"
                           Color="global::MudBlazor.Color.Primary">Create</MudButton>
            </Actions>
        </EmptyState>
    }
    else
    {
        <MudList T="string">
            @foreach (var a in _articles)
            {
                <MudListItem T="string" Text="@a" />
            }
        </MudList>
    }
</LoadingSpinner>
```

`Color` is qualified here even though this file does not import `Zonit.Extensions` itself: once the
four-line block above is in `_Imports.razor`, `Zonit.Extensions` is in scope in *every* `.razor`
file, so qualifying unconditionally is the rule that always holds.

### `PageHeader`

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Title` | `string` | — | `[EditorRequired]`. Rendered as `Typo.h4`, weight 600 |
| `Subtitle` | `string?` | `null` | `Typo.body2`, `mud-text-secondary`; omitted when null/empty |
| `Actions` | `RenderFragment?` | `null` | Right-hand slot; the wrapper `div` is omitted when null |

### `EmptyState`

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Icon` | `string` | `Icons.Material.Outlined.Inbox` | MudBlazor icon string or inline SVG markup |
| `Title` | `string` | `"No data"` | `Typo.h6`, centred |
| `Description` | `string?` | `null` | Centred, `max-width: 320px`; omitted when null/empty |
| `MinHeight` | `string` | `"300px"` | Any CSS length |
| `Actions` | `RenderFragment?` | `null` | Rendered below the description |

### `LoadingSpinner`

It is a **content guard**, not just a spinner: it renders `ChildContent` only when `IsLoading` is
false.

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `IsLoading` | `bool` | **`true`** | Defaults to true — omit it and your content never renders |
| `Message` | `string?` | `null` | Shown under the spinner; pre-translated |
| `Color` | `global::MudBlazor.Color` | `Color.Primary` | Spinner colour |
| `Size` | `global::MudBlazor.Size` | `Size.Large` | Spinner size |
| `Center` | `bool` | `true` | `false` drops the centring flex classes |
| `MinHeight` | `string` | `"200px"` | Applies to the loading container only |
| `ChildContent` | `RenderFragment?` | `null` | Rendered when `IsLoading` is false |

## Known limitations

- **Trim/AOT: the `IsAotCompatible=true` on this assembly is inherited, not earned.** This
  package's own IL is clean — the converter dispatch is `typeof(T)` comparisons with no reflection,
  no `MakeGenericType`, no suppressions. But its entire purpose is wrapping MudBlazor, and
  MudBlazor 9.7.0 declares `IsTrimmable` **without** `IsAotCompatible` and contains both
  `MakeGenericType` and `[RequiresUnreferencedCode]`. Under `PublishAot`, ILC reports `IL3050` in
  `MudBlazor.Utilities.Converter.Dispatcher` and `IL2075` in `MudFormComponent<,>`. Treat this
  package as trim-friendly and **not** NativeAOT-ready, and do not let the badge convince you
  otherwise. Binding an unsupported `T` also lands you on `DefaultConverter<T>`, which is exactly
  the reflection path AOT cannot see through.
- **No extension point for your own value objects.** `ValueObjectConverter<T, TValueObject>` is
  `internal` and the type list is a private switch. Six types, take it or leave it.
- **`Culture` validates format, not existence.** It is built on `new CultureInfo(value)`, and under
  ICU a well-formed synthetic tag such as `xx-YY` is accepted; only a malformed identifier
  (`"not a culture"`) is rejected.
- **`UrlSlug` never reports an error.** Its constructor cannot fail for non-null input — it
  transliterates and strips instead. A user who types nothing but punctuation ends up with an
  empty slug and no message.

## See also

- `.zonit/extensions/core/value-objects.md` — the value objects themselves: `MaxLength` rules,
  `TryCreate` vs the throwing constructor, `HasValue`.
- `.zonit/extensions/website/` — `PageBase` / `PageViewBase<T>` / `PageEditBase<T>`, which usually
  host these fields (`PageEditBase<T>` supplies `EditContext`, `HasChanges` and the
  duplicate-submit guard).
