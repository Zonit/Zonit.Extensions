# Crawling and indexing — robots.txt, sitemap.xml, llms.txt

Part of `Zonit.Extensions.Website`. There is no separate package: the generator needs
`ICurrentSite`, the culture policy and the localized-route table, so it could never stand alone,
and splitting it only made `robots.txt` conditional on remembering an install.

The three files are one statement in three formats and only work if they agree — `robots.txt` must
name the sitemap's real address, and neither may contradict the culture policy about which
languages are indexed. One options tree is what lets the framework derive that instead of asking
you to keep it in step.

## Read this before you write code

| Trap | What actually happens |
| --- | --- |
| Listing sources in the host | There is no list. An area registers its own maps in `ConfigureServices`; the generator receives them injected. Installing a plug-in adds its URLs, removing it removes them. |
| Returning `List<T>` from a source | The contract is `IAsyncEnumerable<SitemapEntry>` on purpose. Yield as the database yields; a source with two million rows must never be materialised. |
| Absolute URLs in `SitemapEntry.Path` | Give a site-relative path with **no** culture segment and no mount base. The package adds origin, mount and language. |
| Only counting URLs | Both protocol limits are enforced. At twenty languages the 50 MB limit binds long before 50 000 URLs, because every entry emits N `<url>` elements each carrying N alternates. |
| Mapping the endpoint globally | `MapSitemap()` goes inside a Site's `MapEndpoints` hook. A panel publishing a sitemap advertises exactly the URLs it is trying to keep out of search. |

## Setup

```csharp
builder.Services.AddWebsite(w => w.AddArea<NewsArea>());   // AddSitemap() runs inside

app.UseWebsite("/", o => o.Indexing());   // robots.txt + sitemap.xml + llms.txt
```

`Indexing()` maps all three and advertises the sitemap in `robots.txt` without you naming its
address twice. Reach for `o.MapEndpoints(ep => ep.MapSitemap())` only to publish a sitemap that
`robots.txt` should *not* name.

## Static pages — the `[WebsiteSitemap]` attribute

A page publishes itself. Collected at **build time** by a source generator, so nothing scans,
reflects or allocates at run time.

```csharp
@page "/ebook"
@attribute [WebsiteSitemap(Change = ChangeFrequency.Monthly, Priority = 0.8)]

// or, when the route is a const in code-behind — the generator resolves it:
[Route(Route)]
[WebsiteSitemap(Change = ChangeFrequency.Daily)]
[WebsiteLlms("Settled outcomes for every signal — the source for hit-rate questions.")]
public sealed partial class Signals : PageBase { public const string Route = "/signals"; }
```

**Opt-in, deliberately.** A page is in the sitemap because someone wrote the attribute. The
opt-out default reads as safer and is not: its failure mode is a page written in a hurry — an
internal tool, a half-finished feature — being *advertised to search engines* before anyone
decided it should be public. Forgetting the attribute costs a listing, which is visible in Search
Console and recoverable. Forgetting to remove one publishes something.

**No `@page` duplication.** The generator reads the route from the same file as the attribute, or
from `[Route(...)]` on the same class. `[WebsiteSitemap("/explicit")]` exists for the case where neither
is visible.

**Parameterised routes warn.** `/signals/{slug}` is a template, not a URL, so it cannot go into the
XML — and the build says so by name:

```
warning ZONITSM0001: 'Signals.Details' declares [WebsiteSitemap] but its route '/signals/{slug}' has a
                     parameter. Enumerate the real URLs with an ISitemapSource, or remove [WebsiteSitemap].
```

**Authorized pages need nothing.** `[Authorize]`, `[RequirePermission]` and `[RequireRole]` imply
`noindex, follow` on the rendered page. Not a `Disallow`: that file is written for anyone to read,
so listing gated paths publishes a map of where the interesting parts are — and it would not work
anyway, since a disallowed URL can still be indexed bare, the crawler being forbidden from
fetching the page that would have told it not to.

**`noindex` on a public page** stays where it was: `PageMeta.NoIndex`, or `PageMeta.Robots` for a
directive the framework does not model.

## `[WebsiteLlms]` — the site briefed for an agent

Separate attribute, because it answers a different question. A sitemap is an inventory of
everything worth crawling; `llms.txt` is a briefing — the handful of pages that explain what the
site is and where its real answers live. Most pages want only `[WebsiteSitemap]`.

Write the description about **when** to read the page. An agent picks a source by its description,
not its name.

Entries merge with anything the host declared through `x.Llms.AddLink(...)`, which covers what no
page can: an external doc, a dataset, a section that is not one route. `Section` groups them; the
summary block comes from `x.Llms.Summary`, then the tenant's `Site.About`, then its meta
description.

## A source

```csharp
internal sealed class NewsSitemap(IArticleRepository articles) : ISitemapSource
{
    public string Name => "news";          // becomes /sitemap/news-1.xml

    public async IAsyncEnumerable<SitemapEntry> GetAsync(
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var a in articles.StreamPublishedAsync(token))
            yield return new SitemapEntry(
                $"/news/{a.Slug}",
                LastModified: a.UpdatedAt,
                ChangeFrequency: ChangeFrequency.Weekly,
                PathsByCulture: new Dictionary<string, string> { ["pl-pl"] = $"/aktualnosci/{a.SlugPl}" });
    }
}
```

Registered by the area that owns the content, not by the host:

```csharp
public sealed class NewsArea : IWebsiteArea, IWebsiteServices
{
    public string Key => "news";

    public void ConfigureServices(IServiceCollection services)
        => services.AddSitemapSource<NewsSitemap>();
}
```

