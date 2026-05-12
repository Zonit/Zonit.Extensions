# Zonit.Extensions.Website.MudBlazor

MudBlazor field components that bind to Zonit value objects with automatic conversion, validation and error surfacing — AOT and trim friendly.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.MudBlazor.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website.MudBlazor/)

```bash
dotnet add package Zonit.Extensions.Website.MudBlazor
```

## What you get

- **`ZonitTextField<T>`** — MudTextField wrapper with a built-in VO converter and exception-aware validation.
- **`ZonitTextArea<T>`** — multiline counterpart for longer content.

Supported VOs: `Title`, `Description`, `Content`, `UrlSlug`, `Url`, `Culture`.

## Usage

```razor
@using Zonit.Extensions.MudBlazor

<EditForm Model="@_model" OnValidSubmit="Save">
    <DataAnnotationsValidator />

    <ZonitTextField @bind-Value="_model.Title"       Label="Title"       />
    <ZonitTextField @bind-Value="_model.Slug"        Label="URL slug"    />
    <ZonitTextField @bind-Value="_model.HomeUrl"     Label="Homepage"    />
    <ZonitTextArea  @bind-Value="_model.Description" Label="Description" />

    <MudButton ButtonType="ButtonType.Submit" Color="Color.Primary">Save</MudButton>
</EditForm>
```

The generic type parameter is inferred from `@bind-Value`. Construction errors thrown by the VO (e.g. invalid URL) are caught and rendered as MudBlazor validation messages.

## License

MIT.
