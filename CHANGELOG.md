# Changelog

Running index of what shipped, when. Newest first. Detailed migration notes for a release live in
`Docs/RELEASE-NOTES-<version>.md`; this file is the index an assistant reads to answer "is this API
new, renamed, or gone?".

Every package in this repository versions together — there is no partial upgrade path.

---

## 10.0.0-preview.20 — 2026-08-11

Everything here is `Zonit.Extensions.Website` and `Zonit.Extensions.Tenants`. No API is removed;
the two additions are opt-in.

### Breaking — the culture prefix is valid for pages, and for nothing else

The rule is enforced from the endpoint table, not from the path's spelling. `CultureRouteGate` runs
right after routing on prefixed Sites: the matched endpoint either carries
`ComponentTypeMetadata` — the marker every Razor Components page endpoint has — or the prefixed
address does not exist.

```
/pl/pricing                      → 200 — a page, the point of the prefix
/pl/_content/acme/app.css        → 404
/pl/llms.txt                     → 404
/pl/api/ping (MapEndpoints)      → 404 — consumer endpoints have one address, unprefixed
/pl/anything.glb                 → 404 — no extension list to outrun
/pl/_framework/… , /pl/_blazor…  → served — see the carve-out below
```

An earlier draft of this rule keyed on a static-extension list. That list is a losing race — there
are thousands of file formats, and every one it does not name becomes a page-shaped request that
splits the segment and serves a duplicate. The set that may carry a language is the set of page
routes, and that set already exists: it is the router's. `WebsiteRequestFilter`'s extension list is
demoted to a fast path (answers the obvious cases before routing, empty-body, no error-page
re-execution); correctness no longer depends on it.

**The framework carve-out.** `/_blazor*` (circuit negotiate, WebSocket, initializers) and
`/_framework/*` (WebAssembly boot resources, dev hot-reload) are fetched by the client relative to
`document.baseURI`, which on a prefixed Site deliberately carries the language — no server-side
rooting can reach those fetches. They split and serve under any prefix; `robots.txt` already
disallows both paths, and nothing under them is content. Without the carve-out, WebAssembly render
modes and dev hot reload break under a culture prefix — the extension-list draft had exactly that
latent bug.

**The unprefixed redirect is endpoint-typed too.** The 302 that sends `/pricing` into the
visitor's language used to be decided before routing, from request shape — and sent every
browser-shaped GET into the language, including consumer endpoints, whose prefixed address then
correctly answered 404: a download link clicked in a browser bounced to a dead URL. The gate now
decides both directions from the matched endpoint: a page reached without its language redirects
into it; anything else serves at the one address it has. A side effect worth having: a garbage
path like `/xx/foo` answers a plain 404 instead of a 302-then-404 hop.

**What this breaks.** (1) Markup still emitting a relative asset URL — `@Assets` rooting is a
compile-time binding, so rebuild every project and check a prefixed page's network tab. (2) A
consumer minimal-API endpoint that was reachable under `/pl/…` by accident: it now has exactly one
address. There is deliberately no opt-in to put a non-page endpoint under the prefix — an endpoint
that wants the visitor's language reads it from the culture cookie or `Accept-Language`, not from a
URL that multiplies its addresses. (3) An unprefixed *localized* route spelling
(`/aktualnosci/x` where the canonical route is `/news/{slug}`) no longer bounces into the
language — routing cannot match it, so it 404s; the canonical unprefixed spelling (`/news/x`)
still redirects, translated, in one hop.

### Fixed — assets carried the culture segment

`<base href>` is `/pl/` on a prefixed Site, and every asset the document shell emitted was a
relative URL, so the browser fetched `/pl/_content/acme/app.css`, `/de/_content/acme/app.css`, and
so on. One stylesheet, one script, one image — as many URLs as the Site has languages, each its own
cache entry in every intermediary, each separately indexable. The culture belongs to what a page
says, not to the files it loads.

The shell now roots what it emits at the Site's **mount**, culture excluded — stylesheets, scripts,
the scoped-CSS bundle, the favicon, `blazor.web.js` and the import map:

```
/pl/            → /_content/acme/app.css          (was /pl/_content/acme/app.css)
/panel/de/      → /panel/_content/acme/app.css    (was /panel/de/_content/acme/app.css)
```

