# SEO — culture URLs, page metadata, structured data, robots

Everything a public Site needs to be indexable correctly. All of it is per-mount: a panel mounted
next to a public site opts into none of it.

## Read this before you write code

| Trap | What actually happens |
| --- | --- |
| `protected override PageMeta Meta => new() { … }` | Does not compile — `Meta` is not virtual, deliberately. An expression-bodied property would hand out a **new object per read**, so `Meta.Title = x` would write to an instance discarded on the next line. Override `Metadata` instead — the base reads it exactly once and caches. |
| Setting `Meta.Title` in `OnAfterRender` | Never runs under `WebsiteMode.Static`, which is the mode a public Site uses. Set it in `Metadata`, `OnInitializedAsync` or `OnParametersSet` — all three are re-announced. |
| `o.Cultures.LocalizeRoute(...)` | Gone. Localized paths are declared by the area that owns the route, as `IWebsiteArea.Routes`. |
| Culture prefix and `@page` | A prefixed URL never appears in a route template. The middleware moves the segment into `Request.PathBase`, so routing sees `/pricing` and `<base href>` becomes `/pl/`. |
| `Indexable` on a permissioned Site | Already `false` — it derives from `Permission is null`. `UseDashboard` pins it explicitly on top of that. |
| Two live URLs per page | Impossible by construction: a non-canonical culture spelling, a missing trailing slash on the language root and an untranslated route all answer **301** to the one canonical form. |
| `robots.txt` in `wwwroot` | Shadowed once a Site calls `o.Indexing(...)` — the generated one knows which cultures are indexed and whether the Site is closed. Set `x.Robots.Enabled = false` to hand the path back. |

## Culture in the URL

```csharp
app.UseWebsite("/", o =>
{
    o.Cultures.Strategy = CultureUrlStrategy.Prefix;   // None (default) | Prefix
    o.Cultures.Format   = CultureUrlFormat.Short;      // Short → /pl/ ; Full → /pl-pl/
});
```

| Request | Response |
| --- | --- |
| `/pl/pricing` | 200, canonical, self-referencing `hreflang` |
| `/pl-pl/pricing` | 301 → `/pl/pricing` (non-canonical spelling) |
| `/pl` | 301 → `/pl/` (language root keeps its slash) |
| `/pl/pricing/` | 301 → `/pl/pricing` (trailing slash; unprefixed Sites 301 GET/HEAD the same way) |
| `/pl/news/x` where Polish translates the route | 301 → `/pl/aktualnosci/x` |
| `/pricing` | 302 → the visitor's language, `Vary: Cookie, Accept-Language`, `no-store` |
| `/pl/app.css`, `/pl/report.glb`, `/pl/api/ping` — anything that is not a page | 404 — the prefix is valid for page routes only, decided from the endpoint table (`CultureRouteGate`), not from an extension list |
| `/pl/_framework/…`, `/pl/_blazor…` | served — the client resolves these against the prefixed base URI (WASM boot, circuits, hot reload); both are robots-disallowed |
| `/en-gb/x` when only `en-us` is supported | falls through — an unknown region is never folded into a neighbour |

`Short` degrades to the full tag **per language** when a primary subtag is ambiguous: with `pt-pt`
and `pt-br` both supported, those two keep their regions and `/de/`, `/fr/` stay short. `/pt/` is
still accepted and 301s to `pt-pt` (the tag whose region repeats the language), else to whichever
comes first in `SupportedCultures`.

Only `GET`/`HEAD` requests that explicitly `Accept: text/html` are redirected, which bounds the
behaviour to browsers and crawlers without a list of API paths to exclude.

### Which language wins

URL prefix → `lang` cookie → `Accept-Language` (first entry) → **`Tenant.Settings.Site.Language`**
→ `CultureOption.DefaultCulture`.

The tenant sits above the framework default because in a multi-site host each brand has its own.
`TenantMiddleware` therefore runs **before** `CultureMiddleware` and before routing; it resolves
off `Request.Host` alone, so nothing there needs routing or an authenticated principal.

### Routes that change shape per language

Declared by the area, not the host — the plug-in that defines a route owns its translations:

```csharp
public sealed class NewsArea : IWebsiteArea
{
    public string Key => "news";

    public IReadOnlyList<AreaRoute> Routes =>
    [
        AreaRoute.Localize("/news/{slug}",
            ("pl-pl", "/aktualnosci/{slug}"),
            ("de-de", "/nachrichten/{slug}")),
    ];
}
```

The component keeps one `@page "/news/{slug}"`. Only the **static head** of the template is mapped
(`/aktualnosci` → `/news`); a translated *slug* lives in the content store and comes from the page
via `PageMeta.Alternates`.

## Page metadata

