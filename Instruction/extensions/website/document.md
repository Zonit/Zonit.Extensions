# Document shell — AppBase, DocumentOptions, appearance, switchers

The HTML document around the routed page: who emits it, how to extend it, and why the colour
scheme never touches the server.

## Read this before you write code

| Trap | What actually happens |
| --- | --- |
| Copying an `App.razor` from a template | Usually wrong under a non-root mount: a hard-coded `<base href="/">` sends every relative link to the host root, and a missing `{App}.styles.css` link silently disables every `*.razor.css` in the project. `AppBase` derives both. |
| `app.UseWebsite("/", …)` with no areas | Works — the entry assembly is always scanned for pages. Before that fix a Site with no areas produced **zero page endpoints** and 404'd everything. |
| Rendering the theme server-side | Makes every HTML response vary by cookie and costs the Site its shared-cache hit rate. Render both states and let CSS pick. |
| `ThemeSwitcherBase.Current` in markup | Same problem — it reads the cookie. Exposed for analytics and settings forms, not for branching the page. |
| Sub-mount after the root mount | Throws. The root branch ends in a terminal `UseEndpoints`, so any later `MapWhen` is unreachable. Declare `/admin` before `/`. |

## The shell

```csharp
app.UseWebsite("/", o =>          // no type argument, no App.razor, no subclass
{
    o.Mode = WebsiteMode.Static;  // Server (default) | WebAssembly | Auto | Static
    o.Document
        .AddPreconnect("https://fonts.gstatic.com", crossOrigin: true)
        .AddStylesheet("_content/Acme.Web/app.css")
        .AddScript("js/analytics.js", defer: true)
        .AddMeta("theme-color", "#0b0b0b")
        .SetFavicon("favicon.svg", "image/svg+xml")
        .AddHeadComponent<StructuredData>()
        .AddBodyEndComponent<ConsentBanner>();

    o.Document.DefaultLayoutKey = "Public.Main";
});
```

`WebsiteMode.Static` is server rendering plus enhanced navigation — no circuit, no WebAssembly
payload. The right mode for content pages; `@onclick` and the state bridges do nothing there.

`AppBase` emits, in order: `<html lang>` from the active culture, `charset`, `viewport`,
`<base href>` from `Request.PathBase` (mount **and** culture segment, trailing slash mandatory),
the blocking appearance script, preconnects, metas, stylesheets, the scoped-CSS bundle, favicon,
`ImportMap`, head components, `HeadOutlet`; then the state bridge (only when there is an
interactive pass), the router, `blazor.web.js`, declared scripts, body-end components.

### Extending, in increasing order of power

1. `SiteOptions.Document` — covers most of what a hand-written shell contains.
2. `AddHeadComponent<T>()` / `AddBodyEndComponent<T>()` for anything needing Razor or services.
3. Derive from `AppBase` and override a virtual: `Lang`, `BaseHref`, `PageRenderMode`,
   `BuildRoutes`, `Hydrate`, `HeadStart`, `HeadEnd`, `BodyStart`, `BodyEnd`, `Document`.
4. Pass your own root component to `UseWebsite<TApp>` — nothing here is mandatory.

Nothing the shell emits names a framework or a product. Cookie names and JS globals are visible in
developer tools, and a recognisable one advertises which open-source stack to look up advisories
against.

## Appearance (light / dark)

The scheme **never reaches the server**. A blocking inline script stamps it onto `<html>` before
the first paint, reading the cookie and falling back to the tenant's default, then to
`prefers-color-scheme`.

```csharp
o.Appearance.Attribute  = "data-theme";   // "class" drives Tailwind's stock darkMode: 'class'
o.Appearance.CookieName = "theme";
o.Appearance.GlobalName = "__theme";
o.Appearance.Enabled    = true;
```

Which scheme is the **default** is a branding decision and lives in the tenant:
`Tenant.Settings.Theme.ColorScheme` — `System` / `Light` / `Dark`. An explicit tenant choice
outranks the operating system; `System` defers to `prefers-color-scheme`.

```css
/* Tailwind v4 */  @custom-variant dark (&:where([data-theme="dark"], [data-theme="dark"] *));
/* Tailwind v3 */  darkMode: ['selector', '[data-theme="dark"]']
/* plain CSS  */   :root { --bg:#fff } [data-theme="dark"] { --bg:#111 }
```

JS API: `__theme.get()`, `.effective()`, `.set('dark'|'light'|'system')`, `.toggle()`. Writing
`'system'` deletes the cookie rather than storing the word, and while no explicit choice is stored
the script tracks live OS changes.

## Switchers — base classes, your markup

No ready-made switcher ships: a panel wants a dropdown, a marketing site a flag row, a docs site a
modal. What is hard is not the markup — it is the right URL per language on a page whose route may
be translated, and knowing whether a click should navigate or change the scope.

```razor
@inherits CultureSwitcherBase

<ul class="lang-menu">
  @foreach (var link in Links)
  {
    <li>
      <a href="@link.Url" hreflang="@link.Hreflang" lang="@link.Hreflang"
         class="@(link.IsCurrent ? "is-current" : null)"
         @onclick="() => SelectAsync(link)" @onclick:preventDefault="InterceptsClick">
        @link.Flag @link.NativeName
      </a>
    </li>
  }
</ul>
```

`CultureLink`: `Culture`, `Hreflang`, `NativeName` (real ICU endonym), `EnglishName`, `Flag`,
`Url`, `IsCurrent`. Every **supported** culture, not only the indexed ones — withholding a
language from search is no reason to hide it from someone who speaks it.

**Always render a real anchor.** The `href` is the only way a crawler discovers the other
languages and the only thing that works with JavaScript off. `InterceptsClick` is true only on an
interactive Site that does not prefix — a panel, where switching should not throw away the
circuit. Everywhere else the anchor navigates, which in a Blazor Web App is already an enhanced
navigation.

```razor
@inherits ThemeSwitcherBase

<button type="button" @attributes="ToggleAttributes" aria-label="@T("Switch theme")">
  <span class="only-light">🌙</span>
  <span class="only-dark">☀️</span>
</button>
```

`ToggleAttributes` and `SetAttributes(ColorScheme)` carry a plain DOM `onclick`, not a Blazor
handler, so the switcher behaves identically in static and interactive rendering with no interop
and no round trip.

## Routing

`WebsiteRoutes` is the built-in router: it scans the entry assembly plus every mounted area's
assembly and renders through `ZonitRouteView`, so string-keyed layouts and the document head both
apply. A Site's fallback layout is a **key**, not a `Type` — `DocumentOptions.DefaultLayoutKey`,
resolved through `ILayoutRegistry`. Passing a `Type` through a component parameter drags trim
annotations across the whole chain.

An unmatched route is a status code, not a fragment: the branch's
`UseStatusCodePagesWithReExecute` renders the Site's error page with a real 404 on the wire.

## Where the rest lives

- `.zonit/extensions/website/seo.md` — culture URLs, `PageMeta`, structured data, robots.
- `.zonit/extensions/website/layouts.md` — `AddWebsiteLayout`, `[LayoutKey]`, `[NoLayout]`.
- `.zonit/extensions/website/hydration.md` — `WebsiteHydrator` and the state bridges.
- `.zonit/extensions/tenants/tenants.md` — the settings the shell reads.
