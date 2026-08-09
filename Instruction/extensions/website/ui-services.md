# UI services: navigation, breadcrumbs, toasts, cookies

Four small scoped providers registered by `AddWebsite()`. All four are already exposed as
`protected` properties on `PageBase` (`Toast`, `Cookie`, `BreadcrumbsProvider`) — inject them
explicitly only in components that do not derive from it.

| Service | Interface | Lifetime | Registered by |
| --- | --- | --- | --- |
| Navigation | `INavigationProvider` | **Scoped** | `AddNavigationsExtension()` |
| Breadcrumbs | `IBreadcrumbsProvider` | Scoped | `AddBreadcrumbsExtension()` |
| Toasts | `IToastProvider` | Scoped | `AddToastsExtension()` |
| Cookies | `ICookieProvider` | Scoped | `AddCookiesExtension()` |

`AddWebsite()` calls all four. Do not call them again.

| Trap | What actually happens |
| --- | --- |
| A full URL in `NavItem.Url` | **Throws at startup.** `UrlPath` rejects absolute addresses by design. Put it in `NavItem.External` and render through `item.ToHref()`. |
| `item.Url.ToHref()` in a renderer | Skips the external destination entirely. Always call `item.ToHref()` / `link.ToHref()`. |
| Marking an external link active | It never can be — compare only when `item.IsMatchable()`. |
| `T(…)` around a nav title | Redundant. `INavigationProvider.Get()` already translates titles and tooltips. |
| `href="/orders"` under a non-root mount | Bypasses `<base href>` and lands on the host root. See below. |

## `UrlPath.ToHref()` — read this before anything else

Extension method in `Zonit.Extensions.Website` over the `UrlPath` value object from
`Zonit.Extensions`.

```csharp
public static string ToHref(this UrlPath path);
```

It strips the leading `/`. That is the whole implementation, and it is mandatory under a non-root
mount.

| Input | `ToHref()` | `ToAbsolutePath()` |
| --- | --- | --- |
| `"/orders"` | `"orders"` | `"/orders"` |
| `"orders"` | `"orders"` | `"/orders"` |
| `UrlPath.Empty` | `""` | `"/"` |

An area writes `new NavItem { Url = "/components" }`, and `UrlPath` preserves what the author
wrote. Emitting `href="/components"` produces an **absolute** path in HTML, which bypasses
`<base href>`. On a Site mounted at `/dashboard` the user clicks the dashboard's link and lands on
the *root* site's `/components` — same route, wrong chrome. `href="components"` is relative, so
the browser resolves it against `<base href="/dashboard/">`.

```razor
@using Zonit.Extensions
<a href="@item.Url.ToHref()" target="@item.Target.ToHtml()">@item.Title</a>
```

Use `ToAbsolutePath()` only where a rooted path is genuinely required — cross-mount redirects,
server-side `Location:` headers.

## Navigation

```csharp
public interface INavigationProvider
{
    void Add(NavGroup model, string? areaKey = null);
    void Clear(string? areaKey = null);
    IReadOnlyList<NavGroup> Get(string areaKey, string? position = null);
    void Refresh(string? areaKey = null);
    event Action<string?>? OnChanged;      // argument: area key, null = all
}
```

The data lives in a process-wide singleton behind the scoped facade, so an `Add` from one circuit
is visible to every request and circuit. `OnChanged` is forwarded to that singleton, so it reaches
subscribers in all scopes.

`Get` returns static contributions from each area's `IWebsiteArea.Navigation` plus runtime
additions, ordered by `NavGroup.Order`, filtered by `position` when supplied, and **filtered to
areas mounted on the active Site** (via `ICurrentSite`). Outside a registered mount no Site filter
applies and everything is visible.

Three things it does not do:

- **No permission filtering.** `NavGroup.Permission` and `NavItem.Permission` are data the UI may
  consult; nothing in *this* package hides an entry based on them. The renderer is expected to,
  and `Zonit.Dashboard` does — it drops any node whose permission the current identity fails,
  wildcards included, and a group left with no visible children goes with them. A host rendering
  its own nav markup must do the same, or its menu will offer links that answer 401.
