# Projects (catalogs)

`Zonit.Extensions.Projects` answers one question per scope: **which project is the user working in
right now?** It is a small state machine plus two contracts. You implement `IProjectSource`, the
package caches the answer per DI scope in `ICatalogManager`, and Razor reads it through
`ICatalogProvider` as the `Project` value object.

**A catalog IS a project.** There is no separate "catalog" entity anywhere in this stack. The domain
uses one value object, `Zonit.Extensions.Project`, and the service names around it kept the older word:
`ICatalogProvider`, `ICatalogManager`, `CatalogModel`, `CatalogStateBridge`. Read "catalog" as "the
currently selected project + the list you may switch into". Do not go looking for a `Catalog` type —
`CatalogModel` is a one-property wrapper (`ProjectModel? Project`) and nothing else.

The package has **no ASP.NET Core dependency** (only `Microsoft.Extensions.DependencyInjection.Abstractions`
and `Zonit.Extensions`). All the web wiring — middleware, prerender bridge — lives in
`Zonit.Extensions.Website`.

## Read vs write

| | `ICatalogProvider` | `ICatalogManager` |
|---|---|---|
| Currency | `Project` VO (`Zonit.Extensions`) | raw `CatalogModel` / `ProjectModel` DTOs |
| Members | `Project Project`, `ImmutableArray<Project> Visible`, `event Action? OnChange` | `Initialize(StateModel)`, `Task<StateModel> InitializeAsync(ct)`, `Task<bool> SwitchProjectAsync(Guid, ct)`, `CatalogModel? Catalog`, `IReadOnlyCollection<ProjectModel>? Projects`, `StateModel? State`, `event Action? OnChange` |
| Use it for | rendering, scoping queries | switching projects, hydrating a scope |

Both live in namespace `Zonit.Extensions.Projects` and both are **scoped** — neither can be injected
into a singleton or an `IHostedService`; create a scope instead.

`State is null` is the "this scope was never hydrated" flag. It is the only way to tell "no project
selected" from "nobody asked the source yet".

## The one contract you implement: `IProjectSource`

Three methods, and their null semantics are not symmetric:

| Method | Returns | `null` means |
|---|---|---|
| `InitializeAsync(ct)` | `Task<CatalogModel>` — **non-nullable** | n/a. Return `new CatalogModel()` for "nothing selected" |
| `GetProjectsAsync(ct)` | `Task<IReadOnlyCollection<ProjectModel>?>` | same as empty — `Visible` ends up empty either way |
| `SwitchProjectAsync(id, ct)` | `Task<CatalogModel?>` | **"no access"** — the switch is refused and the current project is kept |

```csharp
using Microsoft.EntityFrameworkCore;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;

internal sealed class AppProjectSource(
    IDbContextFactory<AppDbContext> factory,
    IWorkspaceProvider workspace) : IProjectSource
{
    public async Task<CatalogModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var org = workspace.Organization;
        if (!org.HasValue)
            return new CatalogModel();               // no organization -> no project

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var row = await db.Projects
            .Where(p => p.OrganizationId == org.Id && p.IsCurrent)
            .Select(p => new ProjectModel { Id = p.Id, Name = p.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? new CatalogModel() : new CatalogModel { Project = row };
    }

    public async Task<IReadOnlyCollection<ProjectModel>?> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var org = workspace.Organization;
        if (!org.HasValue)
            return [];

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Projects
            .Where(p => p.OrganizationId == org.Id)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectModel { Id = p.Id, Name = p.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogModel?> SwitchProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var org = workspace.Organization;
        if (!org.HasValue)
            return null;                             // denial: current project stays

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var row = await db.Projects
            .Where(p => p.Id == projectId && p.OrganizationId == org.Id)
            .Select(p => new ProjectModel { Id = p.Id, Name = p.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new CatalogModel { Project = row };
    }
}
```

### Four things that bite here

