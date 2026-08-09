# Changelog

Running index of what shipped, when. Newest first. Detailed migration notes for a release live in
`Docs/RELEASE-NOTES-<version>.md`; this file is the index an assistant reads to answer "is this API
new, renamed, or gone?".

Every package in this repository versions together — there is no partial upgrade path.

---

## 10.0.0-preview.18 — 2026-08-10

### Added

- **`IWebsiteArea.ConfigureDocument(IDocumentAssets)`** — an area declares its own stylesheets,
  scripts, preconnects, metas and head / body-end components, and they are appended to the
  document shell of every Site that mounts it. Mounting an area is now the whole installation;
  the host shell stops being a manifest of what is installed.
- **`IDocumentAssets`** — the append-only subset of `DocumentOptions` handed to areas.
  `Favicon`, `DefaultLayoutKey`, `ImportMap` and `ScopedStyles` stay Site-wide decisions, so a
  plug-in cannot silently win one by mount order.
- **`DocumentOptions.ScopedCssBundleName`** — states the scoped-CSS bundle's real file name.
  `AppBase` derives it from `ApplicationName`, which stops matching the moment a host renames
  `PackageId` (the bundle is emitted as `$(PackageId).styles.css`) — and the symptom is every
  `*.razor.css` in the solution silently going inert. A configured name the manifest does not
  know now logs a warning, since unlike the derived one it can never legitimately be absent.

Contributions run once per mount, after the Site's own declarations, in area registration order —
so base sheets belong on `SiteOptions.Document` and an area's sheet cascades over them.
Default-implemented, so no existing area needs a change.

> Note on numbering: the section below is headed `preview.14` but covers everything released
> across `.14` through `.17`. The entries are accurate; the header is not.

---

## 10.0.0-preview.14 — 2026-08-09

Public-web release: a Site can now be a correctly indexable multilingual website without the host
writing SEO code. Full detail and migration steps in
[Docs/RELEASE-NOTES-10.0.0-preview.14.md](Docs/RELEASE-NOTES-10.0.0-preview.14.md).

### Added

- **Culture in the URL**, per mount. `SiteOptions.Cultures.Strategy` = `None` (default) or
  `Prefix`; `Format` = `Short` (`/pl/`) or `Full` (`/pl-pl/`), degrading per-language when a
  primary subtag is ambiguous. One canonical address per page, enforced with 301s.
- **`AppBase` / `WebsiteApp`** — a complete document shell. `app.UseWebsite("/", o => …)` with no
  type argument needs no `App.razor` at all. Extend via `SiteOptions.Document`, head/body
  components, or virtuals.
- **`WebsiteMode.Static`** — server rendering plus enhanced navigation, no circuit.
- **`PageMeta`** on `PageBase` — title, description, image, `NoIndex`, canonical, per-culture
  alternates, structured data. Emitted by `PageHead` through `HeadOutlet`.
- **Structured data (JSON-LD)** under `Seo/Schema/`, derived from state the framework already
  holds; a page adds only what inference cannot know.
- **Generated `robots.txt` and `llms.txt`** per Site, derived from live indexability.
- **`Zonit.Extensions.Website.Sitemaps`** (new package) — `ISitemapSource` registered by the area
  that owns the content; the package owns culture expansion, `hreflang`, both size limits, parts,
  the index and caching.
- **`AreaRoute`** on `IWebsiteArea.Routes` — routes whose path differs per language.
- **`CultureSwitcherBase` / `ThemeSwitcherBase`** — behaviour without markup.
- **Client-side appearance system** — `data-theme` stamped before first paint; the scheme never
  reaches the server, so HTML stays cacheable.
- **`Website` configuration section** — per-mount `Indexable` and `IndexedCultures`, hot-reloaded.
- Tenant settings: `Site.CanonicalUrl`, `Site.SocialImageUrl`, `Site.TitlePosition`,
  `Site.TitleSeparator`, `Theme.ColorScheme`.
- **Site-wide metadata defaults.** A page that declares nothing still renders a complete head:
  `Title` → tenant site title, `Description` → tenant meta description, `Image` → tenant
  `SocialImageUrl`, and `theme-color` is derived from `Theme.PrimaryColor` (an explicit
  `Document.AddMeta("theme-color", …)` still wins). See `seo.md` for which layer owns which
  default and why.
- `ICurrentSite` exposes `Appearance`, `Document`, `Mode`, `UrlPolicy`, `LocalizedRoutes`.
- **`NavItem.External` / `LinkModel.External`** (`Url`) for destinations outside the site, plus
  `ToHref()` and `IsMatchable()` extensions that renderers funnel through.
- **Automatic translation** of navigation titles and tooltips, and of `PageMeta.Title` /
  `Description`. Opt out per node with `Translate = false`.
- MudBlazor 9.8.0.

### Changed

- **`TenantMiddleware` now runs before `CultureMiddleware`** and before routing, so culture
  resolution can read `Tenant.Settings.Site.Language`. Resolution order is URL prefix → cookie →
  `Accept-Language` → tenant language → `CultureOption.DefaultCulture`.