- **`Add` without an `areaKey` is unreachable.** The store keys unkeyed additions under the empty
  string, while `Get` rejects a null/whitespace area key outright. Always pass `areaKey`.
- **No persistence.** Runtime additions are in-memory and lost on restart.

```csharp
public class Sidebar : PageBase
{
    [Inject] private INavigationProvider Nav { get; set; } = default!;

    private IReadOnlyList<NavGroup> Groups => Nav.Get("reports", "sidebar");

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        Nav.OnChanged += OnNavChanged;
        return Task.CompletedTask;
    }

    private void OnNavChanged(string? areaKey) => InvokeAsync(StateHasChanged);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Nav.OnChanged -= OnNavChanged;
        base.Dispose(disposing);
    }
}
```

`NavGroup` / `NavItem` are `init`-only classes whose `Title`, `Url` and `Permission` are value
objects with implicit conversion from `string`:

```csharp
new NavGroup
{
    Title    = "Reports",             // Title VO
    Position = "sidebar",             // free-form; the layout decides what it means
    Order    = 20,
    Expanded = true,
    Children =
    [
        new NavItem { Title = "Daily",   Url = "/reports/daily",   Order = 1 },
        new NavItem { Title = "Monthly", Url = "/reports/monthly", Order = 2, Badge = "NEW" },
    ],
}
```

`NavItem` also carries `Icon`, `Target` (`Self`/`Blank`/`Parent`/`Top`, with `.ToHtml()`),
`Match`, `Children`, `Badge`, `BadgeColor`, `Tooltip`, `Disabled`. `NavGroup` adds `Link`
(`LinkModel`), `Groups`, and a free-form `Settings` bag.

### Seeding navigation from a hosted service

**`INavigationProvider` is scoped as of 10.0.0-preview.10** (it was a singleton before). A
singleton `IHostedService` can no longer take it in its constructor — the generic host's scope
validation throws
`Cannot resolve scoped service 'Zonit.Extensions.Website.INavigationProvider' from root provider`.
Inject `IServiceScopeFactory` instead:

```csharp
internal sealed class NavSeeder(IServiceScopeFactory scopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var nav = scope.ServiceProvider.GetRequiredService<INavigationProvider>();

        nav.Add(new NavGroup { Title = "Reports", Position = "sidebar", Order = 20 },
                areaKey: "reports");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

The addition lands in the process-wide store and survives the scope's disposal. Contributing
navigation from an area (`IWebsiteArea.Navigation`) is the declarative alternative and needs no
scope at all — see `.zonit/extensions/website/areas.md`.

## Navigation: in-site versus external links

`NavItem.Url` and `LinkModel.Url` are `UrlPath`, which **rejects absolute addresses by design** —
assigning `"https://twitter.com/acme"` throws from the value object's implicit conversion, at
startup, with nothing pointing at the menu entry that caused it. That is the type doing its job.

External destinations go in `External` (a `Url`):

```csharp
new NavItem { Title = "GitHub", External = "https://github.com/acme", Target = Target.Blank }
```

Render with the extension, never by reaching for one property:

```razor
<a href="@item.ToHref()" target="@item.Target">@item.Title.Value</a>
```

`ToHref()` emits an external address verbatim and an in-site path *relative*, so the latter
resolves against `<base href>` and picks up the mount and the culture prefix. `IsMatchable()`
tells a renderer whether the item can be compared against the current route to be marked active —
an external link never can, and skipping the check highlights a social link whose path collides.

`Target.Blank` is not assumed for external links: "external" and "should open in a new tab" are
two different decisions.

## Navigation is translated for you

`INavigationProvider.Get()` runs every title and tooltip through the translation registry, so a
menu is translated once for every UI layer and no author writes `T(…)` at the declaration site.

`Translate` defaults to `true`; set it to `false` on the individual entries that are exceptions —
a brand or product name that must never be looked up:

```csharp
new NavItem { Title = "Home page" }                                   // translated
new NavItem { Title = "GitHub", External = "https://github.com/acme",
              Target = Target.Blank, Translate = false }              // left alone
