# Hosting a Zonit Website

`Zonit.Extensions.Website` turns a plain `WebApplication` into a multi-mount Razor Components
host. Two calls, two phases, and they are **not** interchangeable:

| Phase | Call | Runs | Does |
|---|---|---|---|
| services | `builder.AddWebsite(o => …)` | once | loads `AppData/Settings` **and** registers the whole service graph + every area's `ConfigureServices` |
| middleware | `app.UseWebsite<TApp>(directory, o => …)` | once **per Site** | builds one isolated request pipeline + `MapRazorComponents<TApp>()` |

`AddWebsite` routes nothing. An app that calls only `AddWebsite` serves 404 for every page.

**It also loads configuration.** Both `builder.AddWebsite(…)` and `builder.Services.AddWebsite(…)`
call `AddAppData()`, so every JSON file under `AppData/Settings` is in configuration — the receiver
makes no difference. The services-level overload manages it because the host registers its
`ConfigurationManager` as the `IConfiguration` service and that type is an `IConfigurationBuilder`
too, so the source list is reachable from the collection alone. Opt out with `o.UseAppData = false`;
to keep the loader but change its settings, call `builder.AddAppData(o => …)` first and `AddWebsite`
will leave it alone. See `.zonit/extensions/configuration/appdata.md`.

`AddWebsite` must still run **before `Build()`** for those files to reach Kestrel and the logging
providers, which read configuration while the host is being constructed.

```csharp
using Zonit.Extensions;          // AddWebsite / UseWebsite / AddWebsiteLayout
using Zonit.Extensions.Website;  // SiteOptions, WebsiteMode, IWebsiteArea

var builder = WebApplication.CreateBuilder(args);

builder.AddWebsite(o =>
{
    o.AddArea<ShopArea>();       // registration — ConfigureServices runs here, once
    o.AddArea<AdminArea>();
});

var app = builder.Build();

// Sub-mounts FIRST. See "Mount order" below — this is not a style preference.
app.UseWebsite<App>("/admin", o =>
{
    o.Permission = "admin.access";   // a concrete token — see permissions.md on wildcards
    o.AddArea<AdminArea>();      // mounting — pick a subset per Site
    o.AddArea<ShopArea>();       // same instance, second mount point
});

app.UseWebsite<App>("/", o =>
{
    o.Mode = WebsiteMode.Server;
    o.HttpsRedirection = !builder.Environment.IsDevelopment();
    o.Compression      = !builder.Environment.IsDevelopment();
    o.AddArea<ShopArea>();
    o.MapEndpoints(ep => ep.MapGet("/healthz", () => Results.Ok()));
});

app.Run();
```

## Mount order — non-root Sites must be declared first

The root mount (`"/"` or `""`) does not get a `MapWhen` branch: it *is* the main pipeline, and it
ends in a terminal `app.UseEndpoints(...)`. Any `MapWhen` branch registered afterwards is
unreachable. The framework detects this and throws at startup rather than letting you ship a
silently broken sub-site:

```
InvalidOperationException: app.UseWebsite<App>("/admin", ...) cannot be called after the root
mount (Directory == "/") has been registered. The root mount finishes with a terminal
UseEndpoints, so any later MapWhen branch is unreachable and the sub-mount silently fails
(typical symptom: HTTP 405 on /<sub>/_blazor/negotiate). Declare every non-root mount BEFORE
the root mount, ...
```

If you ever see **HTTP 405 on `/<mount>/_blazor/negotiate`** or 404 on every sub-site page in an
older build, this ordering is the cause.

## The overload set

All three live in namespace `Zonit.Extensions` and extend `WebApplication` (not
`IApplicationBuilder`). The mount path is always the **first positional argument**, typed
`UrlPath` — a string literal converts implicitly.

```csharp
// 1. The one you normally use.
app.UseWebsite<App>("/admin", o => { /* … */ });

// 2. Satellite hosts with a derived options type (see below).
app.UseWebsite<App, PortalSiteOptions>("/panel", o => { /* … */ });

// 3. Low-level: a pre-built SiteOptions. Skips OnConfiguring/OnConfigured entirely.
app.UseWebsite<App>("/admin", prebuiltSiteOptions);
```

