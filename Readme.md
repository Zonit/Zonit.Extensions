# Zonit.Extensions

A modular set of .NET 10 NuGet packages built on a shared, **framework-agnostic value-object
foundation**, with an ASP.NET Core / Blazor web kernel layered on top. Take the foundation alone, or
take the whole stack — every package above the foundation is optional and independently installable.

Current version: **10.0.0-preview.10** —
see [`Docs/RELEASE-NOTES-10.0.0-preview.10.md`](Docs/RELEASE-NOTES-10.0.0-preview.10.md) if you are
upgrading from preview.9 (it contains two ship-blocker fixes and several breaking changes).

## Packages

| Package | What it is | Depends on | Readme |
|---|---|---|---|
| **Zonit.Extensions** | The value-object foundation: `Title`, `Description`, `Content`, `Url`, `UrlPath`, `UrlSlug`, `Culture`, `Zone`, `Currency`, `Price`, `Money`, `Color`, `Asset`, `FileSize`, `Schedule`, `Identity`, `Permission`, `Role`, `Credential`, `Organization`, `Project` — plus `BaseException`, text/XML/reflection helpers. No ASP.NET Core, no Blazor. | — | [Readme](Source/Zonit.Extensions/Readme.md) |
| **Zonit.Extensions.Auth** | The authentication *core*: the scoped `IAuthenticatedProvider` / `IAuthenticatedRepository` pair that carries the current `Identity` through a unit of work, and the `IAuthSource` adapter you implement. No ASP.NET Core reference — the cookie scheme, the authorization handlers and the Blazor auth state live in Website. | Zonit.Extensions | [Readme](Source/Zonit.Extensions.Auth/Readme.md) |
| **Zonit.Extensions.Cultures** | Per-scope culture and time-zone state, an indexed translation registry, and the `Translation` value object. | Zonit.Extensions | [Readme](Source/Zonit.Extensions.Cultures/Readme.md) |
| **Zonit.Extensions.Organizations** | Workspace (organization) context: you implement `IOrganizationSource`, the package gives you `IWorkspaceProvider` (read) and `IWorkspaceManager` (write). | Zonit.Extensions | [Readme](Source/Zonit.Extensions.Organizations/Readme.md) |
| **Zonit.Extensions.Projects** | Catalog (project) context — the structural twin of Organizations: you implement `IProjectSource`, the package gives you `ICatalogProvider` / `ICatalogManager`. | Zonit.Extensions | [Readme](Source/Zonit.Extensions.Projects/Readme.md) |
| **Zonit.Extensions.Tenants** | Multi-tenancy: you implement `ITenantSource` (host name → `Tenant`), the package gives you `ITenantProvider` plus a typed, persisted settings system. Ships a source generator that emits a strongly-typed accessor for every `Setting<T>` you declare. | Zonit.Extensions | [Readme](Source/Zonit.Extensions.Tenants/Readme.md) |
| **Zonit.Extensions.Website** | The Blazor / ASP.NET Core web kernel: `AddWebsite` + `UseWebsite<TApp>`, the plug-in area model, string-keyed layouts, the `PageBase` / `PageViewBase<T>` / `PageEditBase<T>` component chain, `[RequirePermission]`, navigation, breadcrumbs, toasts, cookies, and prerender→circuit hydration. Pulls in the five cores above. | all of the above | [Readme](Source/Zonit.Extensions.Website/Readme.md) |
| **Zonit.Extensions.Website.MudBlazor** | MudBlazor add-on: `ZonitTextField<T>` / `ZonitTextArea<T>` bound directly to value objects, plus `PageHeader`, `EmptyState` and `LoadingSpinner`. | MudBlazor 9.7.0+ | [Readme](Source/Zonit.Extensions.Website.MudBlazor/Readme.md) |

Two source-generator projects (`Zonit.Extensions.Tenants.SourceGenerators`,
`Zonit.Extensions.Website.SourceGenerators`) are not separate packages — they ship as analyzers inside
Tenants and Website respectively.

## How it layers

