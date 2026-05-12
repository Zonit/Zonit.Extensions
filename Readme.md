# Zonit.Extensions

A modular set of NuGet packages that share a common, **framework-agnostic value-object foundation** and layer ASP.NET Core / Blazor integration on top.

> **All `*.Abstractions` packages have been retired** in favour of in-tree projects. Each domain (Cultures, Auth, Organizations, Projects) is now one package, and value objects live in the lightweight foundation `Zonit.Extensions`.

## Packages

| Package | Description | Readme |
|---|---|---|
| **Zonit.Extensions** | VO foundation — `Identity`, `Permission`, `Role`, `Credential`, `Organization`, `Project`, `Culture`, `Translated`, `Color`, `Asset`, `Money`, `Price`, `Url`, `Title`, … | [Readme](Source/Zonit.Extensions/Readme.md) |
| **Zonit.Extensions.Auth** | Authentication / authorization on top of the standard ASP.NET Core stack. `[RequirePermission("orders.read")]`, wildcard policies, `Identity` VO, cookie scheme. | [Readme](Source/Zonit.Extensions.Auth/Readme.md) |
| **Zonit.Extensions.Cultures** | Per-scope culture state, 17 BCP-47 languages, indexed translation registry, `Translated` VO. | [Readme](Source/Zonit.Extensions.Cultures/Readme.md) |
| **Zonit.Extensions.Organizations** | Tenant context exposing `Organization` VO. | [Readme](Source/Zonit.Extensions.Organizations/Readme.md) |
| **Zonit.Extensions.Projects** | Project / catalog context exposing `Project` VO. | [Readme](Source/Zonit.Extensions.Projects/Readme.md) |
| **Zonit.Extensions.Website** | Blazor / ASP.NET Core base components, areas / plugin model, navigation, breadcrumbs, toasts, cookies. | [Readme](Source/Zonit.Extensions.Website/Readme.md) |
| **Zonit.Extensions.Website.MudBlazor** | MudBlazor `ZonitTextField<T>` / `ZonitTextArea<T>` bound to VOs with validation. | [Readme](Source/Zonit.Extensions.Website.MudBlazor/Readme.md) |

## Architecture at a glance

```
Zonit.Extensions               <- pure VOs, no ASP.NET, no Blazor (lightweight foundation)
        ↑
        ├── Zonit.Extensions.Cultures        (translations + culture state)
        ├── Zonit.Extensions.Auth            (authn/authz, [RequirePermission], Identity VO)
        ├── Zonit.Extensions.Organizations   (tenant context, Organization VO)
        ├── Zonit.Extensions.Projects        (project context, Project VO)
        │
        └── Zonit.Extensions.Website
                ├── Zonit.Extensions.Website.MudBlazor
                └── (your app's IWebsiteArea plug-ins)
```

## Design principles

- **VOs are framework-agnostic.** No ASP.NET Core or Blazor in `Zonit.Extensions`. Every VO is a `readonly struct`, AOT-safe, with hand-written JSON / TypeConverter.
- **Authorization in one place.** Tenant providers (`IWorkspaceProvider`, `ICatalogProvider`) only expose context; permission / role checks go through Microsoft's `[Authorize]` pipeline extended with `[RequirePermission]`. No more `IsPermission("...")` shortcuts.
- **Persist Id only.** Composite VOs (`Identity`, `Organization`, `Project`) carry a snapshot for UI but persist as a single `Guid`. Consumers re-hydrate the snapshot on demand via their database extension — VOs do no implicit I/O.
- **Per-scope state where it matters.** Culture state, organization state and authentication state are scoped (request / circuit), not singletons. No cross-request races.
- **One source of truth per concern.** Cookies, claims, navigation, areas — every concept has exactly one provider.

## Quick start (Blazor + MudBlazor)

```csharp
// Program.cs
builder.Services
    .AddCulturesExtension()
    .AddAuthExtension()
    .AddOrganizationsExtension()
    .AddProjectsExtension()
    .AddWebsite(o => o.AddArea<MyArea>())
    .AddMudServices();

var app = builder.Build();

app
   .UseAuthExtension()
   .UseMiddleware<CultureMiddleware>()
   .UseMiddleware<OrganizationsMiddleware>()
   .UseMiddleware<ProjectsMiddleware>();

app.Run();
```

```razor
@page "/orders"
@inherits PageBase
@attribute [RequirePermission("orders.read")]

<h1>@T("Orders for {0}", Workspace.Organization.Name)</h1>
```

## Repository layout

```
Source/
  Zonit.Extensions/                       value objects
  Zonit.Extensions.Auth/                  authn/authz
  Zonit.Extensions.Cultures/              i18n
  Zonit.Extensions.Organizations/         tenant
  Zonit.Extensions.Projects/              project
  Zonit.Extensions.Website/               Razor / Blazor
  Zonit.Extensions.Website.MudBlazor/     MudBlazor add-on
  Zonit.Extensions.Website.SourceGenerators/   AOT page-metadata generator
```

Each project has its own [Readme.md](Source/Zonit.Extensions/Readme.md). Examples live in [Example/](Example/).

## License

MIT — see [LICENSE](LICENSE.txt).
