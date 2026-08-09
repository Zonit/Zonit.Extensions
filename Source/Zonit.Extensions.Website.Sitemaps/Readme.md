# Zonit.Extensions.Website.Sitemaps

Sitemap generation for [Zonit.Extensions.Website](https://www.nuget.org/packages/Zonit.Extensions.Website)
hosts. Plug-ins declare what to publish; the package owns everything that makes a sitemap valid.

## Install

```bash
dotnet add package Zonit.Extensions.Website.Sitemaps
```

```csharp
builder.Services.AddSitemap();

app.UseWebsite("/", o =>
{
    o.MapEndpoints(ep => ep.MapSitemap());
    o.Robots.Sitemap("/sitemap.xml");
});
```

## Declare a source

```csharp
internal sealed class NewsSitemap(IArticleRepository articles) : ISitemapSource
{
    public string Name => "news";

    public async IAsyncEnumerable<SitemapEntry> GetAsync(
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var a in articles.StreamPublishedAsync(token))
            yield return new SitemapEntry($"/news/{a.Slug}", LastModified: a.UpdatedAt);
    }
}
```

Registered by the area that owns the content, so installing a plug-in adds its URLs and removing
it removes them — there is no list in the host to keep in step:

```csharp
public void ConfigureServices(IServiceCollection services)
    => services.AddSitemapSource<NewsSitemap>();
```

## What you get

Absolute URLs, the mount path base, expansion across every indexed culture with a full `hreflang`
cluster, translated route segments and slugs, both protocol limits (50 000 URLs **and** 50 MB —
the byte one binds first at twenty languages), splitting into numbered parts, the sitemap index,
and an origin-keyed cache with stampede protection.

Sources stream: a table with two million rows is never materialised.

## Documentation

Installing the package writes an AI-assistant guide into your repository's `.claude` / `.cursor` /
`.github` surfaces. The same document is browsable at
`Instruction/extensions/sitemaps/sitemaps.md`.

## License

MIT