```
Zonit.Extensions                     value objects only - no ASP.NET, no Blazor, no DI registration
        |
        +-- Zonit.Extensions.Cultures        culture + time zone state, translations
        +-- Zonit.Extensions.Auth            current Identity per scope
        +-- Zonit.Extensions.Organizations   active workspace per scope
        +-- Zonit.Extensions.Projects        active catalog per scope
        +-- Zonit.Extensions.Tenants         resolved tenant + typed settings
        |
        +-- Zonit.Extensions.Website         wires all five above + the Blazor/ASP.NET kernel
                    |
                    +-- Zonit.Extensions.Website.MudBlazor
                    +-- (your app's IWebsiteArea plug-ins)
```

The five domain cores are deliberately independent of ASP.NET Core: the same `IWorkspaceProvider` works
in a Blazor circuit, a console worker and a unit test. Website is the only package that knows about
`HttpContext`.

## Quick start

Install `Zonit.Extensions.Website`; the five cores and the value-object foundation come with it.

```csharp
// Program.cs
using Zonit.Extensions;             // AddWebsite, UseWebsite, the value objects
using Zonit.Extensions.Auth;        // IAuthSource

var builder = WebApplication.CreateBuilder(args);

// One call. AddWebsite() already registers Auth, Cultures, Organizations,
// Projects and Tenants - do NOT call AddAuthExtension() etc. as well.
builder.Services.AddWebsite();

// The adapters you implement. Use AddScoped, never TryAddScoped: each core
// TryAdds a no-op Null*Source as a safety net, and TryAdd would lose to it.
builder.Services.AddScoped<IAuthSource, MyAuthSource>();

var app = builder.Build();

// Mount one or more Sites. Non-root mounts must be registered BEFORE the root
// mount, or the root swallows their routes.
app.UseWebsite<AdminApp>("/admin", o => o.AddArea<AdminArea>());
app.UseWebsite<App>("/", o => o.AddArea<ShopArea>());

app.Run();
```

`AddWebsite` is services-time (registration); `UseWebsite<TApp>` is middleware-time and mounts one Site
at a URL prefix. There is no `UseAuthExtension()` and no manual `UseMiddleware<…>()` — `UseWebsite`
installs the culture, workspace, catalog, tenant and session middleware itself, in the required order.

One tag in `App.razor` carries prerendered state across the SSR → circuit boundary:

```razor
@using Zonit.Extensions.Website.Hydration

<WebsiteHydrator @rendermode="@RenderMode.InteractiveServer" />
<Routes />
```

A page:

```razor
@page "/orders"
@inherits PageBase
@attribute [RequirePermission("orders.read")]

<h1>@T("Orders for {0}", Workspace.Organization.Name)</h1>
```

## Design principles

- **Value objects are framework-agnostic and strict.** No ASP.NET Core or Blazor in `Zonit.Extensions`.
  Constructors and implicit string conversions **throw** past `MaxLength` — there is no silent
  truncation. Use `TryCreate` / `TryParse` for anything user-supplied. Most VOs are `readonly struct`,
  so the emptiness test is `HasValue`, never `!= null`.
- **The foundation registers nothing.** There is no `AddZonitExtensions()`. You reference the package
  and name the types; JSON converters and `TypeConverter`s are attached to the types themselves.
- **Persist the Id only.** Composite VOs (`Identity`, `Organization`, `Project`) carry a snapshot for the
  UI but persist as a single `Guid`. They perform no implicit I/O — re-hydration is an explicit,
  opt-in call.
- **Per-scope state where it matters.** Culture, workspace, catalog, tenant and authentication state are
  scoped to the request or circuit, never singletons. No cross-request races.
- **You own the data, the framework owns the plumbing.** Each domain core defines one adapter interface
  (`IAuthSource`, `IOrganizationSource`, `IProjectSource`, `ITenantSource`) and ships a no-op default so
  a host boots without one. Registering yours with `AddScoped` is the single step that makes a package
  do anything.
- **Degrade, don't throw, on data you did not write.** Malformed rows coming back from a consumer source
  or a database column project to `Empty` / truncated values rather than taking a query or a render
  down. Validate in your own adapter if you want strict rejection.
- **One source of truth per concern.** Cookies, claims, navigation, areas, layouts — every concept has
  exactly one provider.

## AI assistant instructions ship with the packages

