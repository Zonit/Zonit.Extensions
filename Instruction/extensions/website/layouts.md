# String-keyed layouts

Blazor picks a layout from `[Layout(typeof(X))]` on the page type — which forces every page to
reference the assembly that owns the layout class. That is fine inside one project and unusable
across a plug-in boundary: an area shipped as its own library cannot name the host's
`MainLayout`.

Zonit adds a string indirection. The page declares *intent* (`[LayoutKey("Auth.LoginBox")]`); the
host decides which concrete `LayoutComponentBase` implements it.

Nothing works until `ZonitRouteView` is wired into the Router — see the next section.

## Register layouts

```csharp
using Zonit.Extensions;   // AddWebsiteLayout lives here

builder.Services.AddWebsite(o => o.AddArea<AuthArea>());

builder.Services.AddWebsiteLayout<LoginLayout>("Auth.LoginBox");
builder.Services.AddWebsiteLayout<ErrorLayout>("Shop.Error");
builder.Services.AddWebsiteLayout<HostMinimalLayout>("Zonit.Minimal");  // overwrite the built-in
```

`AddWebsiteLayout<TLayout>(string key)` constrains `TLayout : LayoutComponentBase` and throws
`ArgumentException` on a null/empty/whitespace key. Keys are **case-insensitive**; convention is
`"Area.Purpose"`. Re-registering a key overwrites it — last writer wins — which is exactly how a
host rebrands a framework or plug-in layout without forking anything.

`AddWebsite` already calls `AddLayoutsExtension()`, which registers `ILayoutRegistry` (singleton),
`ILayoutContext` (scoped) and seeds `"Zonit.Minimal"` → `ZonitMinimalLayout`
(`<div class="zonit-minimal-layout">@Body</div>`). Call `AddLayoutsExtension()` yourself only in a
host that does not call `AddWebsite`.

Namespaces are not uniform — import what you actually use:

| Symbol | Namespace |
|---|---|
| `AddWebsiteLayout<T>`, `AddLayoutsExtension` | `Zonit.Extensions` |
| `LayoutKeyAttribute`, `NoLayoutAttribute`, `ILayoutContext`, `PageBase` | `Zonit.Extensions.Website` |
| `ZonitRouteView`, `ZonitMinimalLayout` | `Zonit.Extensions.Website.Layouts.Components` |
| `ILayoutRegistry` | `Zonit.Extensions.Website.Layouts.Repositories` |

## Wire the Router — mandatory

`[LayoutKey]`, `[NoLayout]` and `PageBase.LayoutKey` are honoured by `ZonitRouteView` and by
nothing else. Replace `RouteView` / `AuthorizeRouteView` in `Routes.razor`:

```razor
@* Components/Routes.razor *@
@using Zonit.Extensions.Website.Layouts.Components

<Router AppAssembly="@typeof(Program).Assembly"
        AdditionalAssemblies="@(new[] { typeof(ShopArea).Assembly })">
    <Found Context="routeData">
        <ZonitRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)">
            <NotAuthorized>
                <h1>403</h1>
                <p>You need additional roles or permissions for this page.</p>
            </NotAuthorized>
        </ZonitRouteView>
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
</Router>
```

Parameters: `RouteData` (required), `DefaultLayout` (`Type?`),
`NotAuthorized` (`RenderFragment<AuthenticationState>?`), `Authorizing` (`RenderFragment?`).
`ZonitRouteView` wraps `AuthorizeRouteView` verbatim, so `[Authorize]`, `[RequirePermission]` and
`[RequireRole]` keep working unchanged.

`AdditionalAssemblies` here is the **client-side** route table. It is a separate list from the
`ComponentsAssembly` values that `UseWebsite` feeds to `MapRazorComponents().AddAdditionalAssemblies(...)`
per Site — keep both in sync or interactive navigation to an area page 404s after the first SSR
render. See `.zonit/extensions/website/areas.md`.

## Static selection on the page

```razor
@page "/login"
@using Zonit.Extensions.Website
@attribute [LayoutKey("Auth.LoginBox")]
@inherits PageBase
```

```razor
@page "/embed/widget"
@using Zonit.Extensions.Website
@attribute [NoLayout]
```

Both attributes are class-level, `Inherited = true`, `AllowMultiple = false`.
`new LayoutKeyAttribute("")` throws `ArgumentException: Layout key must be non-empty.` at type
load, not at render time.

