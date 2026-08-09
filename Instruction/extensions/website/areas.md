# Writing an Area

An **Area** is a plug-in unit of a Zonit Website: its own Razor pages, its own services, its own
navigation, optionally its own middleware and minimal-API endpoints. Areas are how a feature
library gets mounted into a host without the host referencing its internals.

Two interfaces, both in `Zonit.Extensions.Website`:

| Interface | Runs | Frequency |
|---|---|---|
| `IWebsiteServices` | inside `builder.Services.AddWebsite(o => o.AddArea<T>())` | **once** per process |
| `IWebsiteArea` | inside `app.UseWebsite<TApp>(dir, o => o.AddArea<T>())` | **once per Site** the area is mounted on |

That split is the whole point. `AuthArea` can be mounted at `/` *and* at `/admin`, but its
`ConfigureServices` must not run twice against the same container.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website;

public sealed class ShopArea : IWebsiteArea, IWebsiteServices
{
    // Stable, unique. Used by INavigationProvider.Get(areaKey) and ICurrentSite.AreaKeys.
    public string Key => "shop";

    // Static navigation contributed by this area.
    public IReadOnlyList<NavGroup> Navigation { get; } =
    [
        new NavGroup
        {
            Title = "Shop", Order = 10, Position = "sidebar", Expanded = true,
            Children =
            [
                new NavItem { Title = "Orders",   Url = "/orders",   Order = 1, Match = false },
                new NavItem { Title = "Products", Url = "/products", Order = 2 },
            ],
        },
    ];

    // DI half — runs once, at services time. TryAdd* so a host can override.
    public void ConfigureServices(IServiceCollection services)
        => services.TryAddScoped<IOrderService, OrderService>();

    // Endpoints inherit the Site's PathBase: mounted at /admin this is POST /admin/orders/import.
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders/import", () => Results.Ok());
}
```

Areas need a **public parameterless constructor** — `WebsiteOptions.AddArea<TArea>()` carries a
`new()` constraint and does `new TArea()`. They are data-first POCOs, not DI services; nothing is
injected into them. Anything they need at runtime is resolved from the `IApplicationBuilder` /
`IEndpointRouteBuilder` handed to the hooks, or injected into their components.

## Registration and mounting are separate calls

```csharp
builder.Services.AddWebsite(o =>
{
    o.AddArea<ShopArea>();     // registers: instantiates + runs ConfigureServices + stores instance
    o.AddArea<AdminArea>();
});

var app = builder.Build();

app.UseWebsite<App>("/admin", o =>
{
    o.AddArea<AdminArea>();    // mounts: pulls the SAME instance out of WebsiteAreaRegistry
    o.AddArea<ShopArea>();
});

app.UseWebsite<App>("/", o => o.AddArea<ShopArea>());
```

Mount an area that was never registered and you get, at startup:

```
InvalidOperationException: Area 'GhostArea' is referenced from app.UseWebsite() but was never
registered with builder.Services.AddWebsite(o => o.AddArea<GhostArea>()). Add it at services-time
so its IWebsiteServices.ConfigureServices runs against the DI container before app.Build().
```

The fix is always the same: add the matching `o.AddArea<T>()` to the `AddWebsite` call. There is
no "auto-register on mount" — that would run `ConfigureServices` after `app.Build()`, against a
container that is already sealed.

Mount the same area twice on one Site and you get:

```
InvalidOperationException: Area with key 'shop' is already mounted on Site ''.
```

(The Site is named by its `Directory`; the root mount prints as an empty string.) The check is on
`Key`, case-insensitively — two *different* area types that both return `"shop"` collide too.
Registration, by contrast, is keyed by `Type` and is idempotent: registering the same type twice
is a no-op and the first instance wins.

## The three pipeline hooks

All three are default-implemented no-ops, so implement only what you need.

| Hook | Signature | Position in the Site branch | Typical use |
|---|---|---|---|
| `App` | `void App(IApplicationBuilder app)` | before routing and auth, right after `UsePathBase` + `ICurrentSite` | libraries that must wrap every request from byte zero — image pipelines, custom static files, rewrites |
| `Use` | `void Use(IApplicationBuilder app)` | after `UseAuthentication`/`UseAuthorization` and after the Cookie/Session/Workspace/Project/Tenant middlewares | guards that need an authenticated principal or a hydrated workspace, audit logging |
| `MapEndpoints` | `void MapEndpoints(IEndpointRouteBuilder endpoints)` | inside the branch's `UseEndpoints`, **after** `MapRazorComponents<TApp>()` | minimal APIs: login POST, OAuth callback, webhooks |

Per Site, every mounted area's hook runs in mount order, and the area hooks run **before** the
matching `SiteOptions.App(...)` / `Use(...)` / `MapEndpoints(...)` hooks. The full ordering is in
`.zonit/extensions/website/hosting.md`.

```csharp
public sealed class AuditArea : IWebsiteArea
{
    public string Key => "audit";