`AddSitemapSource` uses `Add`, not `TryAdd` — several maps per area is normal (pages, articles,
categories) and `TryAdd` would keep only the first.

### `SitemapEntry`

| Member | Notes |
| --- | --- |
| `Cultures` | Languages this entry exists in — **first constructor parameter**, `IReadOnlyList<Culture>`. Omit the overload for every language. |
| `Path` | Site-relative, no culture segment. Required. |
| `LastModified` | The one field crawlers use to decide whether re-fetching is worth it. Supply it. |
| `ChangeFrequency` | Hint. Omit rather than guessing. |
| `Priority` | 0.0–1.0, relative **within this site**. Very little effect; leave unset. |
| `PathsByCulture` | Per-culture path when the **slug** is translated. Routes whose static segment is translated are resolved from the area's `Routes` automatically. |

### Content that is not translated everywhere

Translations that live in a row rather than a resource file arrive unevenly. Read `Cultures` from
the same place the page reads its renditions:

Languages come **first**, because they scope everything after them — they decide which files the
entry appears in at all. There is one way to write it:

```csharp
yield return new SitemapEntry(
    signal.Translations.Keys.Select(c => new Culture(c)).ToArray(),
    $"/signals/{day:yyyy-MM-dd}/{signal.Id}",
    LastModified: signal.ClosedAt ?? signal.CreatedAt);

yield return new SitemapEntry(["en-us", "pl-pl"], "/about/press-kit");   // literal set

yield return new SitemapEntry("/about");                                 // every language
```

`IReadOnlyList<Culture>`, matching `PageMeta.Cultures` — one typed and one not is how the two drift.

The page renders its own half of this with `Meta.Cultures` — same question, same source, see
`.zonit/extensions/website/seo.md`. A sitemap that omits a language while the page still clusters
it is the one inconsistency worth avoiding here.

| `Cultures` value | Result |
| --- | --- |
| `null` | listed in every indexed language |
| `["en-us", "pl-pl"]` | in the `en` and `pl` files, absent from `de` |
| `[]` | dropped everywhere — nothing to crawl |
| contains `fr-fr`, not served | ignored; a stale row cannot invent a language |

Listing a language whose rendition is missing sends a crawler to a page that cannot render, and —
with `Alternates` on — invalidates the whole cluster it belongs to, taking the working languages
down with it. When `Alternates` is on the cluster is built from the languages that exist, not from
the Site's full list.

### `ISitemapSource.IsEnabled`

Defaults to `true`. Lets a plug-in stay registered while its feature is off, instead of the host
conditionally registering it.

## What the package does for you

Absolute URLs, mount path base, expansion across indexed cultures, translated route segments, both
size limits, grouping per language, splitting into numbered parts, the sitemap index, and an
origin-keyed cache with stampede protection.

Only **indexed** cultures are listed: a sitemap is a list of pages a crawler should fetch, and
listing one that answers `noindex` wastes the fetch and contradicts the page.

## Options

```csharp
builder.Services.AddSitemap(o =>
{
    o.MaxUrlsPerFile  = 45_000;                  // protocol limit 50 000, margin left
    o.MaxBytesPerFile = 45 * 1024 * 1024;        // protocol limit 50 MB
    o.CacheDuration   = TimeSpan.FromHours(1);
    o.GroupByCulture  = true;                    // default: one file per language
    o.Alternates      = false;                   // default: hreflang stays in the page
});
```

Generation walks every source, which on a large site is real database work, and an uncached
endpoint is a free denial-of-service primitive. To publish immediately after content changes,
inject `SitemapCache` and call `Invalidate()`.

## Output

```
/sitemap.xml                    → sitemapindex
/sitemap/{name}-{culture}-{n}.xml   grouped (default, prefixed Sites)
/sitemap/{name}-{n}.xml             ungrouped, or a Site with no culture prefix
```

Unknown part names answer 404.

### Why one file per language

Search Console reports index coverage **per submitted file**. Combined, it says "20 000 URLs,
14 000 indexed" and names no problem; split, it says "de 400/400, pl 380/400, bg 12/400" and names
one. File identity is also stable: ungrouped, adding a page shifts every later entry across part
boundaries; grouped, a page added in Polish rewrites the Polish parts only.

One writer stays open per language while a source streams — peak memory is
`languages × current part`. On a very large multilingual source lower `MaxBytesPerFile`, don't
raise it.

### Why `hreflang` is not in the sitemap by default

Sitemap and HTML are alternative ways to declare the same thing, and every indexable page already
carries the HTML form — complete, reciprocal by construction, with `x-default`. Emitting both adds
no signal and costs a square: 1 000 pages × 20 languages is 400 000 link elements and ~38 MB
against a 50 MB ceiling. Without them: 1.8 MB.

Turn `Alternates = true` on only when the HTML cluster is not there to be found — a shell replaced
with one that does not render `PageHead`.

### One address per Site, never one per language

```
/pl/llms.txt         → 404
/pl-pl/sitemap.xml   → 404
/de/robots.txt       → 404
```

Nothing to configure, and nothing specific to these three: `CultureMiddleware` answers *any*
static-extension request carrying a language segment with 404, before routing. A Site mounted at
`/shop` still serves `/shop/robots.txt`. See `.zonit/extensions/website/document.md` for the asset
side of the same rule.

## Where the rest lives

- `.zonit/extensions/website/seo.md` — culture URLs, indexed cultures, `robots.txt`.
- `.zonit/extensions/website/areas.md` — `IWebsiteArea` / `IWebsiteServices`.