**`InitializeAsync` and `GetProjectsAsync` are called concurrently.** `ICatalogManager.InitializeAsync`
starts both tasks and then `Task.WhenAll`s them — on the *same* scoped instance of your source. A shared
`DbContext` therefore throws `InvalidOperationException: A second operation was started on this context
instance...`. Use `IDbContextFactory<T>` (above), or a `SemaphoreSlim`, or any per-call connection.

**`SwitchProjectAsync` can arrive before `InitializeAsync` on the same scope.** Since 10.0.0 the switch
no longer no-ops on a cold scope, so your implementation must not assume it has already been asked to
initialize. It must also tolerate `GetProjectsAsync` being called immediately after a successful switch
(the repository back-fills the list when the scope never had one).

**Scoping to the organization is your job.** Nothing in this package looks at the active workspace.
Inject `IWorkspaceProvider` from `Zonit.Extensions.Organizations` and filter on `Organization.Id`. In a
`Zonit.Extensions.Website` host this is safe: `WorkspaceMiddleware` runs immediately before
`ProjectMiddleware`, so the workspace is already hydrated when your `InitializeAsync` is called. Outside
that pipeline, hydrate the workspace first yourself. See `.zonit/extensions/organizations/organizations.md`.

**`ProjectModel` is a DTO with no validation, and it feeds strict value objects.** The mapping is total
and never throws (that changed in 10.0.0), but it degrades:

| Source data | What `ICatalogProvider` shows |
|---|---|
| `Id == Guid.Empty` | current selection reads as `Project.Empty`; the row is **dropped** from `Visible` |
| `Name` blank / whitespace | `Project.Name` is `Title.Empty` (`HasValue == false`) |
| `Name` > 60 graphemes (`Title.MaxLength`) | whitespace-normalized and cut at the 60th grapheme |

If you want strict rejection instead of a silently truncated label, validate inside your source.

## Registration: `AddScoped`, never `TryAdd`

```csharp
services.AddWebsite();                                     // already calls AddProjectsExtension()
services.AddScoped<IProjectSource, AppProjectSource>();    // AddScoped, never TryAdd
```

`AddProjectsExtension()` `TryAdd`s an internal `NullProjectSource` as a safety net, so
`TryAddScoped<IProjectSource, AppProjectSource>()` **loses to it** and your app silently shows no
project, no list, and refuses every switch — with no exception and no log line. `AddScoped` appends and
wins regardless of ordering.

Without `Zonit.Extensions.Website`:

```csharp
services.AddProjectsExtension();                           // TryAdds NullProjectSource
services.AddScoped<IProjectSource, AppProjectSource>();
```

`AddProjectsExtension()` is the package's only DI entry point. There is no options overload and no
`AddProjectSource<T>()` helper.

## Reading the catalog

```csharp
internal sealed class ReportService(ICatalogProvider catalog)
{
    public Guid CurrentProjectId =>
        catalog.Project.HasValue
            ? catalog.Project.Id
            : throw new InvalidOperationException("No project selected.");
}
```

`Project` is a `readonly struct`, so `catalog.Project != null` compiles and is **always true**. The test
is `HasValue` (`Id != Guid.Empty`).

Worse, `Project` has an implicit conversion to `Guid`:

```csharp
Guid projectId = catalog.Project;   // compiles; Guid.Empty when nothing is selected
```

That silently turns "no project" into a query for `Guid.Empty` rows. Gate on `HasValue` first:

```csharp
Guid? projectId = catalog.Project.HasValue ? catalog.Project.Id : null;
```

`Project` and `Visible` are **cached snapshots**, rebuilt once per `ICatalogManager.OnChange` (this
changed in 10.0.0 — they used to recompute a `Title` and a fresh `ImmutableArray` on every read). Reading
them repeatedly inside `@foreach` is now free; you do not need to copy them into a local.

The flip side: state you mutate behind the manager's back is invisible, because no `OnChange` fires.

```csharp
// StateModel is ambiguous when Zonit.Extensions.Organizations is also imported.
var state = manager.State ?? new Zonit.Extensions.Projects.StateModel();
state.Catalog = new CatalogModel { Project = new ProjectModel { Id = id, Name = name } };

// Without this the provider keeps the old snapshot: no OnChange was raised.
manager.Initialize(state);
```