Prefer the static path. It is read from the page **type** before the component is instantiated, so
the first render already has the right chrome — no flicker.

## Dynamic selection: PageBase.LayoutKey

For the rarer case where the layout depends on runtime state, `PageBase` exposes a
`protected string? LayoutKey` over the scoped `ILayoutContext`.

```csharp
public abstract class DualLayoutPage : PageBase
{
    protected override void OnInitialized()
    {
        base.OnInitialized();   // ExtensionsBase wires breadcrumbs + provider subscriptions here

        // Signed-in users get the app shell; guests get the marketing shell.
        LayoutKey = Authenticated.Current.HasValue ? "App.Shell" : "Site.Public";
    }
}
```

Setter semantics — the three values are genuinely different:

| Value | Meaning |
|---|---|
| `null` | render with **no** layout (runtime equivalent of `[NoLayout]`) |
| `""` | fall back to the Site / router `DefaultLayout`, overriding any static `[LayoutKey]` |
| any other string | resolve through `ILayoutRegistry`; a missing key warns and falls back to `DefaultLayout` |

Setting it after the first render costs exactly one extra re-render, and the user sees a brief
flicker. `ZonitRouteView` clears the override automatically when `RouteData.PageType` changes, so
pages never have to undo it on navigation.

The getter is `LayoutContext.HasOverride ? LayoutContext.Key : null` — it returns `null` both for
"no override active" and for "override = no layout". Use `LayoutContext.HasOverride` /
`LayoutContext.IsNoLayout` when you need to tell them apart.

The underlying `ILayoutContext` (scoped, one per circuit) is also injectable directly:
`HasOverride`, `Key`, `IsNoLayout`, `SetKey(string?)`, `ClearOverride()`, `event Action? OnChange`.
`OnChange` fires only on an effective state transition.

## Precedence

`ZonitRouteView` resolves in this order; the first match wins.

| # | Signal | Result |
|---|---|---|
| 1 | `ILayoutContext.HasOverride && IsNoLayout` | no layout |
| 2 | `ILayoutContext.HasOverride && Key` | `ILayoutRegistry.TryResolve(Key)`; empty key ⇒ `DefaultLayout` |
| 3 | `[NoLayout]` on the page type | no layout |
| 4 | `[LayoutKey("…")]` on the page type | `ILayoutRegistry.TryResolve(key)` |
| 5 | `[Layout(typeof(X))]` on the page type | handled by the wrapped `AuthorizeRouteView` |
| 6 | `DefaultLayout` parameter | the Site / router fallback |

Rows 1–4 are resolved by `ZonitRouteView` itself; rows 5–6 are Blazor's own behaviour, which
`ZonitRouteView` deliberately does not intercept.

## Missing keys never crash

An unregistered key falls back to `DefaultLayout` and logs, once per `(key, page type)` pair for
the lifetime of the process:

```
warn: Zonit.Extensions.Website.Layouts.Components.ZonitRouteView[0]
      Layout key 'Shop.Main' is not registered (page Shop.Pages.Orders); falling back to default
      layout. Register it via services.AddWebsiteLayout<TLayout>("Shop.Main").
```

That is deliberate: a plug-in installed before its layout provider should degrade, not take the
host down. There is no startup validation of layout keys today.

Inspect what is actually registered with `ILayoutRegistry.Keys` (`IReadOnlyCollection<string>`,
unordered) and `TryResolve(key, out Type? layoutType)`.

## Known limitations

- **`[NoLayout]` does not beat a `[Layout]` attribute.** `ZonitRouteView` implements "no layout"
  by passing no `DefaultLayout` down to `AuthorizeRouteView`; Blazor then still applies
  `[Layout(typeof(X))]` found on the page type or, because `LayoutAttribute` is inherited, on any
  base class. Treat `[NoLayout]` and `[Layout]` as mutually exclusive at the page level — nothing
  enforces it. The XML doc shipped on `NoLayoutAttribute` overstates this ("opts out of every
  layout … including Blazor's `[Layout]` attribute on any base class"); the code does not do that.
- **`ILayoutRegistry` is upsert-only.** There is no unregister and no "fail on duplicate key"
  mode, so a plug-in registering a key a host already owns silently wins if it runs later.
- **`LayoutSeed` carries an unsuppressed IL2069** under a Native AOT publish (the positional
  record parameter behind the `[DynamicallyAccessedMembers]` property is unannotated). The
  in-repo trim analyzers do not report it; ILC does. See `.zonit/extensions/website/aot.md`.