```razor
@page "/pricing"
@inherits PageBase

@code {
    protected override PageMeta Metadata => new() { Description = "What it costs." };
}
```

Refine after data loads — the object is stable for the page's lifetime and re-announced after each
lifecycle step:

```csharp
protected override async Task OnInitializedAsync(CancellationToken token)
{
    _article = await _articles.GetAsync(Slug, token);
    Meta.Title = _article.Title;
    Meta.Type  = "article";
    Meta.Alternates["pl-pl"] = $"/aktualnosci/{_article.SlugPl}";
}
```

| `PageMeta` member | Effect |
| --- | --- |
| `Title` | Page title before composition. Composed with the tenant's website title. |
| `Description` | `meta description` + `og:description`. Falls back to the tenant's. |
| `Image` | `og:image`. Relative paths resolve against the canonical origin. |
| `Type` | `og:type`. `"article"` also switches the derived schema node to `Article`. |
| `NoIndex` | `noindex, follow` — reachable but not a search result. Not an access control. |
| `Canonical` | Overrides the derived canonical (paginated listing → page 1). |
| `Alternates` | Per-culture paths when the **slug** is translated. |
| `Schema` | Extra structured-data nodes; replaces the derived node of the same type. |
| `AutoSchema` | `false` disables derivation entirely. |
| `Translate` | `true` by default — see below. |

A page that sets nothing still renders a correct document. Title composition, the `hreflang`
cluster and the robots directive are never the page's business.

### Where a default belongs

Four layers can hold a value. The rule is **who changes it, and how often** — not what the value
happens to be:

| Layer | Holds | Test |
| --- | --- | --- |
| `PageMeta` | this page's title, description, image, canonical, schema | changes per page |
| **Tenant settings** | site name, meta description, share image, logo, favicon, canonical origin, title separator and position, brand colour, default language, colour scheme | a **person** would change it, in an admin UI, without a deploy — and in a multi-site host each brand answers differently |
| `SiteOptions` (code) | culture strategy and format, cookie and global names, document assets, crawl directives, layout key | structural; markup and CSS are written against it, and half of it is baked into the pipeline at startup |
| `appsettings` | `Indexable`, `IndexedCultures` | differs between staging and production on an identical build |

Areas deliberately contribute **none** of these. An area is a plug-in mounted into someone else's
site; letting it set the site's share image or title separator would let a dependency overwrite
brand identity. Areas contribute their own pages' `PageMeta`, their `Routes`, their navigation and
their sitemap sources — things they own.

Everything with a sensible default already falls back, page → tenant:

| Missing on the page | Falls back to |
| --- | --- |
| `Title` | tenant `Site.Title` alone (no separator with nothing on one side) |
| `Description` | tenant `Site.MetaDescription` |
| `Image` | tenant `Site.SocialImageUrl` — one 1200×630 banner gives every page a correct social preview |
| canonical | the page's own address |
| `theme-color` | derived from tenant `Theme.PrimaryColor`; `Document.AddMeta("theme-color", …)` overrides |
| `og:locale`, `og:site_name` | the active culture and the tenant title |

So a page that declares nothing still renders a complete, correct head. Reach for
`Document.AddMeta(...)` only for fixed technical tags a person would never edit — a search-console
verification token, a `referrer` policy.

### Text is translated for you

`Title` and `Description` go through the translation registry automatically. The key in this
framework **is** the English source string, so `Description = "What it costs."` is already a valid
key — writing `T(…)` there is redundant, and forgetting it would produce a page correct in English
and silently untranslated everywhere else.

Text with no rendition falls through to itself, so this is safe for dynamic values:
`Meta.Title = article.Headline` renders the headline whether or not anything matches. Set
`Translate = false` for a product or person name that must never be looked up, or for text you
already passed through `T(…)` yourself.

Navigation works the same way: `NavItem.Title`, `NavItem.Tooltip` and the group equivalents are
translated by `INavigationProvider`, with the same per-node `Translate` opt-out.

Title composition comes from the tenant: `Site.Title`, `Site.TitlePosition`
(`Suffix` / `Prefix` / `None`), `Site.TitleSeparator` (empty ⇒ page title alone).

## Structured data (JSON-LD)

Derived from state the framework already holds, then merged with whatever the page added.

| Node | Source | When |
| --- | --- | --- |
| `WebPage` or `Article` | `PageMeta` title / description / image + canonical | every page |
| `BreadcrumbList` | `IBreadcrumbsProvider` | trail has ≥ 2 entries |
| `Organization` | tenant `Site.Title`, `LogoUrl`, `SocialMedia` → `sameAs` | **home page only** |
| `WebSite` | tenant `Site.Title`, `MetaDescription` | **home page only** |

