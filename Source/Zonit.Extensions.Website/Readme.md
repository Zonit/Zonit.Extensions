# Zonit.Extensions.Website

The Blazor / ASP.NET Core host for the Zonit stack. It owns the request pipeline, the Razor base
components, and the plumbing that carries per-request state into an interactive circuit.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)

```bash
dotnet add package Zonit.Extensions.Website
```

Targets `net10.0`. Version documented here: **10.0.0-preview.10**.

## Two calls, two phases

`AddWebsite()` configures the **container**. `UseWebsite<TApp>()` mounts a **Site** — a URL prefix
with its own middleware branch, its own `MapRazorComponents<TApp>()`, and its own subset of
registered areas. A host can mount as many Sites as it likes.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddWebsite(o =>
{
    o.Url = "https://example.com";
    o.AddArea<HomeArea>();
    o.AddArea<AuthArea>();
});

var app = builder.Build();

// Non-root mounts FIRST — see the ordering rule below.
app.UseWebsite<App>("/admin", o =>
{
    o.Permission = "admin";
    o.AddArea<AuthArea>();
});

app.UseWebsite<App>("/", o =>
{
    o.AddArea<HomeArea>();
    o.AddArea<AuthArea>();
});

app.Run();
```

**`AddWebsite()` already registers the five domain cores** — `AddCulturesExtension()`,
`AddAuthExtension()`, `AddOrganizationsExtension()`, `AddProjectsExtension()`,
`AddTenantsExtension()` — plus navigation, breadcrumbs, toasts, cookies and layouts. Calling any of
them yourself is a double registration, not a requirement.

**`AddWebsite()` also loads configuration.** It calls `AddAppData()` from
[Zonit.Extensions.Configuration](../Zonit.Extensions.Configuration/Readme.md), so every JSON file
under `AppData/Settings` is in place before anything binds. This works from either receiver —
`builder.AddWebsite(…)` and `builder.Services.AddWebsite(…)` behave identically, because the host
registers its `ConfigurationManager` as the `IConfiguration` service and that type is an
`IConfigurationBuilder` as well, so the source list is reachable from the service collection alone.

Opt out with `o.UseAppData = false`. To keep the loader but change its settings, call
`builder.AddAppData(o => …)` first — it is idempotent, so `AddWebsite` will not override you.
Either way `AddWebsite` has to run before `Build()`, or the files arrive after Kestrel and the
logging providers have already read configuration.

**`UseWebsite` needs no companion `Use*` calls.** Each Site branch installs its own
`UseRouting` / `UseAuthentication` / `UseAuthorization` / `UseAntiforgery` and the whole Zonit
middleware chain (cookies → session → workspace → project → tenant → culture). There is no
`app.UseAuthExtension()` and no manual `UseMiddleware<CultureMiddleware>()` — those middlewares are
`internal` and are wired for you.

**Declare non-root mounts before the root mount.** The root branch ends in a terminal
`UseEndpoints`, so any `MapWhen` branch registered after it is unreachable. Getting this backwards
used to fail silently with a 405 on `/<sub>/_blazor/negotiate`; it now throws at startup with a
message telling you to reorder.

## One tag in `App.razor`

```razor
@using Zonit.Extensions.Website.Hydration

<body>
    <WebsiteHydrator @rendermode="@RenderMode.InteractiveServer" />
    <Routes @rendermode="@RenderMode.InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
```

The HTTP-request scope that runs the middleware is not the SignalR circuit scope that owns
interactive components, so without this the circuit starts anonymous, with default culture and no
workspace — after a perfectly authenticated SSR pass. `<WebsiteHydrator />` aggregates every
registered `IPersistentStateProvider` and round-trips their snapshots through
`PersistentComponentState`. It needs a render mode, or it only ever runs on the SSR half.

## A page

```razor
@page "/orders"
@inherits PageViewBase<List<OrderRow>>
@attribute [RequirePermission("orders.read")]

<h1>@T("Orders for {0}", Workspace.Organization.Name)</h1>

@if (IsLoading)
{
    <p>@T("Loading…")</p>
}
else
{
    @foreach (var row in Model ?? [])
    {
        <p>@row.Customer</p>
    }
}

<button @onclick="@(() => Toast.AddSuccess(T("Saved")))">@T("Save")</button>