```

Text with no rendition falls through to itself, so the flag is rarely load-bearing — it matters
when a brand name happens to collide with a translation key. The same flag exists on `NavGroup`
(covering the group's own title and tooltip, not its children) and on `PageMeta` (covering
`Title` and `Description`).

`NavItem`, `NavGroup` and `LinkModel` are records — the provider rebuilds the tree with `with`
rather than mutating the shared registry copy, which would pin the first request's language onto
the whole process.

## Breadcrumbs

```csharp
public interface IBreadcrumbsProvider
{
    void Initialize(IList<BreadcrumbsModel>? model);
    IReadOnlyList<BreadcrumbsModel>? Get();
    event Action? OnChange;
}
```

`Get()` returns a copy, so a renderer cannot mutate the trail. `Initialize` raises `OnChange`,
which `ExtensionsBase` subscribes to for a UI-only re-render.

```csharp
public class BreadcrumbsModel
{
    public Title    Text     { get; init; }
    public UrlPath  Href     { get; init; }   // empty = non-clickable
    public bool     Disabled { get; init; }
    public string?  Icon     { get; init; }
    public string?  Template { get; set; }    // host may render a component for this slot

    public BreadcrumbsModel() { }
    public BreadcrumbsModel(Title text, UrlPath href = default, bool disabled = false, string? icon = null);
}
```

Most pages declare crumbs via `ShowBreadcrumbs` + `Breadcrumbs` on `PageBase`; call
`Initialize` directly only when a crumb depends on loaded data. Both patterns and the ordering
trap are in `.zonit/extensions/website/pages.md`.

## Toasts

```csharp
public interface IToastProvider
{
    IReadOnlyList<ToastEntry> Toasts { get; }
    event Action? OnChange;

    void Add(ToastType taskType, string message, params object[]? args);
    void Remove(Guid id);
    void Clear();

    // default interface methods
    void AddNormal(string message, params object[]? args);
    void AddInfo(string message, params object[]? args);
    void AddSuccess(string message, params object[]? args);
    void AddWarning(string message, params object[]? args);
    void AddError(string message, params object[]? args);
}
```

`ToastType` is `Normal | Info | Success | Warning | Error`. `ToastEntry` is
`record (Guid Id, ToastType Type, string Message, DateTime CreatedAt)` — `Message` is already
formatted.

```razor
<button @onclick="@(() => Toast.AddSuccess(T("Saved {0}", Model.Name)))">@T("Save")</button>
```

Three points that catch people:

- **`Add*` does not translate.** When `args` is non-empty the message goes through
  `string.Format(CultureInfo.CurrentCulture, …)`; the string itself is passed through untouched.
  Translate first with `T(...)`, as above.
- **The `Add*` helpers are default interface methods.** They exist only on `IToastProvider`.
  Inject the interface, never the concrete `ToastService` — the shorthands are invisible there.
- **The queue is scoped** (per circuit / per request) so one host component can render everything
  raised anywhere on the page. A toast raised during static SSR is not carried into the circuit.

### The host component

```razor
@* once, in your layout *@
<ZonitToasts />
```

`ZonitToasts` is in namespace `Zonit.Extensions.Website`. It subscribes to `OnChange`, renders
one `<div class="zonit-toast zonit-toast--{type}">` per entry with a dismiss button wired to
`Remove`, and unsubscribes on dispose.

**No CSS ships with the package.** The component emits `zonit-toasts`, `zonit-toast`,
`zonit-toast--info|success|warning|error|normal`, `zonit-toast__message` and
`zonit-toast__close`, and nothing styles them. Write those rules yourself, or render
`Toast.Toasts` with your own component (e.g. MudBlazor's `MudSnackbar`) and skip `<ZonitToasts />`.

There is also **no auto-dismiss timer** — entries stay until `Remove` or `Clear`.

## Cookies

```csharp
public interface ICookieProvider
{
    CookieModel? Get(string key);                  // case-insensitive
    List<CookieModel> GetCookies();
    Task RefreshAsync();

    CookieModel        Set(string key, string value, TimeSpan lifetime);
    Task<CookieModel>  SetAsync(string key, string value, TimeSpan lifetime);
    CookieModel        Set(string key, string value, DateTime expires);
    Task<CookieModel>  SetAsync(string key, string value, DateTime expires);
    CookieModel        Set(CookieModel model);
    Task<CookieModel>  SetAsync(CookieModel model);