There is **no** lambda-only overload, and `SiteOptions.Directory` has an `internal` setter. Both
of these fail to compile from a consumer assembly:

```csharp
app.UseWebsite<App>(o => { /* … */ });                    // CS1501: no overload takes 1 argument
app.UseWebsite<App>("/", o => o.Directory = "/admin");    // CS0200: Directory is read-only
```

The mount path can therefore never desync from the branch's actual path base.

`"/"` and `""` both normalise to `UrlPath.Empty`, i.e. the same root mount.

`TApp` is the root Razor component (usually `App.razor`). Different Sites may use different root
components — useful when a sub-site needs its own `<base href>`.

## SiteOptions

Every toggle is per-Site. Two Sites in one host can disagree on all of them.

| Member | Type | Default | Effect |
|---|---|---|---|
| `Directory` | `UrlPath` | set by the framework | mount prefix; setter is `internal` |
| `Permission` | `string?` | `null` | `RequireAuthorization(value)` on the Razor Components endpoints only |
| `Mode` | `WebsiteMode` | `Server` | `Server`/`Auto` ⇒ `AddInteractiveServerRenderMode()`; `WebAssembly` ⇒ nothing |
| `Hsts` | `bool` | `true` | `UseHsts()` — non-Development only |
| `HttpsRedirection` | `bool` | `true` | `UseHttpsRedirection()` |
| `Compression` | `bool` | `true` | `UseResponseCompression()` (Brotli + gzip) |
| `Proxy` | `bool` | `false` | `UseForwardedHeaders()` — non-Development only |
| `AntiForgery` | `bool` | `true` | `UseAntiforgery()` |
| `SecurityHeaders` | `bool` | `true` | adds `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, masks `Server` |
| `ExceptionHandlerPath` | `string?` | `"/error"` | non-Development only; `null` disables |
| `Areas` | `IReadOnlyList<IWebsiteArea>` | empty | read-only view of what `AddArea<T>()` mounted |

Methods: `AddArea<TArea>()`, `App(Action<IApplicationBuilder>)`,
`Use(Action<IApplicationBuilder>)`, `MapEndpoints(Action<IEndpointRouteBuilder>)`,
`AttachRegistry(WebsiteAreaRegistry)`. `AddArea`/`App`/`Use`/`MapEndpoints` all return
`SiteOptions`, so they chain.

`ExceptionHandlerPath` drives **two** registrations: `UseExceptionHandler("/error")` and
`UseStatusCodePagesWithReExecute("/error/{0}")`. Ship a page routed at `/error` *and* one at
`/error/{code:int}`, or set the property to `null`.

`Permission` is a policy name resolved through `PermissionPolicyProvider`, so a bare permission
token works directly. Use a **concrete** token: a wildcard here becomes the *required* permission,
which only a user holding that exact wildcard satisfies. See
`.zonit/extensions/website/permissions.md`.

## Per-Site pipeline order

`UseWebsite` builds the branch in exactly this order. Anything you add through the hooks lands at
the marked positions.

1. `UsePathBase(prefix)` — non-root mounts only
2. stamp `ICurrentSite` for the request scope
3. `UseDeveloperExceptionPage()` in Development; otherwise `UseExceptionHandler` + `UseStatusCodePagesWithReExecute`
4. non-Development only: `UseForwardedHeaders()` (if `Proxy`), `UseHsts()` (if `Hsts`)
5. `UseHttpsRedirection()` (if `HttpsRedirection`)
6. `UseResponseCompression()` (if `Compression`)
7. security headers (if `SecurityHeaders`)
8. **`IWebsiteArea.App(app)` for each mounted area, then each `SiteOptions.App(...)` hook**
9. `CultureMiddleware` — deliberately before routing so `/pl/home` can be rewritten to `/home`
10. `UseRouting()`
11. `UseAuthentication()` → `UseAuthorization()` → `UseAntiforgery()` (if `AntiForgery`)
12. `CookieMiddleware` → `SessionMiddleware` → `WorkspaceMiddleware` → `ProjectMiddleware` → `TenantMiddleware`
13. **`IWebsiteArea.Use(app)` for each mounted area, then each `SiteOptions.Use(...)` hook**
14. `UseEndpoints`: `MapStaticAssets()`, then `MapRazorComponents<TApp>()` (+ render mode, +
    `AddAdditionalAssemblies`, + `RequireAuthorization(Permission)`), `MapControllers()` /
    `MapRazorPages()` if enabled, then **`IWebsiteArea.MapEndpoints(ep)` and each
    `SiteOptions.MapEndpoints(...)` hook**

`MapStaticAssets()` runs *inside* the branch on purpose. Registering it globally makes every
`/<mount>/_content/...` request 404 under a non-root mount, because the branch has its own
endpoint route builder and `UsePathBase` has already stripped the prefix.

## What AddWebsite already registered — do not repeat it

Calling any of these again after `AddWebsite` is a double-registration bug, not a no-op safety net:

- the five domain cores: `AddCulturesExtension()`, `AddAuthExtension()`, `AddOrganizationsExtension()`, `AddProjectsExtension()`, `AddTenantsExtension()`
- the UI providers: `AddNavigationsExtension()`, `AddBreadcrumbsExtension()`, `AddToastsExtension()`, `AddCookiesExtension()`, `AddLayoutsExtension()`
- `AddAuthorization()`, the permission/role authorization handlers, `PermissionPolicyProvider` (installed with `Replace`), `AddCascadingAuthenticationState()`, the scoped `AuthenticationStateProvider`
- `AddAntiforgery()`, `AddProblemDetails()`, `AddHsts(...)`, `AddResponseCompression(...)`, `Configure<ForwardedHeadersOptions>`, `Configure<WebEncoderOptions>(UnicodeRanges.All)`
- `AddHttpContextAccessor()`, the five prerender→circuit state bridges, `WebsiteMountRegistry`, `ICurrentSite`
- the `"Zonit"` authentication scheme — **only if the app has no `IAuthenticationSchemeProvider` yet**

`UseCookiesExtension()` is legacy: `UseWebsite` already installs `CookieMiddleware` in every
branch, so calling it inside a Zonit host double-populates the cookie snapshot.

Register your own services *after* `AddWebsite` if you want to win, and prefer `TryAdd*` inside
areas. See `.zonit/extensions/website/permissions.md` for the ordering trap around
`AddAuthentication`.

## WebsiteOptions (services-time)

```csharp
builder.Services.AddWebsite(o =>
{
    o.MemoryCache     = true;    // default true  → AddMemoryCache()
    o.RazorComponents = true;    // default true  → AddRazorComponents().AddInteractiveServerComponents()
    o.Controllers     = false;   // default false → AddControllers() + MapControllers() per Site
    o.RazorPages      = false;   // default false → AddRazorPages() + MapRazorPages() per Site
    o.AddArea<ShopArea>();
});
```

`WebsiteOptions.AddArea<TArea>()` requires a **public parameterless constructor** (`new()`
constraint) — areas are data-first POCOs. `SiteOptions.AddArea<TArea>()` has no such constraint,
because it resolves the instance the services phase already created.

`WebsiteOptions.Url` exists and is settable, but nothing in the framework reads it. Do not build
on it.

## Satellite hosts: deriving SiteOptions

A downstream package that ships its own chrome (this is exactly how `Zonit.Dashboard` implements
`app.UseDashboard(...)`) derives `SiteOptions` and forwards to `UseWebsite<TApp, TSiteOptions>`.
The derived type needs a public parameterless constructor.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions;
using Zonit.Extensions.Website;

public sealed class PortalSiteOptions : SiteOptions
{
    public int DrawerWidth { get; set; } = 240;

    // Runs BEFORE the consumer lambda. Seed always-on areas here.
    protected override void OnConfiguring(IServiceProvider services)
    {
        base.OnConfiguring(services);
        AddArea<PortalChromeArea>();   // registry is attached and Directory is set already
    }

    // Runs AFTER the consumer lambda. Snapshot the final state / add late hooks.
    protected override void OnConfigured(IServiceProvider services)
    {
        base.OnConfigured(services);
        var mounts = services.GetRequiredService<PortalMountRegistry>();
        mounts.Register(Directory, Areas, DrawerWidth);
    }
}

public static class PortalHostExtensions
{
    public static WebApplication UsePortal(
        this WebApplication app, UrlPath directory, Action<PortalSiteOptions>? configure = null)
        => app.UseWebsite<PortalApp, PortalSiteOptions>(directory, configure);
}
```