The mount is kept because `MapStaticAssets` is registered inside the Site's branch — a bare
`/_content/…` would leave the branch and 404 wherever no root Site exists. The value comes from
`ICultureUrlFeature.SitePathBase`, which is the path base from before the culture was appended.

The import map needed the same treatment and could only get it here: an import map resolves against
the document base URL exactly like `src` does, and the specifier a module is imported under is the
map's *key*, so no call site can correct it. Every entry now appears under **both** spellings —
`/_content/lib/x.js` and `./_content/lib/x.js` — pointing at the same rooted URL, because a library
loading itself with `JS.InvokeAsync<IJSObjectReference>("import", "./_content/lib/x.js")` would
otherwise go unmatched, lose its fingerprint and look for a file under the language segment.

Override `AppBase.AssetBase` or `AppBase.RootedImportMap()` to change either.

`@Assets["…"]` written in markup is covered too: `ExtensionsBase` now hides
`ComponentBase.Assets` with a rooted lookup, so `<img src="@Assets["_content/acme/logo.png"]">`
in any component deriving from it (`PageBase`, `PageViewBase`, `PageEditBase`, or
`@inherits ExtensionsBase`) emits `/_content/acme/logo.png` with no change at the call site.
Off-site URLs and fingerprints pass through untouched.

**Requires a recompile, not just a new binary.** Member lookup is resolved by the compiler, so an
assembly still built against `preview.19` keeps binding to the framework's own collection and its
markup keeps the old URLs. Rebuild every project whose components use `@Assets`.

**Not covered:** components on `LayoutComponentBase` or plain `ComponentBase`, and code that
casts to `ComponentBase` before reading `Assets` — hiding is not virtual dispatch. Root those by
hand (`/_content/…` on a Site mounted at `/`).

### Added — `PageMeta.Cultures`: content that is not translated everywhere

Content translated per item arrives unevenly — a signal exists in eight of ten languages. The Site
still routes `/cs/signals/x`, the page still renders with its fallback rendition and a notice, and
the same English text now answers at three addresses, each claiming through `hreflang` to be a
distinct language version. That is a claim a crawler can check and find false.

```csharp
protected override async Task OnInitializedAsync(CancellationToken token)
{
    _signal = await _signals.GetAsync(Id, token);
    Meta.Cultures = _signal.Translations.Keys.Select(c => new Culture(c)).ToArray();
}
```

One declaration, two consequences that cannot drift apart:

- rendering in a language outside the set is `noindex, follow` — the fallback page stays reachable
  and its links crawlable, it is just not offered as a result;
- the `hreflang` cluster on the versions that *do* exist lists only those. This is the half that is
  easy to miss: a cluster naming a version that answers `noindex` is discarded whole, so without
  the filter the eight real translations lose their clustering because of the two missing ones.

Verified: with content in `en` + `pl` on a three-language Site, `/en/` and `/pl/` are indexable and
cluster `[en, pl, x-default]`; `/de/` is `noindex, follow` with no cluster at all.

`Meta.NoIndex = true` remains the blunt form for a page that should never be a result regardless of
language. `Meta.Robots` still overrides both.

### Breaking — `SitemapEntry.Cultures` moved to the front and changed type

One way to declare it, not two. The languages come first because they scope everything after them —
they decide which files the entry appears in at all, which is not the same kind of fact as
`Priority`, and as a trailing optional it read like one:

```csharp
// content translated per item
yield return new SitemapEntry(
    signal.Translations.Keys.Select(c => new Culture(c)).ToArray(),
    $"/signals/{day:yyyy-MM-dd}/{signal.Id}",
    LastModified: signal.ClosedAt ?? signal.CreatedAt);

// literal set
yield return new SitemapEntry(["en-us", "pl-pl"], "/about/press-kit");

// everywhere — no ceremony
yield return new SitemapEntry("/about");
```

`IReadOnlyList<Culture>`, not `IReadOnlyList<string>`. `PageMeta.Cultures` answers the same question
for the rendered page and was already typed; one typed and one not is how the two drift apart, and
they must be fed from the same place. The value object converts from a string literal implicitly, so
collection expressions need nothing extra.