That `using` collision is real: `Zonit.Extensions.Organizations.StateModel` and
`Zonit.Extensions.Projects.StateModel` both exist, and a file importing both fails with
`CS0104: 'StateModel' is an ambiguous reference`. Qualify it or alias it.

### `Visible` is not `Projects`

`ICatalogProvider.Visible.Length` can be **smaller** than `ICatalogManager.Projects.Count`, because rows
with `Id == Guid.Empty` are dropped. Never index one by the other — join on `Id`:

```csharp
internal sealed class ProjectLabels(ICatalogProvider catalog, ICatalogManager manager)
{
    public IEnumerable<(Guid Id, string Display, string Verbatim)> Rows()
    {
        var raw = manager.Projects?.ToDictionary(p => p.Id) ?? [];

        foreach (var project in catalog.Visible)
            yield return (
                project.Id,
                project.Name.Value,                                  // <= 60 graphemes, may be cut
                raw.TryGetValue(project.Id, out var row) ? row.Name : string.Empty);
    }
}
```

Use `ICatalogManager.Projects` when you need the untruncated name.

### In Razor

```razor
@using Zonit.Extensions
@using Zonit.Extensions.Projects
@using Zonit.Extensions.Website
@inherits PageBase
@inject ICatalogManager Manager

@if (Catalog.Project.HasValue)
{
    <h3>@Catalog.Project.Name</h3>
}

@foreach (var project in Catalog.Visible)
{
    <button @onclick="() => SwitchAsync(project.Id)">@project.Name</button>
}

@code {
    private async Task SwitchAsync(Guid id)
    {
        if (!await Manager.SwitchProjectAsync(id))
            Toast.AddError("You do not have access to that project.");
    }
}
```

Components deriving from `Zonit.Extensions.Website`'s `ExtensionsBase` (which `PageBase` does) get
`Catalog` as a protected property that subscribes to `OnChange` on **first access** and unsubscribes on
dispose; a catalog change then re-runs `OnInitializedAsync`. The subscription is lazy, so a component
that never touches `Catalog` is never re-rendered by a project switch. Injecting `ICatalogProvider` by
hand means subscribing and unsubscribing by hand:

```csharp
internal sealed class CatalogWatcher : IDisposable
{
    private readonly ICatalogProvider _catalog;

    public CatalogWatcher(ICatalogProvider catalog)
    {
        _catalog = catalog;
        _catalog.OnChange += Reload;
    }

    private void Reload() { /* re-read _catalog.Project */ }

    public void Dispose() => _catalog.OnChange -= Reload;
}
```

Mind the namespaces: the services are in `Zonit.Extensions.Projects`, the `Project` VO in
`Zonit.Extensions`, and `PageBase` / `IToastProvider` in `Zonit.Extensions.Website`. Put all three in
`_Imports.razor`.

## Switching

```csharp
internal sealed class ProjectSwitcher(ICatalogManager manager)
{
    public async Task<string> OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await manager.SwitchProjectAsync(projectId, cancellationToken)
            ? "switched"
            : "no access - previous project kept";
}
```

`SwitchProjectAsync` returns `Task<bool>` (it was `Task` before 10.0.0 — recompile). `false` means your
source answered `null`: **nothing changed**, no state was written, and no `OnChange` fired. The user is
still in the project they were in. Surface it — a toast, an inline message — or the click looks like it
did nothing.

`true` means the snapshot now describes the requested project and `OnChange` has already fired.

## Hosting and hydration

With `Zonit.Extensions.Website` there is nothing to wire:

- `AddWebsite()` calls `AddProjectsExtension()` and registers `CatalogStateBridge`.
- `UseWebsite<TApp>(...)` installs the internal `ProjectMiddleware`, after `WorkspaceMiddleware`.
- The middleware calls `InitializeAsync(HttpContext.RequestAborted)` **once per scope**, and only when
  the request is authenticated (`IAuthenticatedRepository.Current.HasValue`) and `State is null`. Static
  assets and anonymous traffic are skipped, so an anonymous page sees `State == null` and
  `Project.Empty` — that is by design, not a bug.
