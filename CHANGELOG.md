# Changelog

Running index of what shipped, when. Newest first. Detailed migration notes for a release live in
`Docs/RELEASE-NOTES-<version>.md`; this file is the index an assistant reads to answer "is this API
new, renamed, or gone?".

Every package in this repository versions together — there is no partial upgrade path.

---

## 10.0.0-preview.18 — 2026-08-10

### Changed — `Zonit.Extensions.Website.Sitemaps` is gone; it is part of the kernel

The package could never work on its own — it needs `ICurrentSite`, the culture policy and the
localized-route table — so "a package for people who only want sitemaps" described nothing that
could exist. Meanwhile the split cost a package to forget, a version to skew, and made
`robots.txt` conditional on installing it. Sitemaps, robots and llms are web-only concerns, unlike
`Cultures` or `Tenants`, which is what makes the kernel their right home.

- **Breaking.** Remove the `Zonit.Extensions.Website.Sitemaps` `PackageReference`. Namespaces are
  unchanged (`Zonit.Extensions.Website.Sitemaps`), so `using` directives keep compiling.
- `AddSitemap()` now runs inside `AddWebsite()`. Call it explicitly only to change the generation
  limits or the cache duration.
- The package is withdrawn at `10.0.0-preview.17`.

### Added — `[WebsiteSitemap]` and `[WebsiteLlms]`, collected at build time

Both carry the `Website` prefix on purpose: typing `Webs` in an editor lists every attribute this
package contributes, next to `WebsiteMode` and `WebsiteHydrator`. An unprefixed `[Sitemap]` would
also collide with the `WebsiteSitemap` class name projects commonly give their `ISitemapSource`.

```csharp
[Route(Route)]
[WebsiteSitemap(Change = ChangeFrequency.Monthly, Priority = 0.8)]
[WebsiteLlms("Settled outcomes for every signal — the source for hit-rate questions.")]
public sealed partial class Signals : PageBase { public const string Route = "/signals"; }
```

Two attributes, because they answer different questions: a sitemap is an inventory of everything
worth crawling, `llms.txt` is a briefing of the handful of pages that explain what the site is.
Most pages want only the first.

**Opt-in.** A page is published because someone wrote `[WebsiteSitemap]`. The opt-out default reads as
safer and is not — its failure mode is a page written in a hurry being *advertised to search
engines* before anyone decided it should be public. Forgetting the attribute costs a listing and
shows up in Search Console; forgetting to remove one publishes something.

**Collected by a source generator, not at start-up.** The set of static pages in an assembly is
fixed the moment it compiles; rediscovering it by reflection each run costs start-up time,
produces the same answer every time, and is opaque to trimming. `SitemapPageGenerator` emits an
array literal and a `[ModuleInitializer]` that hands it to `StaticPageRegistry`.

It reads two shapes: `@page` + `@attribute [WebsiteSitemap]` out of the `.razor` text (the Razor SDK puts
every `.razor` into `AdditionalFiles`), and `[Route(...)]` + `[WebsiteSitemap]` off the C# symbol — where
`[Route(Route)]` against a `const string` resolves to its value, which a text parser could not do.
A generator cannot see another generator's output, so the `[Route]` the Razor compiler emits is
invisible here; that is why the template is parsed rather than the generated class.

**Parameterised routes warn at build time.** `ZONITSM0001` names the page: a route template is not
a URL and cannot go into the XML. `ZONITSM0002` covers an attribute with no route in sight.

- Static pages are served by a built-in `ISitemapSource` scoped to the areas the Site mounts, so
  a host running a public site and an admin panel publishes different sitemaps. The hand-written
  three-entry source every project used to carry is now the attributes themselves.
- `[WebsiteLlms]` entries merge with `x.Llms.AddLink(...)`, grouped by `Section`.
- `Site.About` (tenant) — what the site *is*, in prose, for `llms.txt`. Separate from
  `MetaDescription`, which is a 160-character snippet written to earn a click and the wrong text
  for an agent deciding whether the site can answer a question at all.
- `ChangeFrequency.Unset` is new and is now the zero value.

### Removed — the `[Seo]` attribute and the `Crawl` enum

Both existed for a day. `Crawl` modelled four states only because the sitemap was opt-out; with
opt-in, "not in the sitemap" is simply the default and the enum has nothing left to say.
`noindex` stays on `PageMeta.NoIndex`, a `Disallow` stays on `x.Robots.Disallow(...)`, and
`[Authorize]` / `[RequirePermission]` / `[RequireRole]` still imply `noindex` — see `PageIndexing`.


### Added — eight languages in `Zonit.Extensions.Cultures`

`bg-bg` Bulgarian · `bn-bd` Bengali · `el-gr` Greek · `et-ee` Estonian · `lt-lt` Lithuanian ·
`lv-lv` Latvian · `mt-mt` Maltese · `ro-ro` Romanian. Registry goes 17 → 25, and all eight are
added to `CultureOption.SupportedCultures`.