The two constructors deliberately share parameter *names*, capitals included, so `LastModified:`
means the same thing in both. The first draft used camelCase in the second constructor and the
mismatch surfaced as an overload-resolution error pointing two arguments away from the cause.

**This is a binary break, and it fails loudly.** An assembly compiled against `preview.19` throws
`MissingMethodException` naming the old signature the first time its source is walked — one stale
plug-in takes the whole `/sitemap.xml` to 500 rather than silently dropping its URLs. Rebuild every
project that implements `ISitemapSource`.

### Changed — one sitemap file per language, and the `hreflang` cluster left to the page

Two independent changes to `SitemapOptions`, both aimed at the same symptom: on a prefixed Site the
sitemap grew as the square of the language count.

**`GroupByCulture` (new, default `true`).** Parts are now named `/sitemap/news-pl-1.xml` instead of
`/sitemap/news-1.xml`. Search Console reports index coverage *per submitted file*, so a combined
sitemap answers "20 000 URLs, 14 000 indexed" — which names no problem — while split by language it
answers "de 400/400, pl 380/400, bg 12/400", which names one. It also stabilises file identity:
ungrouped, adding a page shifts every later entry across part boundaries and every part changes;
grouped, a page added in Polish rewrites the Polish parts only. A language a source contributes
nothing to produces no file at all.

Streaming is preserved — one writer stays open per language rather than the source being walked
once per language, since a source is a database query. Peak memory is `languages × current part`;
lower `MaxBytesPerFile` on a very large multilingual source rather than raising it.