Lifecycle of `UseWebsite<TApp, TSiteOptions>`, in order:

1. `new TSiteOptions()`
2. `AttachRegistry(registry)` — so `AddArea<T>()` works inside the hooks
3. `Directory = directory` (normalised) — so the hooks can read the mount path
4. `OnConfiguring(services)`
5. the consumer's `configure` lambda
6. `OnConfigured(services)`
7. the actual mount

`services` is the **application** `IServiceProvider` (`app.Services`), so resolving a scoped
service inside a hook needs an explicit scope.

The low-level `UseWebsite<TApp>(directory, SiteOptions site)` overload skips steps 1–6 entirely.

Why `OnConfigured` matters: `SiteOptions` is a build-time object that does not survive past the
`UseWebsite` call. Anything a component must read at render time has to be copied into a
singleton there. The framework does exactly this for the base state, via `WebsiteMountRegistry`.

## Reading the active mount at runtime

`ICurrentSite` (scoped) answers "which Site am I rendering under": `IsSet`, `Directory`,
`Permission`, `Areas`, `AreaKeys`. The branch middleware sets it for HTTP requests; the default
implementation self-hydrates from the singleton `WebsiteMountRegistry` when that middleware never
ran — which is the case for the whole SignalR **circuit** scope that owns interactive components.
Without the fallback an `InteractiveServer` page would see zero mounted areas the moment it
hydrates.