- The culture cookie is `lang` (was `Culture`), and carries no product name.
- `ZonitRouteView` renders the document head for every routed page and gained `DefaultLayoutKey`.
- `UseDashboard` pins `Indexable = false` and `Cultures.Strategy = None`.
- Page endpoint discovery always includes the entry assembly.

### Fixed

- `/pl` (language root without a trailing slash) 404'd; now 301s to `/pl/`.
- Responses varying by cookie or `Accept-Language` carried no `Vary` header, so a shared cache
  could serve one visitor's language to the next.
- **Every relative asset 404'd on a culture-prefixed Site**, including Blazor's own framework
  script. `<base href>` is `/pl/`, so a relative `src="_framework/blazor.web.js"` makes the
  browser request `/pl/_framework/blazor.web.js` — and `CultureMiddleware` returned early for
  static-looking paths *before* moving the segment into `PathBase`, so the static-asset endpoint
  never saw a route it knew. Stylesheets, scripts and the framework bundle all failed silently on
  every prefixed page. The skip now suppresses only the culture *work* (resolution, cookie,
  feature, redirects); the path split always runs. A mount prefix was never affected, because
  `UsePathBase` is real middleware that runs unconditionally.
- A Site mounted with the non-generic `UseWebsite` and no areas produced zero page endpoints.
- `UrlPath.ToHref()` rendered the site root (`"/"`) as `href=""`, which means "this exact URL",
  not "the base" — a Home link went nowhere. It renders `"./"` now. An *empty* `UrlPath` still
  renders `href=""`, which is what a non-clickable breadcrumb wants.
- `CultureSwitcherBase` listed languages from `ICultureState.Supported`, whose models resolve
  through the seventeen-entry `ILanguageProvider` registry — a configured tag outside it silently
  collapsed onto a neighbour, so a site supporting both `pt-pt` and `pt-br` showed two identical
  "português (Portugal)" entries pointing at `/pt-pt/` and Brazilian Portuguese was unreachable
  from the switcher. It iterates the configured allow-list now and uses the registry only for the
  flag.
- `Zonit.Documents` emitted its own `<title>` before `<HeadOutlet />`, which after this release
  would have won over the composed one and frozen every page on the site name.
- `AppBase` now logs a warning (once per URL) when a declared asset asked for a fingerprint and
  the manifest did not recognise the key — previously that failure was completely silent and cost
  the file its cache-busting.

### Breaking

- `PageBase.Meta` is no longer virtual — override `Metadata()`.
- `SiteOptions.Cultures.LocalizeRoute(...)` removed; declare `AreaRoute.Localize(...)` on the area.
- `AppearanceOptions.Default` removed — the default scheme is `Tenant.Settings.Theme.ColorScheme`.
- Removed as redundant: `Cultures.RedirectUnprefixed`, `Appearance.SetColorScheme`,
  `SiteOptions.CanonicalOrigin`.
- `WebsiteMountRegistry.MountSnapshot` gained members (positional record).
- `NavItem`, `NavGroup` and `LinkModel` are now `record`s — source-compatible for construction and
  reads; they gain value equality and `with`.
- Dashboard cookies renamed: `zonit.dashboard.theme` / `.mode` / `.system-dark` →
  `ui.theme` / `ui.mode` / `ui.scheme`. Each user's stored theme preference resets once.

### Dashboard now builds on the Website kernel

`Zonit.Dashboard` stopped hand-writing what the framework provides. Behaviour is unchanged; the
code that produced it is not.

- `DashboardApp.razor` (186 lines of hand-written document) → `DashboardApp : AppBase` (one
  override, for the router). `<base href>`, the scoped-CSS bundle, `ImportMap`, `HeadOutlet`, the
  state bridge and the framework script all come from `AppBase`.
- Dashboard assets are **declared** on `SiteOptions.Document` in `DashboardSiteOptions`
  (MudBlazor CSS/JS, Roboto, dashboard chrome) instead of written into markup.
  `DashboardHead` and `Connection` are registered as head / body-end components.
- `Routes.razor` reads `ICurrentSite.Areas` instead of a dashboard-local registry. It survives
  only because its 403 panel is MudBlazor markup, which the kernel does not reference.
- `DashboardMountRegistry` lost `Assemblies` and `ForMount()` — it keeps only genuinely
  dashboard-specific per-mount data. **Breaking** for anything calling `Register(...)` with the
  old six-argument shape.
- `Appearance.Enabled = false` on a dashboard mount: MudBlazor resolves its palette server-side
  and needs the opposite handshake to the framework's client-side one.
- No occurrence of the framework name is left in a rendered dashboard page. RCL assets moved to
  `_content/ui/` (`StaticWebAssetBasePath`), `zonit-dashboard.css` → `dashboard.css`,
  `zonit-reconnect-*` classes → `reconnect-*`. **Breaking** for anything referencing
  `_content/Zonit.Dashboard/…`.

---

## 10.0.0-preview.13 and earlier

See the `Docs/RELEASE-NOTES-*.md` files; the most recent authored one is
[10.0.0-preview.10](Docs/RELEASE-NOTES-10.0.0-preview.10.md).