@code {
    protected override Task<List<OrderRow>?> LoadAsync(CancellationToken cancellationToken)
        => _orders.ListAsync(cancellationToken);
}
```

Put the usings in `_Imports.razor` once — `Zonit.Extensions` (the value objects: `UrlPath`,
`Title`, `Permission`), `Zonit.Extensions.Website` (the base classes and UI providers) and
`Zonit.Extensions.Website.Authentication` (`[RequirePermission]`, `[RequireRole]`).

`PageViewBase<T>` gives you `Model`, `IsLoading`, a `LoadAsync` that re-runs when the workspace /
catalog / tenant changes, and prerender→circuit model persistence. `PageEditBase<T>` adds an
`EditContext`, DataAnnotations validation translated through `ICultureProvider`, change tracking,
duplicate-submit protection and per-field `[AutoSave]`. Both inherit the injected provider surface
(`Culture`, `Workspace`, `Catalog`, `Tenant`, `Authenticated`, `Toast`, `Cookie`,
`BreadcrumbsProvider`) and `T()` / `TM()` for translation.

Every overridable lifecycle method takes a `CancellationToken`, and that token is genuinely
cancelled when the component is disposed — override the token overload, not the framework's
parameterless one.

## What else is in the box

- **Areas** — `IWebsiteArea` plug-ins that contribute Razor components, navigation, middleware
  hooks and minimal-API endpoints, mountable on any number of Sites.
- **Layouts** — string-keyed layout registry with `[LayoutKey("…")]`, `[NoLayout]` and a runtime
  `LayoutKey` override.
- **Permissions** — `[RequirePermission]` / `[RequireRole]` with a synthetic policy provider, the
  `"Zonit"` cookie authentication scheme, and a Blazor `AuthenticationStateProvider`.
- **UI services** — navigation, breadcrumbs, toasts (`<ZonitToasts />`) and cookies, plus
  `UrlPath.ToHref()`, which is mandatory for links under a non-root mount.
- **A source generator** that emits AOT-safe view-model metadata for every `T` you use with
  `PageViewBase<T>` / `PageEditBase<T>`.

## Upgrading from 10.0.0-preview.9

- **Five components were deleted** in commit `1cfc6d8`: `<ZonitCulturesExtension />`,
  `<ZonitIdentityExtension />`, `<ZonitOrganizationsExtension />`, `<ZonitProjectsExtension />`,
  `<ZonitCookiesExtension />`. Replace all of them with one `<WebsiteHydrator />`.
- **The source generator no longer emits a `JsonSerializerContext`.** In preview.9 it did, and
  because Roslyn does not chain generators the emitted partial was never completed — every
  consumer build failed with `CS0534`. `ViewModelMetadata<T>.JsonTypeInfo` is gone with it.
- **`INavigationProvider` is now scoped**, not singleton. Injecting it into a singleton
  `IHostedService` throws at startup; take `IServiceScopeFactory` and create a scope. Additions
  still land in the process-wide store.
- **Component cancellation tokens now really cancel on dispose.** Fire-and-forget work riding on a
  page's token will abort when the user navigates away — pass `CancellationToken.None` or move it
  to a service.
- **`PageViewBase<T>` no longer overrides `OnRefreshChangeAsync`**, which removes a duplicate
  `LoadAsync` on every provider change.
- **Model persistence and all hydration bridges no-op instead of throwing** when
  `JsonSerializer.IsReflectionEnabledByDefault` is `false` — which the SDK sets for any
  `PublishTrimmed` publish. Read the AOT notes before publishing trimmed.

## Trimming and Native AOT

The assembly ships `IsTrimmable` / `IsAotCompatible`. Two members warn at your call site:
`AddWebsite` and `ExtensionsBase.Options<T>()`. Deriving from the page base classes warns about
nothing.

Under `PublishTrimmed` (and therefore `PublishAot`) prerender→circuit hydration and
`PageViewBase` model persistence **silently switch themselves off** — the app keeps working, but
circuits start anonymous with default culture. Set
`<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>` to
keep them, or publish untrimmed. The full, honest account is in the `aot` document below.

## Documentation

Installing the package writes these into your repository at build time (`.zonit/extensions/website/`,
plus editor-specific copies under `.cursor/rules/`, `.github/instructions/` and `.claude/skills/`):

| Document | Covers |
| --- | --- |
| `hosting.md` | `AddWebsite`, `UseWebsite<TApp>`, several Sites in one app, `SiteOptions`, mount ordering |
| `areas.md` | `IWebsiteArea`, `IWebsiteServices`, navigation contributions, the three pipeline hooks |
| `pages.md` | `PageBase` / `PageViewBase<T>` / `PageEditBase<T>`, cancellation, EditForm wiring, auto-save |
| `layouts.md` | `AddWebsiteLayout`, `[LayoutKey]`, `[NoLayout]`, `ZonitRouteView`, precedence |
| `hydration.md` | `WebsiteHydrator`, the built-in bridges, writing your own `IPersistentStateProvider` |
| `permissions.md` | `[RequirePermission]`, `[RequireRole]`, the claim contract, the session cookie |
| `ui-services.md` | Navigation, breadcrumbs, toasts, cookies, `UrlPath.ToHref()` |
| `aot.md` | What is genuinely trim/AOT-safe, what is annotated, what turns off |

## License

MIT.