`WebsiteMountRegistry.ForMount(path)` matches by longest prefix and falls back to the root mount:

```
ForMount("")             -> root mount
ForMount("/admin")       -> the /admin mount
ForMount("/admin/users") -> the /admin mount
ForMount("/unknown")     -> root mount (if one is registered), otherwise null
```

## Known limitations

- **`AddWebsite` is `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]`.** A host built with
  `EnableTrimAnalyzer` / `PublishTrimmed` / `PublishAot` gets IL2026 + IL3050 at the call site.
  See `.zonit/extensions/website/aot.md`.
- **Prerender→circuit hydration is silently disabled under any `PublishTrimmed` publish**, not
  just `PublishAot`: the five state bridges `AddWebsite` registers are gated on
  `JsonSerializer.IsReflectionEnabledByDefault`, which the SDK turns off, and they log nothing
  when they skip. Identity, culture, workspace, catalog and cookie state then do not survive the
  boundary. Details in `.zonit/extensions/website/hydration.md`.
- **`WebsiteMode.WebAssembly` registers no interactive render mode at all**, and `Auto` registers
  only the *server* one. `AddWebsite` calls `AddRazorComponents().AddInteractiveServerComponents()`,
  and no hook reaches the framework's `MapRazorComponents<TApp>()` convention builder, so a
  WebAssembly Site cannot be completed through `SiteOptions` today. Treat `Server` as the only
  fully-wired value.
- **`SiteOptions.Permission` guards only the Razor Components endpoints** (pages plus the
  `/_blazor` hub endpoints). Minimal-API endpoints registered from `IWebsiteArea.MapEndpoints` or
  `SiteOptions.MapEndpoints`, and the static-assets endpoint, carry no authorization metadata —
  add `.RequireAuthorization("…")` to those endpoints yourself.
- **`WebsiteOptions.Url` is dead.** Nothing reads it.
