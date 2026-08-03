# Zonit.Extensions.Projects

Per-scope "which project is the user working in?" context. Framework-agnostic — no `HttpContext`, no
middleware here; the ASP.NET Core / Blazor wiring lives in
[`Zonit.Extensions.Website`](../Zonit.Extensions.Website/Readme.md).

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Projects.svg)](https://www.nuget.org/packages/Zonit.Extensions.Projects/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Projects.svg)](https://www.nuget.org/packages/Zonit.Extensions.Projects/)

```bash
dotnet add package Zonit.Extensions.Projects
```

**A catalog is a project.** One value object, [`Project`](../Zonit.Extensions/Readme.md), two names: the
services around it are called `ICatalogProvider` / `ICatalogManager` / `CatalogModel`. There is no
separate catalog entity.

## What you get

- **`ICatalogProvider`** (read-only) — `Project Project` (`Project.Empty` when nothing is selected),
  `ImmutableArray<Project> Visible`, `event Action? OnChange`. Both members are cached snapshots,
  rebuilt when the manager changes.
- **`ICatalogManager`** (write / raw models) — `Initialize(StateModel)`,
  `Task<StateModel> InitializeAsync(ct)`, `Task<bool> SwitchProjectAsync(Guid, ct)`, plus
  `CatalogModel? Catalog`, `IReadOnlyCollection<ProjectModel>? Projects`, `StateModel? State`
  (`null` = this scope was never hydrated) and `OnChange`.
- **`IProjectSource`** — the one contract your app implements.
- `AddProjectsExtension()` — the package's only DI entry point. All registrations are scoped.

## Setup

```csharp
builder.Services.AddWebsite();                                   // already calls AddProjectsExtension()
builder.Services.AddScoped<IProjectSource, AppProjectSource>();  // AddScoped, never TryAdd
```

`AddProjectsExtension()` `TryAdd`s an internal null source as a safety net, so registering yours with
`TryAddScoped` loses to it and the app silently shows no project at all.

In a `Zonit.Extensions.Website` host, `UseWebsite<TApp>(...)` hydrates each authenticated request and
`<WebsiteHydrator />` carries the snapshot across the prerender → interactive boundary. Elsewhere, call
`InitializeAsync` yourself when `State is null`.

## Implementing the source

```csharp
public interface IProjectSource
{
    // Never returns null — an empty CatalogModel means "nothing selected".
    Task<CatalogModel> InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync(CancellationToken cancellationToken = default);

    // null == no access. The switch is refused and the current project is kept.
    Task<CatalogModel?> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
```

`InitializeAsync` and `GetProjectsAsync` are invoked **concurrently** on the same instance, so do not
share a `DbContext` between them. Scoping the query to the active organization is your job — inject
`IWorkspaceProvider` from `Zonit.Extensions.Organizations`.

## Reading and switching

```razor
@inject ICatalogProvider Catalog
@inject ICatalogManager Manager

@if (Catalog.Project.HasValue)          @* readonly struct — never test != null *@
{
    <h2>@Catalog.Project.Name</h2>
}

@foreach (var project in Catalog.Visible)
{
    <button @onclick="() => SwitchAsync(project.Id)">@project.Name</button>
}

@code {
    private async Task SwitchAsync(Guid id)
    {
        if (!await Manager.SwitchProjectAsync(id))
        {
            // Denied: nothing changed, no OnChange fired, the previous project is still active.
        }
    }
}
```

Malformed source rows degrade rather than throw: a row with `Id == Guid.Empty` is dropped from
`Visible`, a blank name becomes `Title.Empty`, and a name over 60 graphemes is cut. `Visible.Length` can
therefore be smaller than `Projects.Count` — join on `Id`, never by index.

The full guide, including the traps, ships inside the package and is installed into consuming repos at
`.zonit/extensions/projects/projects.md`.

## License

MIT.
