# Zonit.Extensions.Website.MudBlazor

Two things in one package: **value-object-aware MudBlazor inputs**, and three **stateless MudBlazor
layout primitives**. They live in two different namespaces.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)

```bash
dotnet add package Zonit.Extensions.Website.MudBlazor
```

| Component | Namespace |
|---|---|
| `ZonitTextField<T>`, `ZonitTextArea<T>` | `Zonit.Extensions.MudBlazor` |
| `PageHeader`, `EmptyState`, `LoadingSpinner` | `Zonit.Extensions.Website.MudBlazor` |

Despite the name it does not reference `Zonit.Extensions.Website`. Its dependencies are
`Zonit.Extensions` and **MudBlazor 9.7.0** (the MudBlazor 9 converter API is required — 8.x will
not work).

## Setup

**This package registers nothing** — there is no `AddXxx()` here. You wire MudBlazor yourself:

```csharp
using MudBlazor.Services;

builder.Services.AddMudServices();
```

…plus `<MudThemeProvider/>`, `<MudPopoverProvider/>`, `<MudDialogProvider/>`,
`<MudSnackbarProvider/>` in the host layout and `_content/MudBlazor/MudBlazor.min.css` /
`MudBlazor.min.js` in the host page. `MudBlazor.min.js` is required for the `Copyable` adornment.
Pages need an interactive render mode.

In `_Imports.razor`:

```razor
@using Zonit.Extensions                    @* Title, Description, Content, UrlSlug, Url, Culture *@
@using Zonit.Extensions.MudBlazor          @* ZonitTextField<T>, ZonitTextArea<T>                *@
@using Zonit.Extensions.Website.MudBlazor  @* PageHeader, EmptyState, LoadingSpinner             *@
@using MudBlazor
```

> **`Color` is ambiguous.** `Zonit.Extensions` ships a `Color` value object, so with both namespaces
> imported `Color.Primary` is a `CS0104` compile error. Write `global::MudBlazor.Color.Primary`, or
> add `@using Color = MudBlazor.Color`. It is the only name shared by the two public surfaces.

## Value-object inputs

```razor
<EditForm Model="_model" OnValidSubmit="SaveAsync" FormName="article">
    <DataAnnotationsValidator />

    <ZonitTextField @bind-Value="_model.Title"       Label="Title"    Counter="0" />
    <ZonitTextField @bind-Value="_model.Slug"        Label="URL slug" Copyable />
    <ZonitTextField @bind-Value="_model.HomeUrl"     Label="Homepage" OpenNewTab />
    <ZonitTextArea  @bind-Value="_model.Description" Label="Summary"  Counter="0" />

    <MudButton ButtonType="global::MudBlazor.ButtonType.Submit"
               Color="global::MudBlazor.Color.Primary">Save</MudButton>
</EditForm>
```

`T` is inferred from `@bind-Value` — never write `T="…"`. Everything inherited from `MudTextField`
works unchanged.

Supported `T` (and their `Nullable<>` forms): **`Title`, `Description`, `Content`, `UrlSlug`,
`Url`, `Culture`**. Any other type compiles with no warning and silently falls back to MudBlazor's
reflection-based `DefaultConverter<T>` — the converter table is an internal, closed switch and is
not extensible.

- Exceptions from the value object's constructor become MudBlazor conversion errors with the
  technical tail stripped (`Title cannot exceed 60 characters.`), and **the rejected text stays in
  the input** so the user can fix it in place.
- `Counter="0"` means "use the value object's `MaxLength`" → 60 for `Title`, 160 for `Description`.
  For those two, `MaxLength` is set to max + 1 so the value object gets a chance to throw a real
  message instead of the browser silently swallowing the keystroke.
- Clearing a field yields the value object's `Empty`, never `null` — even when bound as `Title?`.
  Test `HasValue`; `[Required]` will not fire.
- `Copyable` adds a copy-to-clipboard button. `OpenNewTab` (only meaningful for `Url`/`Url?`) adds
  an open-in-new-tab button, wired **only** when the current text parses to an absolute `http`/`https`
  URL — anything else, including relative paths and `javascript:`, renders an inert icon and opens
  nothing. `OpenNewTab` wins over `Copyable`; the adornment slot holds one button.
- `ZonitTextArea` defaults `Lines` to 3 and rewrites `Lines="1"` to 3; use `ZonitTextField` for a
  single-line input.

## Layout primitives

Unopinionated, DI-free, and they ship no CSS. Pass already-translated strings — they do not
localize anything.

```razor
<PageHeader Title="Articles" Subtitle="Draft, review and publish">
    <Actions><MudButton Color="global::MudBlazor.Color.Primary">New</MudButton></Actions>
</PageHeader>

<LoadingSpinner IsLoading="_loading" Message="Loading…">
    <EmptyState Title="No articles yet" Description="Create your first one." />
</LoadingSpinner>
```

`LoadingSpinner` is a content guard, not just a spinner: `ChildContent` renders only when
`IsLoading` is false — and `IsLoading` **defaults to `true`**, so always bind it.

## Trim and AOT

This package's own code is trim- and AOT-clean: converter selection is `typeof(T)` comparisons, with
no reflection and no suppressions. It wraps MudBlazor, however, and MudBlazor 9.7.0 is marked
trimmable but **not** AOT-compatible (`MakeGenericType` in its converter dispatcher,
`[RequiresUnreferencedCode]` on parts of the form stack). Full NativeAOT publishing therefore
depends on MudBlazor, not on us — treat the assembly's `IsAotCompatible` flag accordingly.

## License

MIT.