    public void Use(IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            // Identity is already hydrated here — SessionMiddleware ran upstream.
            var who = ctx.User.Identity?.Name ?? "anonymous";
            ctx.Response.Headers["X-Audit-User"] = who;
            await next();
        });
}
```

Because a mounted area's hooks run once **per Site**, an area mounted at `/` and at `/admin`
installs its middleware twice — once in each branch. That is correct (the branches are separate
pipelines) but it means the hook body must not mutate process-wide state.

## ComponentsAssembly and Razor page discovery

`ComponentsAssembly` is a default-implemented member returning `GetType().Assembly`. Per Site the
framework collects it from every mounted area, drops nulls and drops `typeof(TApp).Assembly`, and
passes the distinct remainder to `MapRazorComponents<TApp>().AddAdditionalAssemblies(...)`.

The host assembly is filtered out because `MapRazorComponents<TApp>()` already treats it as the
default and `AddAdditionalAssemblies` rejects duplicates with *"Assembly already defined"* — which
is exactly what would happen when an area lives in the same assembly as `App.razor`.

Override it only when your Razor components live in a different assembly than the area class:

```csharp
public sealed class ShopArea : IWebsiteArea
{
    public string Key => "shop";
    public System.Reflection.Assembly ComponentsAssembly => typeof(Shop.Pages.Orders).Assembly;
}
```

This feeds the *endpoint* side only. The client-side `<Router>` in `Routes.razor` has its own
`AdditionalAssemblies` parameter that you must keep in sync — see
`.zonit/extensions/website/layouts.md`.

## Contributing navigation

`Navigation` is a `IReadOnlyList<NavGroup>` (default: empty). It is read once at startup, when
the singleton navigation store is built, so build it as an initialised property — not something
that depends on request state.

```csharp
new NavGroup
{
    Title    = "Shop",        // Title VO — throws past 60 graphemes, so keep labels short
    Icon     = "shopping_cart",
    Position = "sidebar",     // free-form; the layout decides what positions exist
    Order    = 10,
    Expanded = true,
    Permission = "shop.read", // data only — the framework does NOT filter on it
    Children =
    [
        new NavItem { Title = "Orders", Url = "/orders", Order = 1, Match = false,
                      Badge = "12", BadgeColor = NavBadgeColor.Info },
    ],
}
```

Reading it back, from a component or service:

```csharp
IReadOnlyList<NavGroup> sidebar = navigation.Get("shop", position: "sidebar");
IReadOnlyList<NavGroup> all     = navigation.Get("shop");   // every position
navigation.Add(new NavGroup { Title = "Live", Position = "sidebar" }, areaKey: "shop");
```

Things worth knowing before you write a menu renderer:

- `Get(areaKey, position)` merges the static area contributions with runtime `Add(...)` calls,
  filters by `Position` when given, and orders by `Order`. Runtime additions are process-wide and
  survive the scope that added them.
- `Get` returns **empty** when `ICurrentSite.IsSet` is true and the area is not mounted on the
  active Site. That is the per-Site filter; outside any registered mount the filter is skipped and
  everything is visible.
- `NavGroup.Permission` / `NavItem.Permission` are **data the UI may consult**. Nothing in the
  framework hides an item based on them.
- `NavItem.Url` is a `UrlPath` and keeps its leading `/`. Render it through
  `urlPath.ToHref()` (`Zonit.Extensions.Website`), which strips the slash so `<base href>`
  resolution works. Emitting `href="/orders"` verbatim under a `/admin` mount navigates to the
  *root* site's `/orders`. See `.zonit/extensions/website/ui-services.md`.
- Two areas sharing the same `Key` have their static navigation merged under that key by the
  store — even though mounting both on one Site is rejected.

## Contributing layouts

An area registers its own layouts from `ConfigureServices` — there is no separate hook, because
`AddWebsiteLayout<T>(key)` is an `IServiceCollection` extension and the layout registry is a
singleton keyed by string:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddWebsiteLayout<ShopCheckoutLayout>("Shop.Checkout");
    services.AddWebsiteLayout<ShopMinimalLayout>("Zonit.Minimal");   // overwrite the built-in
}
```

