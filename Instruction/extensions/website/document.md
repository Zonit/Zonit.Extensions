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
| `AddStylesheet("app.css")` | The manifest is keyed by the **full** path — `_content/<AssemblyName>/app.css` for an RCL. A key it does not know comes back verbatim, so the page renders fine and silently loses cache-busting. `AppBase` logs a warning once per URL; `fingerprint: false` says the omission is deliberate and silences it. |

## Static assets and fingerprinting

`AppBase` resolves every declared asset through .NET's static-asset manifest (`@Assets`, .NET 9+),
so a declared URL comes out content-addressed: `app.22ublido7q.css`. `MapStaticAssets` then serves
the fingerprinted route with `Cache-Control: max-age=31536000, immutable` and the plain one with
`no-cache`, and — **in a Release publish** — with precompressed gzip and brotli variants.

Three things are worth knowing before you measure anything:

- **It does not bundle or minify.** Seven declared assets stay seven requests. Minification is
  your build tool's job (Tailwind CLI, esbuild); .NET only fingerprints, compresses and caches.
- **A Debug build is not representative.** Under `dotnet run` the compressed variants are
  effectively absent and fingerprinted routes still answer `no-cache`. Measure on
  `dotnet publish -c Release`.
- **An unknown key fails silently.** No exception, no log, no build error — the key is returned
  as-is. That is why `AppBase` warns, and why `ScopedCssBundle` can use "came back unchanged" as a
  reliable "this file does not exist" and skip the link instead of shipping a 404 on every page.

`Assets` lives on `ComponentBase`, so it works in a `.razor.cs` code-behind exactly as `@Assets`
does in markup. It is **not** in DI — `[Inject] ResourceAssetCollection` compiles and throws at
runtime — so a service, a minimal-API handler or an `IWebsiteArea` cannot resolve an asset URL.
That is why `DocumentOptions` stores plain strings and `AppBase` resolves them at render time.

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

### Assets do not carry the culture

`<base href>` carries the culture because links must. Files must not: a relative
`_content/acme/app.css` under `<base href="/pl/">` becomes `/pl/_content/acme/app.css`, so one file
acquires one URL per language — separate cache entries, separately indexable images.

Everything the shell emits — stylesheets, scripts, scoped-CSS bundle, favicon, `blazor.web.js`, the
import map — is therefore rooted at `AppBase.AssetBase`: the Site's **mount**, culture excluded.

```
Site at "/",      request /pl/          →  /_content/acme/app.css
Site at "/panel", request /panel/de/    →  /panel/_content/acme/app.css
```

The mount stays because `MapStaticAssets` is registered inside the Site's branch; a bare
`/_content/…` 404s wherever no root Site exists. Override `AssetBase` (or `RootedImportMap()`) to
change it.

`@Assets["…"]` in markup is rooted too — `ExtensionsBase` hides `ComponentBase.Assets`, so any
component deriving from it (`PageBase`, `PageViewBase`, `PageEditBase`, `@inherits
ExtensionsBase`) needs no change:

```razor
<img src="@Assets["_content/acme/logo.png"]" />   →  /_content/acme/logo.png
```

Member lookup is a compile-time decision, so this **requires rebuilding** the project whose
markup uses `@Assets` — a binary still compiled against an older package keeps the old URLs.

Not rooted: `LayoutComponentBase` / plain `ComponentBase` components, and any code that casts to
`ComponentBase` before reading `Assets` (hiding is not virtual dispatch). Write `/_content/…` by
hand there.

Because nothing generates a prefixed asset URL any more, a language prefix is valid for **pages
only** — `CultureRouteGate` checks the matched endpoint after routing, so this covers every file
format and every consumer endpoint without an extension list to maintain:

```
/pl/_content/acme/app.css        → 404
/panel/de/_content/acme/logo.png → 404
/pl/api/anything                 → 404   (non-page endpoint)
/pl/_framework/…, /pl/_blazor…   → served (client resolves these against the prefixed base URI)
```

So markup still emitting a relative asset URL fails immediately and visibly. That is the intended
signal: rebuild the project, or root the URL by hand.

### Extending, in increasing order of power

0. `IWebsiteArea.ConfigureDocument(IDocumentAssets)` — an area's *own* assets, declared by the area
   and appended to every Site that mounts it. Use this for anything a plug-in needs, so the host
   shell never becomes a list of what is installed. See `.zonit/extensions/website/areas.md`.
1. `SiteOptions.Document` — covers most of what a hand-written shell contains.
2. `AddHeadComponent<T>()` / `AddBodyEndComponent<T>()` for anything needing Razor or services.
3. Derive from `AppBase` and override a virtual: `Lang`, `BaseHref`, `AssetBase`,
   `RootedImportMap`, `PageRenderMode`, `BuildRoutes`, `Hydrate`, `HeadStart`, `HeadEnd`,
   `BodyStart`, `BodyEnd`, `Document`.
4. Pass your own root component to `UseWebsite<TApp>` — nothing here is mandatory.

Nothing the shell emits names a framework or a product. Cookie names and JS globals are visible in
developer tools, and a recognisable one advertises which open-source stack to look up advisories
against.

## Keeping library and plug-in names out of the page

Asset URLs are the one place a stack fingerprint survives however carefully the markup, cookies and
JS globals are named — and the import map is worse than a single URL, because it enumerates
**every** JS asset the application serves. A page that ships
`_content/Acme.Plugins.Billing.Presentation/js/billing.js` has published its plug-in inventory.

The lever is `StaticWebAssetBasePath`, set in the RCL that owns the files:

```xml
<StaticWebAssetBasePath>_content/billing</StaticWebAssetBasePath>
```

Build-time, so the served URL, the manifest, `@Assets[...]` and the import map all move together
and stay consistent. It replaces the **whole** default prefix, not the last segment — a bare
`billing` puts the files at the application root.

What you cannot do is filter or rewrite the import map at render time. `ImportMap` does take an
`ImportMapDefinition` parameter, and `ImportMapDefinition` has a public constructor, so building
your own compiles and renders. It just does not work: a map key **is** the specifier an `import`
uses, so renaming keys without moving the files breaks module resolution, and dropping an entry
drops that module's fingerprint. Rename at build time or not at all.

Three leaks `StaticWebAssetBasePath` does not close:

- **`_framework/blazor.web.js`** and `blazor.server.js`. Not renameable, and the reconnect markup,
  the `_blazor` endpoint and the enhanced-navigation attributes identify the stack anyway. Hiding
  *which framework* is not achievable; hiding *which components and plug-ins you run* is, and it
  is the more useful secret.
- **Third-party RCLs** (`_content/MudBlazor/…`). The property belongs to the project that owns the
  assets, and you do not own theirs.
- **Collocated `*.razor.js`**, which carries the component's path and name into the URL even after
  a rebase — `_content/ui/Layouts/ReconnectModal.razor.js`. Move the file to `wwwroot/js/` and load
  it by path if the name matters.

The application's own scoped-CSS bundle is `$(PackageId).styles.css` at the site root. Renaming
`PackageId` moves it, but `AppBase` derives the name from `ApplicationName` (the assembly name),
which does not follow — so set `Document.ScopedCssBundleName` to the new name in the same commit.
Get that wrong and every `*.razor.css` in the solution goes inert without a 404 or a log; the
explicit setting warns when the manifest does not recognise it, the derived one cannot.

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
