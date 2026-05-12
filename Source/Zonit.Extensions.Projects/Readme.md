# Zonit.Extensions.Projects

Project / catalog context for ASP.NET Core and Blazor. Exposes the user's currently selected project as the [`Project`](../Zonit.Extensions/Readme.md) value object.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Projects.svg)](https://www.nuget.org/packages/Zonit.Extensions.Projects/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Projects.svg)](https://www.nuget.org/packages/Zonit.Extensions.Projects/)

```bash
dotnet add package Zonit.Extensions.Projects
```

## What you get

- **`ICatalogProvider.Project : Project`** — current project VO; returns `Project.Empty` when none is selected.
- **`ICatalogProvider.Visible : ImmutableArray<Project>`** — projects visible to the current user (e.g. for cross-project views).
- **`ICatalogManager`** — write surface (`Initialize`, `SwitchProjectAsync(id)`, holds backend `CatalogModel` + `ProjectModel[]`).
- **`IUserProjectManager`** — consumer-implemented backend contract.
- A Blazor `<ZonitProjectsExtension />` for cross-render-mode persistence.

## Setup

```csharp
builder.Services.AddProjectsExtension();
app.UseMiddleware<ProjectsMiddleware>();
```

```razor
@using Zonit.Extensions
<ZonitProjectsExtension />
```

## Reading the project

```razor
@inject ICatalogProvider Catalog

@if (Catalog.Project.HasValue)
{
    <h2>@Catalog.Project.Name</h2>
}

@foreach (var p in Catalog.Visible)
{
    <option value="@p.Id">@p.Name</option>
}
```

## Switching project

```razor
@inject ICatalogManager Manager

<button @onclick="@(async () => await Manager.SwitchProjectAsync(id))">Open</button>
```

`OnChange` fires both on the manager and the provider.

## License

MIT.
