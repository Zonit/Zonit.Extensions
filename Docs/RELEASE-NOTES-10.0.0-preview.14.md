# Zonit.Extensions 10.0.0-preview.14 — release notes

**2026-08-09.** Upgrading from **10.0.0-preview.13**. Every package moves together; there is no
partial upgrade path.

This release makes a Site able to be a correctly indexable multilingual website without the host
writing any SEO code, and adds a sitemap package. Read [Breaking changes](#breaking-changes)
first — three of them are silent behaviour changes rather than compile errors.

Nothing here activates by default. A Site that does not opt in keeps the URLs, the pipeline and
the document it has today.

---

## What you can now do in four lines

```csharp
app.UseWebsite("/", o =>
{
    o.Mode = WebsiteMode.Static;
    o.Cultures.Strategy = CultureUrlStrategy.Prefix;
});
```

That yields, per page and per language: one canonical URL with permanent redirects from every
other spelling, a full `hreflang` cluster plus `x-default`, a composed `<title>`, `description`,
Open Graph, JSON-LD, a `robots` directive that follows indexability, and a generated `robots.txt`.
No `App.razor`, no head component, no meta tags written by hand.

---

## Breaking changes

### `PageBase.Meta` is no longer virtual

`Meta` is now a stable instance the base class owns. Override the factory instead.

```csharp
// before — preview.10 had no PageMeta at all; this is the shape from the interim API
protected override PageMeta Meta { get; } = new() { Description = "…" };

// after
protected override PageMeta Metadata() => new() { Description = "…" };
```

Overriding `Meta` with an expression body (`Meta => new() { … }`) is now a compile error, and that
is the point: it would have handed out a fresh object on every read, so `Meta.Title = x` would
have written to an instance discarded on the next line.

### Localized routes moved from the Site to the area

```csharp
// before
o.Cultures.LocalizeRoute("/news/{slug}", ("pl-pl", "/aktualnosci/{slug}"));

// after — on the area that owns the route
public IReadOnlyList<AreaRoute> Routes =>
[
    AreaRoute.Localize("/news/{slug}", ("pl-pl", "/aktualnosci/{slug}")),
];
```

A host should not have to restate the translations of every plug-in it mounts.

### The default colour scheme moved to the tenant

`AppearanceOptions.Default` is gone. Set `Tenant.Settings.Theme.ColorScheme`
(`System` / `Light` / `Dark`). `AppearanceOptions` keeps only the plumbing — attribute, cookie
name, global name.

### `TenantMiddleware` moved ahead of `CultureMiddleware` — *silent*

It now runs before routing so culture resolution can read the tenant's default language. Tenant
resolution keys off `Request.Host` alone, so nothing there needs routing or an authenticated
principal, and its initialisation is idempotent per host.

**What changes for you:** a host whose `ITenantSource` assumed an authenticated principal was
already available will now see an anonymous request. It never legitimately could — the middleware
sat after auth by accident of ordering, not by contract — but check it.

### Culture resolution gained a step — *silent*

URL prefix → cookie → `Accept-Language` → **`Tenant.Settings.Site.Language`** →
`CultureOption.DefaultCulture`.

A host that set `CultureOption.DefaultCulture` and left the tenant's `Site.Language` at its
`"pl-PL"` default will now serve Polish where it previously served the configured default. Set
`Site.Language` to match, or change it in the admin UI.

### The culture cookie is `lang` — *silent*

Renamed from `Culture`. Existing visitors fall back to `Accept-Language` once and are re-cookied;
nothing errors. The old name is not read.

Rationale: cookie names are visible in developer tools, and a recognisable framework name
advertises which open-source stack to look up advisories against. No framework or product name now
appears in the emitted HTML, the cookies or the JS globals.

### Removed as redundant

| Removed | Why |
| --- | --- |
| `Cultures.RedirectUnprefixed` | Off meant serving a duplicate of every page at an unprefixed address. There was no correct "off". |
| `Appearance.SetColorScheme` | A dark theme with white native controls is a broken dark theme, not a variant. |
| `SiteOptions.CanonicalOrigin` | The tenant's `Site.CanonicalUrl` plus the request host covers it. |

### `MountSnapshot` gained members

`WebsiteMountRegistry.MountSnapshot` is a positional record and now carries `Appearance`,
`Document`, `Settings`, `UrlPolicy`, `LocalizedRoutes` and `Mode`. Code constructing it by hand
must be updated; code reading it is unaffected.

---

## Fixed

**`/pl` 404'd.** The culture-segment matcher required a non-empty remainder, so the language root —
the most valuable URL in the cluster — was unreachable. It now 301s to `/pl/`.

**No `Vary` on cookie-dependent responses.** A Site whose output depends on the culture cookie or
`Accept-Language` emitted no `Vary` header, so any shared cache in front of the app could hand one
visitor's language to the next. Unprefixed Sites and the unprefixed redirect now declare
`Vary: Cookie, Accept-Language`; prefixed pages are deterministic and stay fully cacheable.

**A Site with no areas had no pages.** `MapRazorComponents<TApp>()` roots page discovery at
`TApp`'s assembly, which for the built-in shell is the framework — so a mount using the
non-generic `UseWebsite` with no areas produced zero page endpoints and answered 404 for
everything. The entry assembly is now always included.

**`OnAfterRender` never runs under static rendering.** Page metadata assigned after an `await` was
computed and then dropped. Republishing is hooked to `OnInitializedAsync` / `OnParametersSetAsync`
instead, which run in every render mode.

---

## Added

### Culture in the URL

Per mount, opt-in. See `.zonit/extensions/website/seo.md` for the full contract; the short version:

| Request | Response |
| --- | --- |
| `/pl/pricing` | 200, canonical |
| `/pl-pl/pricing` | 301 → `/pl/pricing` |
| `/pl` | 301 → `/pl/` |
| `/pl/news/x` where Polish translates the route | 301 → `/pl/aktualnosci/x` |
| `/pricing` | 302 → visitor's language, uncacheable, `x-default` target |

The segment is moved into `Request.PathBase`, not deleted from `Request.Path`. That is what makes
`<base href>` become `/pl/` so every relative link keeps the language, and what keeps
`PathBase + Path` equal to the URL the browser asked for — a plain path rewrite puts
`NavigationManager` outside its own `BaseUri` and throws.

### `AppBase` and the non-generic `UseWebsite`

A complete, extensible document shell. `SiteOptions.Document` declares stylesheets, scripts,
preconnects, metas and favicon; `AddHeadComponent<T>()` / `AddBodyEndComponent<T>()` cover anything
needing Razor; virtuals cover the rest; your own `TApp` still works unchanged.

### `PageMeta` and structured data

See `.zonit/extensions/website/seo.md`. Structured data is derived from the page's metadata, the
breadcrumb trail and tenant settings, with `Organization` + `WebSite` on the home page only. A
page-supplied node replaces the derived one of the same type.

### `Zonit.Extensions.Website.Sitemaps`

New package. `ISitemapSource` yields records; the package owns absolute URLs, the mount base,
culture expansion, `hreflang`, both protocol limits, part numbering, the index and caching.
Sources register themselves from the area that owns the content, so installing a plug-in adds its
URLs and removing it removes them. See `.zonit/extensions/sitemaps/sitemaps.md`.

### Configuration

```json
{
  "Website": {
    "/":      { "Indexable": true, "IndexedCultures": [ "en-us", "pl-pl" ] },
    "/admin": { "Indexable": false }
  }
}
```

Two keys, both hot-reloaded, both answering the same question: what this environment lets search
engines see. Staging differs from production by one file rather than one build. Everything else is
code (structural) or tenant settings (cosmetic) — deliberately, so there is one place to look.

---

## Upgrade checklist

1. Replace `Meta { get; } = new()` overrides with `Metadata() => new()`.
2. Move `o.Cultures.LocalizeRoute(...)` calls onto the owning area's `Routes`.
3. Set `Tenant.Settings.Site.Language` to the language you actually want as the default.
4. If you relied on `AppearanceOptions.Default`, set `Tenant.Settings.Theme.ColorScheme`.
5. Verify your `ITenantSource` does not read the authenticated principal.
6. Opting a Site into `CultureUrlStrategy.Prefix`? Submit the new sitemap and expect
   "Page with redirect" to grow in Search Console — that is the unprefixed space, and it is
   *Excluded*, not *Error*.