**`Alternates` (new, default `false`) — breaking.** The `xhtml:link` cluster is no longer written
into the sitemap. Sitemap and HTML are alternative ways to declare the same thing, and this package
already emits the HTML form on every indexable page: complete, reciprocal by construction (one
policy generates every page's cluster, so no page can disagree with another), and including
`x-default`, which the sitemap form never had. Declaring it twice added no signal and cost a
square — measured here, 1 000 pages in 20 languages is 400 000 link elements and ~38 MB against a
50 MB protocol ceiling; without them, 1.8 MB. Set `Alternates = true` if the shell was replaced
with one that does not render `PageHead`.

**Partial translations were already supported and are now documented.** `SitemapEntry.Cultures`
takes the languages an entry actually exists in — read it from wherever the page reads its
translations:

```csharp
yield return new SitemapEntry(
    $"/signals/{day:yyyy-MM-dd}/{signal.Id}",
    LastModified: signal.ClosedAt ?? signal.CreatedAt,
    Cultures: signal.Translations.Keys);
```

An entry translated into English and Polish is listed in those two files and absent from the German
one; an empty list drops it everywhere; tags outside the Site's indexed set are ignored, so a stale
row cannot conjure a language the Site does not serve. When `Alternates` is on, the cluster is
built from the languages that exist rather than the Site's full list — a cluster naming a version
that does not answer is discarded whole by search engines, taking the working languages with it.

### Fixed — the error page lost the language and emitted an impossible canonical

`UseStatusCodePagesWithReExecute` and `UseExceptionHandler` replay the request with the error route
in `Request.Path` — but with `Request.PathBase` still carrying what the first pass moved into it.
The middleware resolved from scratch on that second pass and was wrong twice, silently:

- the culture segment was no longer in `Path`, so `/pl/missing` looked unprefixed and a Polish
  visitor's 404 rendered in English (`<html lang="en-US">` under `<base href="/pl/">`);
- `PathBase` was then read as the mount, so `SitePathBase` became `/pl` and every URL built on top
  of it gained a second language segment — the canonical came out as `/pl/en/not-found/404`, an
  address that cannot exist.

The first pass already resolved all of this. The second now reuses it: same culture, same
`PathBase`, feature re-pointed at the error route. `/pl/missing`, `/de/missing` and `/missing` are
now the same page in three languages instead of three different documents.

**An error render also emits no canonical and no cluster.** A canonical asserts that content lives
at a URL; on a 404 there is no content, and the address it stands in for does not exist. Pointing
it at a real page invites consolidation onto that page, and pointing it at the error route
advertises the error route as content. The page is `noindex, follow` — which already suppressed
`hreflang`, `x-default` and structured data — and the canonical is now withheld too. Nothing in a
page's own declaration can know it is being rendered for a failed address; the pipeline can, so the
decision is made there.

### Fixed — `/pl/signals/` rendered next to `/pl/signals`, each claiming to be canonical

A trailing slash was invisible to the canonical comparison, so both spellings rendered — and each
emitted *itself* as canonical, so they were not even competing for one index entry; both were
claiming it. Trailing slashes now fold into the same comparison as the culture spelling:
`/pl/signals/` and `/pl/signals///` answer `301 → /pl/signals`, query preserved. The language root
keeps its slash (`/pl/`), which is what `PathBase + Path` reconstruct to. Unprefixed Sites get the
same rule for browser-shaped requests (GET/HEAD, not `/_*`, not an error re-execution), and the
unprefixed redirect normalizes before building its target so `/signals/` reaches `/pl/signals` in
one hop, not two.

### Fixed — `HEAD` on `robots.txt` / `llms.txt` / `sitemap*.xml` answered 404

`MapGet` registers GET alone; an unmatched HEAD does not become 405 — it falls off the endpoint
table and reads as "this site has no robots.txt" to every link checker and uptime probe that HEADs
before it GETs. All four endpoints now register GET and HEAD; Kestrel discards the body on HEAD by
itself. (HEAD on *pages* still answers 404 — that is `MapRazorComponents`, upstream.)

### Fixed — `.avif` and `.webmanifest` missing from the static-extension list

Both fell through to the page pipeline, so under a language prefix they split-and-served — the
exact duplicate the 404 rule exists to prevent, surviving on two file types. Added to
`WebsiteRequestFilter`.

`AssetPaths` also gained `Versioned(path)` — hiding `ComponentBase.Assets` hid the collection the
`AssetVersioning.Versioned` extension binds to, so `@Assets.Versioned(…)` would have stopped
compiling in exactly the components the hiding covers. The result is rooted like the indexer's.

### Fixed — `llms.txt` answered 404 on a Site whose pages all declared themselves

`Llms.Enabled` was only ever set by `AddLink`, so a Site relying on `[WebsiteLlms]` — the shape the
attribute exists to encourage — had plenty to say and returned 404 saying it. The endpoint now also
counts declared pages.

### Fixed — `schema.org` `sameAs` listed 6 of 12 social platforms

`SchemaComposer` enumerated its own subset, so Reddit, Twitch, Threads, Discord, Pinterest and
Snapchat were filled in by the tenant and silently dropped from structured data. Both callers now
go through `SocialMediaModel.All()`; the list exists once.

### Added — `SocialMediaSetting.Custom`

Named properties cover the twelve platforms worth a first-class name. Anything else — a Facebook
group, a community forum, a status page — goes in `Custom` as label → URL:

```json
"SocialMedia": {
  "Facebook": "https://www.facebook.com/acme/",
  "Custom": { "Facebook group": "https://www.facebook.com/groups/acme/" }
}
```

`All()` enumerates named entries then custom ones; `All(includeCustom: false)` is what `sameAs`
uses. Custom links stay out of structured data on purpose: `sameAs` asserts *this page identifies
this organisation*, which is true of a profile and not of a status page.

### Added — `llms.txt` gained a languages line and an `## Optional` section

```
Available in 3 languages — prefix any path with the language segment: /en/, /pl/, /de/.
An unprefixed path serves the reader's own language.

## Optional
- [Sitemap](https://example.com/sitemap.xml): every indexable URL, with per-language alternates.
- [Facebook](https://www.facebook.com/acme/)
```

The languages line is the one fact an agent cannot derive by reading a page — the `hreflang`
cluster sits in the head of pages it has not fetched, and the prefix shape is policy, not a link.
`## Optional` is the convention's marker for material to skip when context is short, which is where
social profiles belong: they answer questions about identity, not about the product.

### Changed — `robots.txt` and `llms.txt` send `Cache-Control: public, max-age=3600`

`llms.txt` is not crawled on a schedule; it is pulled on demand by agent tooling (IDE assistants,
MCP servers) that may re-read it per session or per question, and the header is the only way to
tell it not to. One hour matches the sitemap's regeneration window, so the two never disagree about
how fresh the Site claims to be. Nothing is cached server-side — generation is a `StringBuilder`
over an in-memory registry.

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