    // default interface methods — 1-year lifetime
    CookieModel        Set(string key, string value);
    Task<CookieModel>  SetAsync(string key, string value);
}
```

**`Set` and `SetAsync` are not two spellings of the same thing.**

| | Writes the in-memory snapshot | Writes `document.cookie` |
| --- | --- | --- |
| `Set(...)` | yes | **no** |
| `SetAsync(...)` | yes | yes, via JS interop |

`Set` only records the cookie in the scoped repository, so `Get` sees it for the rest of the
request or circuit and the browser never hears about it. Use `SetAsync` for anything the browser
must keep.

`SetAsync` catches `InvalidOperationException` during prerender / static SSR (JS interop not yet
available) and `JSDisconnectedException` on a dead circuit. In both cases the value is still in
the repository, but the browser did **not** get it — re-issue from
`OnAfterRenderAsync(firstRender: true, …)`.

```razor
@inherits PageBase

@foreach (var c in Cookie.GetCookies())
{
    <p>@c.Name = @c.Value</p>
}

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender, CancellationToken cancellationToken)
    {
        if (firstRender)
        {
            await Cookie.RefreshAsync();
            StateHasChanged();
        }
    }

    private Task UseDarkAsync()
        => Cookie.SetAsync("theme", "dark", TimeSpan.FromDays(365));
}
```

`RefreshAsync()` re-reads `document.cookie` and rebuilds the snapshot. It is the reliable way to
populate the jar in an interactive circuit, whose scope the `CookieMiddleware` never ran against
(see `.zonit/extensions/website/hydration.md`; `CookieStateBridge` covers the same gap
automatically when hydration is enabled). It is a no-op during prerender.

More caveats worth knowing:

- **CSP.** The JS helper is installed once per circuit by `eval`-ing a compile-time constant
  string. A host shipping a strict CSP without `'unsafe-eval'` must replace `ICookieProvider`
  with its own implementation backed by a real `.js` file. Nothing user-controlled is ever spliced
  into that source — cookie name, value and every attribute cross as separate JS interop
  arguments — but the `eval` itself is what a strict policy blocks.
- **`HttpOnly` cannot be set from the browser.** `CookieModel.HttpOnly` exists but is deliberately
  not forwarded to JS, because browsers ignore it on a client-side write. Issue real HttpOnly
  cookies from the server with a `Set-Cookie` header.
- **HttpOnly cookies never appear** in `GetCookies()` after `RefreshAsync()` — JS cannot see them.
  That is a browser guarantee, not a bug here.
- `CookieModel` defaults: `Path = "/"`, `Secure = true`, `HttpOnly = false`, `SameSite = false`
  (true emits `SameSite=Strict`), `Session = false`.
- `CookieService` captures the repository's list reference in its constructor and
  `ICookiesRepository.Initialize` replaces that list. Resolve `ICookieProvider` after
  `<WebsiteHydrator />` has initialised — which is the normal order when the hydrator precedes
  `<Routes />` — or call `RefreshAsync()`.

## Cache-busting RCL assets

```razor
<link href="@Assets.Versioned("_content/MudBlazor/MudBlazor.min.css")" rel="stylesheet" />
```

`AssetVersioning.Versioned(this ResourceAssetCollection, string path)` resolves the canonical
`@Assets[...]` URL and appends `?v={assemblyVersion}` when the path matches
`_content/{AssemblyName}/…`. Non-RCL paths pass through unchanged, so framework files keep their
fingerprint. Only needed for RCLs that do not opt into `StaticWebAssetFingerprintPattern`;
MudBlazor 9.4+ fingerprints its own assets, so plain `@Assets[...]` is enough there.

## See also

- `.zonit/extensions/website/pages.md` — these providers as `PageBase` members
- `.zonit/extensions/website/areas.md` — declaring navigation from an area
- `.zonit/extensions/website/hosting.md` — Sites, mounts and `ICurrentSite`
- `.zonit/extensions/website/hydration.md` — why cookies look empty in a circuit
- `.zonit/extensions/core/value-objects.md` — `UrlPath`, `Title`, `Permission`