- `<WebsiteHydrator />` in `App.razor` round-trips the snapshot across prerender → interactive.

There is **no** `<ZonitProjectsExtension />` component and **no** public `ProjectsMiddleware`; both were
in the old README and never existed in this shape. `"ZonitProjectsExtension"` survives only as the
persistence key inside `CatalogStateBridge`. See `.zonit/extensions/website/hydration.md`.

Outside a Website host (worker, console, WASM client, custom pipeline) hydrate the scope yourself:

```csharp
app.Use(async (context, next) =>
{
    var catalog = context.RequestServices.GetRequiredService<ICatalogManager>();
    if (catalog.State is null)
        await catalog.InitializeAsync(context.RequestAborted);

    await next(context);
});
```

Never call `InitializeAsync` from a component: it re-fetches from your source and fires `OnChange`,
which re-runs the component that called it.

## Compared with Organizations

`Zonit.Extensions.Organizations` is the structural twin and behaves identically for:
`TryAdd`-based registration plus a `Null*Source` safety net, `Switch*Async` returning `Task<bool>` with a
denial being a no-op, switching on a cold scope reaching the source anyway, the parallel two-call fan-out
in `InitializeAsync`, non-throwing VO projection, and `State is null` meaning "never hydrated".

Where they differ:

| | Organizations | Projects |
|---|---|---|
| Provider list member | none — `IWorkspaceProvider` exposes only `Organization` | `ICatalogProvider.Visible : ImmutableArray<Project>` |
| Snapshot model | `WorkspaceModel` also carries `Permissions` / `Roles` | `CatalogModel` carries only the project |
| DTO surface | `OrganizationModel` has address / tax / contact fields | `ProjectModel` is `Id` + `Name` only |
| Scoping input | none — the source scopes by user | must scope by the active organization |

To list organizations you must go through `IWorkspaceManager.Organizations` (the raw DTOs); there is no
`Visible` on the workspace provider.

## Known limitations

**Hydration is silently disabled under trimming.** `CatalogStateBridge` gates both `Restore` and the
persist callback on `JsonSerializer.IsReflectionEnabledByDefault`, which is `false` in **any**
`PublishTrimmed` publish, not only `PublishAot` (measured on SDK 10.0.301: a plain
`PublishTrimmed=true` self-contained publish prints `False`). In such a build the catalog snapshot does
not cross the prerender → interactive boundary, the circuit starts with `State == null`, and **nothing
is logged** — the first interactive render just shows `Project.Empty`.

The bridge roots every member it serializes with `[DynamicDependency]` on `StateModel`, `CatalogModel`
and `ProjectModel`, so turning the switch back on is safe for this payload:

```xml
<PublishTrimmed>true</PublishTrimmed>
<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
```

That flips the property back to `True` in a trimmed publish (verified). It re-enables reflection-based
`System.Text.Json` for the whole app, so weigh it against your other serialization. The alternative is to
re-hydrate the circuit yourself.

**No `Slug`.** `ProjectModel` has no slug field, so `ICatalogProvider.Project.Slug` is always
`UrlSlug.Empty` and `HasSnapshot` is driven by `Name` alone. Route on `Id`.

**No cross-scope caching.** The manager caches for the lifetime of one DI scope only — one HTTP request,
or one Blazor circuit. Every request calls your source again. Put caching in your `IProjectSource`.

## Names you may still see — none of them exist

| Gone / never real | Use instead |
|---|---|
| `IUserProjectManager` (never existed; old README fiction) | `IProjectSource` |
| `IProjectEntity` | a `Guid ProjectId` FK plus the `Project` VO, hydrated via `IProjectLookup` in `Zonit.Extensions.Databases` |
| `<ZonitProjectsExtension />`, `ProjectsMiddleware` | automatic in `UseWebsite<TApp>(...)` + `<WebsiteHydrator />` |
| `Task SwitchProjectAsync(...)` | `Task<bool> SwitchProjectAsync(...)` — branch on the result |
