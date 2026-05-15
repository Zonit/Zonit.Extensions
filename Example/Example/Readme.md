# Example — Zonit.Extensions reference host

End-to-end Blazor Server host that wires **every** `Zonit.Extensions.*` package and
demonstrates the full feature surface across one host project + seven feature-area
projects. None of these projects are published to NuGet (`<IsPackable>false</IsPackable>`).

## Layout

```
Example/
├── Directory.Build.props          ← shared TFM/lang settings for every Example.* project
├── Example/                       ← the host (this folder)
│   ├── Example.csproj             ← Microsoft.NET.Sdk.Web — Program.cs, layout, stubs
│   ├── Program.cs                 ← AddWebsite, register all areas + stubs, map login
│   ├── Components/
│   │   ├── App.razor              ← root document, theming from Tenant.Settings
│   │   ├── Routes.razor           ← Router + AdditionalAssemblies for every area
│   │   ├── HomeArea.cs            ← built-in IWebsiteArea contributing /, /login
│   │   ├── Layout/                ← MainLayout + NavMenu (driven by INavigationProvider)
│   │   └── Pages/                 ← Home.razor, Login.razor
│   └── Stubs/                     ← in-memory IAuthSource / IUserDirectory / etc.
├── Example.Cultures/              ← /cultures + /cultures/translation + /cultures/time-zone
├── Example.Auth/                  ← /auth + /auth/authorize-view + /auth/require-permission
├── Example.Organizations/         ← /organizations + /organizations/switcher
├── Example.Projects/              ← /projects + /projects/switcher
├── Example.Tenants/               ← /tenants + /tenants/solo
├── Example.Components/            ← PageEditBase, PageViewBase, AutoSave, Toast, Breadcrumbs, Cookies
└── Example.ValueObjects/          ← /vo/strings + /vo/numbers + /vo/identity + /vo/tenancy
```

Every `Example.*` sub-project is a `Microsoft.NET.Sdk.Razor` library that ships:

- one `IWebsiteArea` (`{Domain}Area.cs`) declaring its navigation,
- one `_Imports.razor`,
- a folder of `*.razor` pages mounted via `@page`.

The host references each as a `<ProjectReference>`, registers them with
`opts.AddArea(new {Domain}Area())`, and wires them into `MapRazorComponents<App>().AddAdditionalAssemblies(...)`.

This is exactly how a real consumer plugs feature modules into a Zonit-hosted app.

## Run

```powershell
cd Example/Example
dotnet run
```

Open <https://localhost:7290>. Log in with one of the seeded accounts:

| Username | Password | Roles | Permissions |
|---|---|---|---|
| `admin` | `admin` | `admin`, `user` | `users.read`, `users.write`, `settings.write`, `*` |
| `user`  | `user`  | `user` | `users.read` |

## What's demonstrated

| Area | Routes | What it shows |
|---|---|---|
| Cultures | `/cultures`, `/cultures/translation`, `/cultures/time-zone` | `ICultureProvider` / `ICultureManager`, `Translate(...)`, language + zone switching |
| Auth | `/auth`, `/auth/authorize-view`, `/auth/require-permission`, `/auth/admin-only` | Identity VO, `<AuthorizeView Roles>` / `<AuthorizeView Policy>`, `[RequirePermission]`, wildcards |
| Organizations | `/organizations`, `/organizations/switcher` | `IWorkspaceProvider`, `IWorkspaceManager`, scoped switcher |
| Projects | `/projects`, `/projects/switcher` | `ICatalogProvider`, `ICatalogManager`, scoped under organization |
| Tenants | `/tenants`, `/tenants/solo` | `ITenantProvider.Settings.Site/Theme/Maintenance/SocialMedia`, solo-mode auto-fallback |
| Components | `/components/page-view`, `/components/page-edit`, `/components/auto-save`, `/components/toasts`, `/components/breadcrumbs`, `/components/cookies` | `PageViewBase<T>`, `PageEditBase<T>`, `[AutoSave]`, `IToastProvider`, `IBreadcrumbsProvider`, `ICookieProvider` |
| Value objects | `/vo/strings`, `/vo/numbers`, `/vo/identity`, `/vo/tenancy` | Title / Url / UrlSlug / Money / Price / FileSize / Color / Identity / Permission / Organization / Project / Culture / TimeZone |

## Architecture map

```
Program.cs
 ├── AddWebsite() — pulls Cultures, Auth, Organizations, Projects, Tenants
 │                  + Razor Components, Antiforgery, Authentication
 │                  + Navigation/Breadcrumbs/Toast/Cookie providers
 │                  + PermissionPolicyProvider (for <AuthorizeView Policy="orders.read">)
 ├── opts.AddArea(new HomeArea());           ← built-in
 ├── opts.AddArea(new Example.Cultures.CulturesArea());
 ├── opts.AddArea(new Example.Auth.AuthArea());
 ├── opts.AddArea(new Example.Organizations.OrganizationsArea());
 ├── opts.AddArea(new Example.Projects.ProjectsArea());
 ├── opts.AddArea(new Example.Tenants.TenantsArea());
 ├── opts.AddArea(new Example.Components.ComponentsArea());
 ├── opts.AddArea(new Example.ValueObjects.ValueObjectsArea());
 └── Stubs/ — in-memory implementations for the consumer-side contracts:
       DemoStore, IAuthSource, IUserDirectory,
       IOrganizationSource, IProjectSource, ITenantSource

UseWebsite() — ForwardedHeaders → Compression → Authentication → Authorization
              → SessionMiddleware → CultureMiddleware
              → WorkspaceMiddleware → ProjectMiddleware → TenantMiddleware
```

## Solo vs multi-site

The demo registers an `ITenantSource` that resolves only `localhost` to a real tenant
record. Other host names fall through and `TenantMiddleware` seeds `Tenant.Solo`
automatically, so `provider.Settings.Site.Title` etc. always work.

To force solo-mode globally, comment out the `ITenantSource` registration in
`Program.cs`:

```csharp
// builder.Services.AddScoped<ITenantSource, InMemoryTenantManager>();
```

## Replacing stubs

Every class in `Stubs/` implements a consumer-side contract. Swap them for EF Core /
Dapper / a remote-API client and the rest of the host (middleware, providers,
components) keeps working unchanged — that is the whole point of the architecture.