Every package carries its own documentation and **installs it into your repository at build time**. The
authored docs live in [`Instruction/extensions/`](Instruction/extensions/); each package's
`buildTransitive/*.targets` declares only its own, so you get docs for exactly what you installed —
Cultures alone gives you the cultures guide, adding Website makes the website guides appear.

On the first build after install, the shared installer writes into the workspace root (the nearest
ancestor holding `.git` or a solution file):

| Output | Written when |
|---|---|
| `.zonit/extensions/<area>/*.md` | always (neutral, human- and agent-readable) |
| `.zonit/index.md` | always — a generated map of what is installed |
| `.claude/skills/zonit-extensions/SKILL.md` + a `CLAUDE.md` block | `.claude/` or `CLAUDE.md` exists |
| `.cursor/rules/zonit-ext*.mdc` | `.cursor/` exists |
| `.github/instructions/*` + a `.github/copilot-instructions.md` block | `.vscode/`, `.github/instructions/` or `copilot-instructions.md` exists |

Nothing is written if no editor is detected, and nothing is written on CI
(`ContinuousIntegrationBuild=true`). Generated files are rewritten only when their content actually
changes, so this does not dirty git on every build. Umbrella blocks are injected between
`<!-- ZONIT:extensions START -->` / `END` markers and never touch anything outside them.

Knobs (set in your `.csproj` or `Directory.Build.props`) —
see [`Zonit.Extensions.Instructions.targets`](Source/buildTransitive/Zonit.Extensions.Instructions.targets):

```xml
<PropertyGroup>
  <ZonitExtInstructions>false</ZonitExtInstructions>          <!-- master off switch -->
  <ZonitExtEditors>auto</ZonitExtEditors>                     <!-- auto | all | none | cursor;claude -->
  <ZonitExtInstructionsRoot>.zonit</ZonitExtInstructionsRoot>  <!-- relocate the doc tree -->
  <ZonitExtInstructionsAnchor>$(MSBuildThisFileDirectory)</ZonitExtInstructionsAnchor>
</PropertyGroup>
```

The same 21 documents are the reference material for humans too — start at
[`Instruction/extensions/core/value-objects.md`](Instruction/extensions/core/value-objects.md) for the
foundation, or [`Instruction/extensions/website/hosting.md`](Instruction/extensions/website/hosting.md)
for the web kernel.

## Repository layout

```
Source/
  Zonit.Extensions/                            value objects
  Zonit.Extensions.Auth/                       authentication core
  Zonit.Extensions.Cultures/                   i18n
  Zonit.Extensions.Organizations/              workspace context
  Zonit.Extensions.Projects/                   catalog context
  Zonit.Extensions.Tenants/                    multi-tenancy + settings
  Zonit.Extensions.Tenants.SourceGenerators/   settings accessor generator (ships as an analyzer)
  Zonit.Extensions.Website/                    Blazor / ASP.NET Core kernel
  Zonit.Extensions.Website.MudBlazor/          MudBlazor add-on
  Zonit.Extensions.Website.SourceGenerators/   view-model metadata generator (ships as an analyzer)
  buildTransitive/                             the AI-instruction installer, one stub per package
Instruction/extensions/                        the 21 authored guides, packed into the nupkgs
Docs/                                          audits, migration notes, release notes
Example/Zonit.Extensions.ConsumerGate/         build-time regression gate for both source generators
```

`Example/Zonit.Extensions.ConsumerGate` is **not** a demo application. It is a minimal consumer that
declares one `Setting<T>` and one `PageViewBase<T>` view model, so that generator output which fails to
compile in a consumer breaks the build here first — the exact failure mode that shipped in preview.9.

## Building

```bash
dotnet build Zonit.Extensions.sln -c Release
dotnet pack  Zonit.Extensions.sln -c Release   # 8 nupkgs + 8 snupkgs
```

The solution builds with 0 warnings and 0 errors; trim and AOT analyzers are enabled for every non-Roslyn
project. See [`Instruction/extensions/website/aot.md`](Instruction/extensions/website/aot.md) for an
honest account of what is genuinely trim/AOT-safe and what is annotated instead.

## License

MIT — see [LICENSE](LICENSE.txt).