This matters more than a list of tags because `ILanguageProvider.GetByCode` **never fails**: a tag
outside the registry silently resolves to the English model, so a picker configured with `ro-ro`
before this release rendered an entry labelled "English" with an American flag and no warning
anywhere. Each new model ships its own inline SVG flag and a real `NativeName`.

### Fixed — `AlternativeCodes` was documented but never read

`LanguageModel.AlternativeCodes` has always said resolution goes "exact code, then primary subtag,
then anything in `AlternativeCodes`". `LanguageService` only ever consulted the first two — a model
declaring an alias got nothing. Aliases now fold into the exact-match index, which is the order the
contract always claimed.

### Fixed — which regional variant owns a bare subtag was decided by hash order

The primary-subtag index (`en` → some `en-*`) was built by enumerating a `FrozenDictionary`, which
guarantees no ordering. With one variant per language that was invisible; the moment a second
arrives — `en-gb` beside `en-us`, `es-mx` beside `es-es` — which one a bare `en` resolves to would
have been decided by hash layout: stable within a build, silently different after adding an
unrelated entry, and invisible in review. The registry is now an explicitly ordered array and first
declaration wins.

### Documentation

- `CHANGELOG.md` now ships **inside the package** and installs into a consumer's `.zonit/` tree as
  `changelog.md`. An assistant working in a consumer repository could see the current API surface
  and the consumer's code, but nothing about which release moved what — so "is this new, renamed,
  or gone?" was unanswerable without leaving the repository. Packed from the repo file, so there
  is one changelog and no copy to drift.
- `sitemaps.md` moved from its own area into `website/`, and now documents the attributes, the
  `Crawl`-free model, the generator diagnostics and the authorization rule.

### Fixed

- **`hreflang` and `x-default` would have disappeared from every page.** `SeoDocumentBuilder`
  inferred indexability from `robots is null`, which held only while the indexable case emitted no
  tag. The positive default introduced in this same release made every page look non-indexable, so
  the whole alternates cluster was suppressed — silently, with valid HTML and no error. It now
  tests the directive's content.
- The AI-context installer shipped two broken paths: `seo.md` and `document.md` had their
  directory separators stripped (`..Instructionextensionswebsiteseo.md`), so neither doc ever
  installed into a consumer repository.

### Changed — robots, sitemap and llms are now one subsystem

`robots.txt` and `llms.txt` moved out of the kernel into **`Zonit.Extensions.Website.Sitemaps`**,
joining `sitemap.xml` under a single options tree. The three files are one statement in three
formats and only work if they agree: `robots.txt` has to name the sitemap's real address, and
neither may contradict the culture policy about which languages are indexed. Configured
separately, that agreement was a convention the host maintained by hand — and the failure is
silent, because a `robots.txt` naming a sitemap that moved is a *valid file* crawlers simply
believe.

```csharp
app.UseWebsite("/", o => o.Indexing(x =>
{
    x.Robots.Disallow("/search");
    x.Llms.Summary = "…";
    x.Llms.AddLink("Docs", "/docs", "API reference — for parameter-level questions.");
}));
```

- **Breaking.** `SiteOptions.Robots` is gone. `o.Robots.Disallow/Allow` → `x.Robots.…`;
  `o.Robots.Summary` → `x.Llms.Summary`; `o.Robots.AddLlmsLink(…)` → `x.Llms.AddLink(…)`;
  `o.Robots.Sitemap("/sitemap.xml")` → **delete it**, `Indexing()` derives it; and
  `o.MapEndpoints(ep => ep.MapSitemap())` → **delete it**, `Indexing()` maps it.
- **Breaking.** A Site that never calls `Indexing()` no longer serves `robots.txt`. It used to be
  mapped by the kernel for every mount.
- `ICurrentSite.Indexable` is new and public — the resolved verdict, read per request so a
  configuration reload reaches a running process. It exists so packages outside the kernel can
  agree with the pipeline instead of re-deriving the rule.
- `MapSitemap()` remains, for the rare Site that publishes a sitemap `robots.txt` must not name.

### Changed — pages now emit a positive `robots` directive

The normal case used to emit no tag, on the grounds that `index, follow` is what a crawler assumes.
It now emits `index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1`. The
first half is indeed redundant; the three limits are not — they default to conservative values, so
a site that said nothing was opting into a smaller SERP presentation without meaning to.
`max-image-preview:large` is what makes a result eligible for the large image thumbnail. It also
removes a real ambiguity: "no tag" and "the head renderer never ran" look identical in view-source.

`PageMeta.Robots` is new — a string that replaces the directive outright (`""` emits no tag). Site
level still wins: a closed Site, or a language kept out of the index, cannot be talked into
`index` by a page.

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