"Home page" means `RoutePath` is `/` — i.e. `/pl/`, `/en/`, or `/` on an unprefixed Site. The
publisher block is emitted there and nowhere else: that is the documented placement, and repeating
it on every page adds bytes without adding meaning.

`Organization` here is the **schema.org publisher**, built from tenant settings. It has nothing to
do with `Zonit.Extensions.Organizations` / `Projects`, which model workspaces and project catalogs
for team collaboration inside the app.

Several nodes are emitted as one `@graph`. Only `WebPage` / `Article` inherit the page canonical —
a breadcrumb trail is not located at the page, and an organization certainly is not.

Add only what inference cannot know:

```csharp
Meta.Schema.Add(new SchemaArticle
{
    DatePublished = _article.PublishedAt,
    DateModified  = _article.UpdatedAt,
    Author        = [new SchemaPerson { Name = _article.AuthorName }],
});
```

A supplied node **replaces** the derived one of the same type. Nodes: `SchemaWebPage`,
`SchemaArticle`, `SchemaOrganization`, `SchemaWebSite`, `SchemaPerson`, `SchemaBreadcrumbList`.
Structured data is skipped entirely on a `noindex` page.

## Indexability

```csharp
o.Indexable = null;   // default → derived from Permission is null
```

Precedence, highest first — the first three are Site-level and a page cannot argue with them:

| State | `robots` meta | `robots.txt` |
| --- | --- | --- |
| Site not indexable | `noindex, nofollow` | `Disallow: /` |
| culture not in `IndexedCultures` | `noindex, follow` | `Disallow: /{segment}/` |
| `Metadata.Robots = "…"` | that string verbatim (`""` → no tag) | — |
| `Metadata.NoIndex` | `noindex, follow` | — |
| otherwise | `SeoDocument.DefaultRobots` | normal |

`DefaultRobots` is `index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1`.
The `index, follow` half is what a crawler assumes anyway; the three limits are **not** — they
default to conservative values, so a site that says nothing is opting into a smaller SERP
presentation without meaning to. `max-image-preview:large` is what makes a result eligible for the
large image thumbnail. Emitting the positive form also removes a real ambiguity: "no tag" and "the
head renderer never ran" look identical in view-source.

## robots.txt, sitemap.xml and llms.txt

All three live in **`Zonit.Extensions.Website.Sitemaps`**, configured as one tree:

```csharp
app.UseWebsite("/", o => o.Indexing(x =>
{
    x.Robots.Disallow("/search").Allow("/search/help");

    x.Llms.Summary = "What this site is, for a reader who has never seen it.";
    x.Llms.AddLink("Docs", "/docs", "API reference — the source for parameter-level questions.");
}));
```

One call maps all three endpoints. **The sitemap address is not restated**: `robots.txt` takes it
from the same call that mounted it. That is the whole reason they share a tree — a `robots.txt`
naming a sitemap that moved is a *valid file*, so the mistake never surfaces as an error, and
splitting the configuration made that agreement something the host retyped by hand.

Generated per request from live state, so the directives cannot drift from what the pipeline does:
a Site behind a permission is `Disallow: /`, a language outside `IndexedCultures` gets its segment
disallowed, framework paths (`/_framework/`, `/_blazor`) are disallowed automatically. Served
inside the Site's branch, so a mount at `/admin` answers at `/admin/robots.txt`.

`llms.txt` stays off until the first `AddLink`. A file whose only content is the site title tells
an agent nothing it could not read from the page, and an empty one is worse than none — it looks
authoritative and says nothing. Write link descriptions about **when** the resource is the right
thing to read; an agent picks a source by its description, not its name.

A Site that never calls `Indexing()` serves none of the three.

## Configuration

Only what a **deployment** decides — everything else is code or tenant settings:

```json
{
  "Website": {
    "/":      { "Indexable": true, "IndexedCultures": [ "en-us", "pl-pl" ] },
    "/admin": { "Indexable": false }
  }
}
```

Both keys hot-reload. Arrays **replace** the code defaults rather than extending them. The key is
the mount path; `"/"` and `""` both name the root, and a trailing slash is ignored.

`IndexedCultures` is a different axis from `SupportedCultures`: the first answers "may this
language appear in search results", the second "can it be rendered". They differ while a
translation is finished but unreviewed.

## Where the rest lives

- `.zonit/extensions/website/hosting.md` — `AddWebsite` / `UseWebsite`, mount ordering.
- `.zonit/extensions/website/document.md` — `AppBase`, `DocumentOptions`, appearance, switchers.
- `.zonit/extensions/cultures/cultures.md` — languages, translations, `T()`.
- `.zonit/extensions/tenants/tenants.md` — the settings tree these read from.