Runs once per process, not per Site, which is what you want: the registry maps a key to a type and
nothing about that mapping is per-mount. Pages then reference the key as a string —
`[LayoutKey("Shop.Checkout")]` — so no page needs an assembly reference to the layout that wraps
it.

Two caveats. Registration is **last-writer-wins** on the key, and area order is the order of
`AddArea<T>()` calls at services time, so two areas claiming one key resolve by registration order
with no diagnostic. And which layout is the Site's *default* stays a Site decision —
`SiteOptions.Document.DefaultLayoutKey` — because the same area mounted at `/` and `/admin`
legitimately wants different chrome.

## Contributing document assets

`ConfigureDocument(IDocumentAssets document)` (default: no-op) appends this area's stylesheets,
scripts, preconnects, meta tags and head / body-end components to the document shell of **every
Site that mounts it**. It runs once per mount, after the Site's own declarations, in area
registration order.

```csharp
public sealed class SignalsArea : IWebsiteArea
{
    public string Key => "signals";

    // Write the prefix out. Do NOT derive it from the assembly name — see below.
    private const string Content = "_content/signals";

    public void ConfigureDocument(IDocumentAssets document) => document
        .AddStylesheet($"{Content}/css/signals.css")
        .AddScript($"{Content}/js/signals.js")
        .AddHeadComponent<SignalsPreload>();
}
```

Without this hook the host has to list every plug-in's assets in its own shell, which turns the
host document into a manifest of everything installed and makes installing an area two edits in
two repositories. Forgetting the second edit fails silently: the component renders, nothing 404s,
nothing logs, the feature is simply unstyled or inert.

**The surface is append-only.** `IDocumentAssets` is a strict subset of `DocumentOptions`: an area
cannot set `Favicon`, `DefaultLayoutKey`, `ImportMap` or `ScopedStyles`. Those are Site-wide
verdicts — the last area to touch one would silently win, the result would depend on mount order,
and the host that configured the value would never see it change.

**Ordering.** Site declarations first, then areas. So base stylesheets (Tailwind, a component
library) belong on `SiteOptions.Document` and an area's sheet cascades over them. The corollary:
a Site cannot out-cascade an area it mounts, so make host rules specific rather than relying on
source order.

**Asset paths.** The prefix is whatever the RCL actually serves under, which is
`_content/{AssemblyName}` *only* while the project leaves `StaticWebAssetBasePath` alone. A project
that sets it — the supported way to keep a plug-in's name out of the rendered page — serves its
files somewhere else entirely, and a key the static-asset manifest does not recognise comes back
from `@Assets` unfingerprinted. `AppBase` logs a warning once per URL when that happens; it is not
an error, just a silently lost cache-buster. Pass `fingerprint: false` for absolute URLs (a CDN, a
font host) so the lookup is skipped instead of warning on every boot.

## Known limitations

- **`IWebsiteServices` is only honoured on the area class itself.** `AddArea<TArea>()` does
  `new TArea()` and then tests `area is IWebsiteServices`. Putting `ConfigureServices` on a
  sibling class means it never runs — implement both interfaces on the same type.
- **`INavigationProvider` is scoped**, so it cannot be constructor-injected into a singleton
  `IHostedService`. In Development (where scope validation is on) `builder.Build()` throws
  `InvalidOperationException: Cannot consume scoped service
  'Zonit.Extensions.Website.INavigationProvider' from singleton
  'Microsoft.Extensions.Hosting.IHostedService'.` Seed from a scope instead — the data lands in
  the process-wide store and outlives it:

  ```csharp
  internal sealed class NavSeeder(IServiceScopeFactory scopes) : IHostedService
  {
      public Task StartAsync(CancellationToken ct)
      {
          using var scope = scopes.CreateScope();
          scope.ServiceProvider.GetRequiredService<INavigationProvider>()
               .Add(new NavGroup { Title = "Live", Position = "sidebar" }, areaKey: "shop");
          return Task.CompletedTask;
      }
      public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
  }
  ```

- **Endpoints from `MapEndpoints` are not covered by `SiteOptions.Permission`.** The Site-level
  policy is applied to the `MapRazorComponents` convention builder only, so an area endpoint on a
  permission-gated mount is anonymous unless you call `.RequireAuthorization(...)` on it yourself.
- **The Website source generator mishandles some view-model shapes** used by area pages:
  `init`-only properties (records) and `required` members produce a compile error in the
  consumer's build, and properties inherited from a base class are silently missing from the
  generated metadata. Details in `.zonit/extensions/website/pages.md`.
