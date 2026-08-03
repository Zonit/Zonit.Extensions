# Zonit.Extensions.Organizations

Workspace (organization / tenant) context for Zonit applications: which organization the current user is
working in, and which ones they can switch into. Framework-agnostic — no `HttpContext`, no middleware, no
Blazor types. The ASP.NET Core middleware and the prerender → circuit bridge ship in
[Zonit.Extensions.Website](../Zonit.Extensions.Website/Readme.md).

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Organizations.svg)](https://www.nuget.org/packages/Zonit.Extensions.Organizations/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Organizations.svg)](https://www.nuget.org/packages/Zonit.Extensions.Organizations/)

```bash
dotnet add package Zonit.Extensions.Organizations
```

## What you get

- **`IOrganizationSource`** — the one contract you implement: load the current workspace, list switchable
  organizations, perform a switch. Everything else is built on it.
- **`IWorkspaceManager`** — per-scope state machine: `Initialize(StateModel)`, `InitializeAsync(ct)`,
  `SwitchOrganizationAsync(id, ct)`, plus `Workspace` / `Organizations` / `State` and `OnChange`.
- **`IWorkspaceProvider`** — read surface for UI and domain code: the active organization as the
  [`Organization`](../Zonit.Extensions/Readme.md) value object, `Organization.Empty` when none is selected.

`AddOrganizationsExtension()` registers exactly three scoped services — `IWorkspaceManager`,
`IWorkspaceProvider` and a `NullOrganizationSource` safety net — and nothing else.

## Setup

In a Website host, register only your source: `AddWebsite()` already calls `AddOrganizationsExtension()`
and `UseWebsite<TApp>()` installs the middleware.

```csharp
builder.Services.AddScoped<IOrganizationSource, AcmeOrganizationSource>();
builder.Services.AddWebsite();
```

Anywhere else (console, worker, tests) wire it yourself and drive the manager once per scope:

```csharp
services.AddScoped<IOrganizationSource, AcmeOrganizationSource>();
services.AddOrganizationsExtension();

await using var scope = provider.CreateAsyncScope();
var manager = scope.ServiceProvider.GetRequiredService<IWorkspaceManager>();
await manager.InitializeAsync(cancellationToken);
```

> **Use `AddScoped`, not `TryAddScoped`.** The null source is registered with `TryAddScoped`; a
> `TryAddScoped` of your own after it is a silent no-op and you keep an empty workspace forever.

## Implementing the source

```csharp
public interface IOrganizationSource
{
    Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceModel?> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
```

Three things the compiler will not tell you:

1. `InitializeAsync` and `GetOrganizationsAsync` are started **concurrently** on the same scoped instance
   (`Task.WhenAll`). Inject `IDbContextFactory<T>`, not a shared `DbContext` — EF Core forbids overlapping
   operations on one context.
2. `null` from `SwitchOrganizationAsync` means *denied* — this method is the authorization check.
   `GetOrganizationsAsync` returning `null` means "unknown"; return `[]` for "member of nothing".
3. `OrganizationModel.Id` must be non-empty and `Name` non-blank and ≤ 60 graphemes, or the projection to
   the `Organization` value object degrades silently (empty organization / empty or truncated title). It
   never throws.

In a Website host the whole snapshot — the active workspace and every organization you return — is
serialized into the prerendered HTML, so do not fill invoice fields (`Email`, `TaxIdentification`, address)
unless the page needs them.

## Reading the workspace

```razor
@inject IWorkspaceProvider Workspace

@if (Workspace.Organization.HasValue)
{
    <p>@Workspace.Organization.Name — @Workspace.Organization.Id</p>
}
```

`Organization` is a `readonly struct`: test `HasValue`, never `!= null`. Pages deriving from `PageBase` /
`PageViewBase<T>` already expose `Workspace` as a protected property.

## Switching organization

```csharp
if (!await manager.SwitchOrganizationAsync(organizationId, cancellationToken))
{
    // Denied — the previously selected organization is untouched and no OnChange fired.
}
```

A granted switch replaces the workspace, back-fills the organization list if the scope had none, and
raises `OnChange`, which `IWorkspaceProvider` and `ExtensionsBase`-derived components observe.

## Authorization is elsewhere

`WorkspaceModel.Permissions` / `Roles` are transported but never read by the framework. Authorization runs
off the `Identity` produced by `Zonit.Extensions.Auth` (`[RequirePermission]`, `IAuthorizationService`,
`<AuthorizeView>`). Pure tenancy lives here.

## Migration to 10.0.0-preview.10

Removed public types — there is no drop-in replacement; the framework only ever needs `IOrganizationSource`:

| Removed | Notes |
| --- | --- |
| `IOrganizationProvider` | `GetOrganizationAsync` / `GetUserOrganizationAsync` / `GetUserOrganizationsAsync` belong in your own data layer |
| `IOrganizationManager` | single-method (`GetAsync`) lookup contract, unused by the framework |
| `IOrganizationEntity` | unused marker; put `Guid OrganizationId` on your entity and filter with `IWorkspaceProvider.Organization` |
| `Zonit.Extensions.Organizations.Entities.Organization` | use the `Zonit.Extensions.Organization` value object or your own entity |
| `<ZonitOrganizationsExtension />` | replaced by `<WebsiteHydrator />` (Zonit.Extensions.Website), placed once in `App.razor` |

Behaviour changes:

- `IWorkspaceManager.SwitchOrganizationAsync` returns `Task<bool>` instead of `Task`. Source-compatible for
  a plain `await`, binary-breaking (recompile), source-breaking for anyone implementing the interface.
- A **denied switch is now a no-op**. It used to clear the workspace, logging the user out of an
  organization they did have access to; it now returns `false` and leaves the selection alone.
- A switch on a scope that was never initialized is no longer ignored — the source is called, so
  `SwitchOrganizationAsync` may arrive without a preceding `InitializeAsync` on the same scope.
- Malformed source data no longer throws `ArgumentException` from `IWorkspaceProvider.Organization`:
  `Guid.Empty` → `Organization.Empty`, blank name → `Title.Empty`, over-long name → truncated at 60
  graphemes. Read `IWorkspaceManager.Workspace?.Organization` for the verbatim values.

Earlier versions also exposed `IsPermission(string)` / `IsRole(string)` with hard-coded `"Developer"` /
`"All"` bypasses. That is gone; see [Zonit.Extensions.Auth](../Zonit.Extensions.Auth/Readme.md).

## License

MIT.
