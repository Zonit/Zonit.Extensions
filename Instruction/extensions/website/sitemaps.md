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
| `Path` | Site-relative, no culture segment. Required. |
| `LastModified` | The one field crawlers use to decide whether re-fetching is worth it. Supply it. |
| `ChangeFrequency` | Hint. Omit rather than guessing. |
| `Priority` | 0.0–1.0, relative **within this site**. Very little effect; leave unset. |
| `Cultures` | Subset this entry exists in. `null` = every indexed culture. |
| `PathsByCulture` | Per-culture path when the **slug** is translated. Routes whose static segment is translated are resolved from the area's `Routes` automatically. |

### `ISitemapSource.IsEnabled`

Defaults to `true`. Lets a plug-in stay registered while its feature is off, instead of the host
conditionally registering it.

## What the package does for you

Absolute URLs, mount path base, expansion across indexed cultures, the full `hreflang` cluster per
URL, translated route segments, both size limits, splitting into numbered parts, the sitemap
index, and an origin-keyed cache with stampede protection.

Only **indexed** cultures are listed: a sitemap is a list of pages a crawler should fetch, and
listing one that answers `noindex` wastes the fetch and contradicts the page.

## Options

```csharp
builder.Services.AddSitemap(o =>
{
    o.MaxUrlsPerFile  = 45_000;                  // protocol limit 50 000, margin left
    o.MaxBytesPerFile = 45 * 1024 * 1024;        // protocol limit 50 MB
    o.CacheDuration   = TimeSpan.FromHours(1);
});
```

Generation walks every source, which on a large site is real database work, and an uncached
endpoint is a free denial-of-service primitive. To publish immediately after content changes,
inject `SitemapCache` and call `Invalidate()`.

## Output

```
/sitemap.xml            → sitemapindex → /sitemap/news-1.xml, /sitemap/pages-1.xml
/sitemap/{name}-{n}.xml → urlset with xhtml:link alternates
```

Unknown part names answer 404.

### One address per Site, never one per language

`robots.txt`, `sitemap.xml`, `llms.txt` and the sitemap parts are not translated, so on a prefixed
Site every language-prefixed spelling answers `301` to the unprefixed form:

```
/pl/llms.txt         → 301 → /llms.txt
/pl-pl/sitemap.xml   → 301 → /sitemap.xml
/de/robots.txt       → 301 → /robots.txt
```

Nothing to configure. The redirect fires before generation, so a prefixed request never triggers
the sitemap walk. Query strings are preserved; a Site mounted at `/shop` keeps its mount
(`/shop/pl/robots.txt` → `/shop/robots.txt`).

## Where the rest lives

- `.zonit/extensions/website/seo.md` — culture URLs, indexed cultures, `robots.txt`.
- `.zonit/extensions/website/areas.md` — `IWebsiteArea` / `IWebsiteServices`.
