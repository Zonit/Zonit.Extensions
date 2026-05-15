# Case: should we extract `Zonit.Services.Website` between `Zonit.Extensions.Website` and `Zonit.Services.Dashboard`?

> Decision deferred to next session. This document captures the case so we can decide
> with full context.

## Today's stack

```
Zonit.Extensions.Website        ← framework: AddWebsite, Areas, Sites, providers, middleware
       ↑ direct dependency
Zonit.Services.Dashboard        ← MudBlazor, drawer extensions, dashboard layout, settings
       ↑ direct dependency
SeoMatch / consumer apps        ← AddDashboardService + custom areas
```

`Zonit.Extensions.Website` is **framework primitives only**. Anything heavy
(MudBlazor, ImageSharp, controllers boilerplate, sitemap, mail templates) lives in
the consumer or in adjacent libraries (`Zonit.Libraries.Website.*`,
`Zonit.Services.Dashboard`).

## What the user is proposing

Insert a new project between framework and dashboard:

```
Zonit.Extensions.Website         ← unchanged — framework core
       ↑
Zonit.Services.Website   ← NEW   ← "everything useful for a public website"
       ↑                            (ImageSharp wiring, SignalR defaults, sitemap,
       ↑                             mail, common security headers, etc.)
Zonit.Services.Dashboard         ← thin: MudBlazor + dashboard layout, depends on Services.Website
       ↑
SeoMatch / consumer apps
```

## What would live in `Zonit.Services.Website`

Reading `SeoMatch.Server.Program.cs` (the canonical real-world consumer), the recurring
non-framework boilerplate that every public website host repeats:

- `AddImageSharp()` + `AddImageSharpService()` + `app.UseImageSharp()` (per-Site)
- SignalR defaults (timeouts, max-message-size, detailed errors off in prod)
- `AddSitemap()` and the matching `MapGet("/sitemap.xml", …)` plumbing
- Mail/SMTP wiring via `AddMailsExtension`
- `AddSettingsExtension` (key-value site settings)
- Hsts/Compression/TextEncoding/Proxy *service* wrappers (already in
  `Zonit.Extensions.Website` namespace as static helpers — would consolidate here)
- `AddControllers()` opinionated wiring + `MapControllers()` per Site
- Logging filters for noisy sources (HttpClient, EventMessage, etc.)
- Common HTTP→HTTPS redirect / canonical-host enforcement
- Common error pages (`/error`, `/error/{code}`)
- robots.txt / ads.txt minimal-API endpoints
- StaticWebAssetsLoader

The consumer would then write:

```csharp
builder.Services.AddWebsiteService(opts =>
{
    opts.AddArea<HomeArea>();
    opts.AddArea<MyShopArea>();

    opts.ImageSharp = true;          // wire ImageSharp end-to-end
    opts.Sitemap = true;             // wire sitemap.xml
    opts.SignalR.Timeouts = …;
});

app.UseWebsiteService<App>(o =>
{
    o.Directory = "";
    o.AddArea<HomeArea>();
    // ImageSharp middleware auto-installed via Site.AppHooks
});
```

`AddWebsiteService` would internally call `AddWebsite` plus the rest. Dashboard then
extends *this* (gets ImageSharp etc. for free).

## Pros

- **One opinionated layer per use-case**: framework / public-site / admin-dashboard.
  Consumers pick the layer that matches their need.
- **Removes per-app boilerplate**: SeoMatch's `Program.cs` is ~360 lines today, mostly
  service registrations. `AddWebsiteService` would collapse a lot of that.
- **Single place to upgrade defaults**: when ImageSharp ships v4 with new options,
  one PR to `Services.Website` updates every site.
- **Dashboard becomes lighter**: it's currently a mix of "dashboard-specific" (drawer
  extensions, MudBlazor) and "useful for any site" code (settings repository,
  StatusCodePages handler). A clean split clarifies what's what.
- **Aligns with Zonit naming**: `Zonit.Extensions.*` are protocol-level primitives,
  `Zonit.Services.*` are integrated stacks. A public-website stack belongs in
  Services.

## Cons

- **Overlap with `Zonit.Libraries.Website.*`**: SeoMatch already imports
  `Zonit.Libraries.Website` (HSTS / Compression / TextEncoding / Proxy service helpers,
  Sitemap). If we create `Zonit.Services.Website`, we'd need a clear story:
  - Does `Services.Website` wrap `Libraries.Website`?
  - Does `Libraries.Website` get absorbed into `Services.Website`?
  - Or do we delete `Libraries.Website` entirely and re-implement the small bits inline?
- **Third-party dependencies in mid-tier**: `Services.Website` would pull SixLabors.ImageSharp,
  potentially MailKit, potentially a sitemap library. That weight propagates to
  `Services.Dashboard`. Today Dashboard only depends on MudBlazor.
- **Decision overhead per feature**: every "is this a public-site concern or a
  framework concern?" debate becomes a routing problem. `AddControllers` is borderline,
  for example.
- **Risk of becoming a kitchen-sink**: similar to how `Microsoft.AspNetCore.App` ended
  up bundling everything. If we're not strict, `Services.Website` becomes another
  catch-all and consumers pull half of SixLabors / SignalR / Mail just to get
  `AddArea<HomeArea>()`.

## Hybrid alternative: keep `Zonit.Extensions.Website` slim, add **opt-in side-modules**

Instead of one new mid-tier project, ship optional packages:

- `Zonit.Extensions.Website.ImageSharp` → registers ImageSharp + provides an
  `IWebsiteArea`-compatible area class (`ImageSharpArea`) that calls
  `app.UseImageSharp()` in its `App(IApplicationBuilder)` hook.
- `Zonit.Extensions.Website.Sitemap` → `SitemapArea` that maps `GET /sitemap.xml`.
- `Zonit.Extensions.Website.Controllers` → `ControllersArea` that wires `AddControllers`
  + `MapControllers`.

Consumers compose: `AddWebsite(o => { o.AddArea<ImageSharpArea>(); o.AddArea<SitemapArea>(); … })`.
Each Area is a single-purpose package the consumer opts into; framework stays slim;
no mid-tier project needed.

This leverages the **existing Site/Area split** we just shipped — every "feature
module" is already a first-class concept (`IWebsiteServices` + `IWebsiteArea`), so
the natural extension is to ship more Areas, not more service-collection extensions.

## Decision matrix

| Aspect                                  | Mid-tier `Services.Website` | Side-modules `*.Website.X` |
|-----------------------------------------|------------------------------|----------------------------|
| Reduces consumer boilerplate            | ✅ big win                  | ✅ same — `AddArea<X>()`  |
| Avoids dependency bloat for slim hosts  | ⚠️ no — pulls everything    | ✅ pay-as-you-go          |
| Single upgrade path for defaults        | ✅                          | ❌ N packages to bump     |
| Plays well with Site/Area architecture  | ⚠️ duplicates the concept   | ✅ extends it             |
| Discoverability ("what's available?")   | ✅ one package              | ⚠️ requires docs index    |
| Aligns with Zonit naming convention     | ✅ `Services.*`             | ✅ `Extensions.*`         |

## Recommendation (for the next session)

**Side-modules win on architectural grounds**: the Site/Area split was specifically
designed so that "feature module = `IWebsiteArea`". A new mid-tier project would
re-introduce the very monolith we just broke up.

But the **boilerplate-reduction** argument is real, especially for SeoMatch-class apps.
A pragmatic path:

1. **Ship side-modules first** — `Zonit.Extensions.Website.ImageSharp`,
   `…Sitemap`, `…Controllers` — each a small package with one Area class.
2. **Re-evaluate after 3-4 modules ship**: if consumers are still boilerplating
   `AddArea<X>()` ten times in a row, *then* introduce a `Zonit.Services.Website`
   that's literally `AddArea<ImageSharpArea>().AddArea<SitemapArea>()…` as
   `AddWebsiteService(o => …)`. At that point we know exactly what the bundle
   contains and have proven each piece in isolation.

This keeps every step reversible and avoids speculative middleware.

## Projection: what would SeoMatch's `Program.cs` collapse to?

Today: **~360 lines** (`d:\GitVsCode\Stand\Source\Projects\SeoMatch\SeoMatch.Server\Program.cs`).
With the current state of `Zonit.Extensions.Website` (Site/Area split + comprehensive
`AddWebsite` / `UseWebsite("/", o => …)` defaults shipped this iteration), the file
would shrink to roughly:

```csharp
using SeoMatch.Server;
using Serilog;
using SixLabors.ImageSharp.Web.DependencyInjection;
using Zonit.Extensions;
using Zonit.Extensions.Website;
using Zonit.Services;
using Zonit.Services.Dashboard;
using Zonit.Services.Dashboard.DependencyInjection;

var config = Zonit.Libraries.Website.Configuration.CreateConfiguration(args);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

builder.AddServiceDefaults();      // Aspire
builder.Host.UseSerilog();
builder.Configuration.AddConfiguration(config);

builder.Services
    .Configure<WebsiteOptions>(builder.Configuration.GetSection("Website"))
    .Configure<Zonit.Extensions.Databases.DatabaseOptions>(builder.Configuration.GetSection("Database"));

builder.Services.AddSignalR();              // Blazor Server tuning if needed
builder.Services.AddImageSharp().AddImageSharpService(); // until side-module ships

builder.Services.AddDashboardService();
builder.Services.AddMailsExtension();
builder.Services.AddSettingsExtension();

builder.Services.AddWebsite(o =>
{
    o.Controllers = true;                   // SeoMatch needs MVC controllers
    o.AddArea<HomeArea>();
    o.AddArea<IdentityArea>();
    o.AddArea<ArticlesArea>();
    o.AddArea<AIArea>();
    o.AddArea<InvoicesArea>();
    o.AddArea<WalletsArea>();
    o.AddArea<CatalogsArea>();
    o.AddArea<OrganizationsArea>();
    o.AddArea<ProjectsArea>();
    o.AddArea<IntegrationsArea>();
    // …all existing AddXxxApplication/Infrastructure calls move INTO each area's
    //  ConfigureServices(IServiceCollection)
});

var app = builder.Build();
app.MapDefaultEndpoints();                  // Aspire

app.UseDashboardServices<IAreaManager>(new DashboardOptions { Directory = "Manager", … });
app.UseDashboardServices<IAreaManagement>(new DashboardOptions { Directory = "management", Permission = "AllowManagement", … });

app.UseWebsite<Areas.Web.App>("/", o =>
{
    o.Proxy = true;                         // Aspire / nginx ingress in prod
    o.App(b => b.UseImageSharp());          // until ImageSharpArea side-module ships
});

app.Run();
```

**Estimated reduction**: ~360 → ~50-60 lines, **~85 % less boilerplate**, with one
crucial caveat: each per-area `AddXxxApplication() / Infrastructure() / Manager()`
call must move out of `Program.cs` and into the area's `ConfigureServices` hook.
That's an Area-by-Area migration, not a `Program.cs` rewrite — and it's exactly
what the Site/Area architecture was designed to enable.

### What's still consumer-side after the migration

These remain in `Program.cs` because they're host-policy decisions, not framework
concerns:

- **Aspire integration** (`builder.AddServiceDefaults()` / `app.MapDefaultEndpoints()`)
- **Logging configuration** (Serilog, log filters per consumer's noise tolerance)
- **Configuration sources** (consumer chooses appsettings vs Vault vs env vars)
- **Hosted services** the host-app owns (e.g. `InitSetting` data seeder)
- **Custom domain endpoints** (`/facebook`, `/discord`, `/.well-known/apple-developer-…`)
  — these are SeoMatch-specific marketing redirects, not framework concerns. They
  live nicely in a `MarketingArea` with `MapEndpoints` though.

### What's still missing in `Zonit.Extensions.Website` to fully match SeoMatch

After this iteration the framework covers HSTS, Compression, Proxy, AntiForgery,
ExceptionHandler, HttpsRedirection, ProblemDetails, MemoryCache, Controllers,
RazorComponents, AuthN/AuthZ, Antiforgery, Razor Components.

Still NOT in the framework (intentionally — they're either side-modules or
domain-specific):

| Concern | Where it should live |
|---------|----------------------|
| ImageSharp pipeline | side-module `Zonit.Extensions.Website.ImageSharp` |
| Sitemap generation | side-module `Zonit.Extensions.Website.Sitemap` |
| Mail / SMTP | already separate (`Zonit.Extensions.Mails`) |
| Settings KV store | already separate (`Zonit.Extensions.Settings`) |
| SignalR tuning | call `AddSignalR(o => …)` directly — too app-specific to default |
| `.well-known` endpoints | per-app `MapEndpoints` site hook or dedicated area |
| Logging filters | consumer policy, not framework opinion |

These boundaries are clean: framework owns the **pipeline shape**, side-modules own
**reusable feature areas**, and the consumer owns **policy and integrations**.

## Open questions for next session

- Do we want `Zonit.Libraries.Website` to absorb the side-modules, or do we keep
  a strict `Extensions.*` (protocol) vs `Libraries.*` (utilities) split?
- Where does Dashboard sit in the side-module model? Probably stays a Service
  (it's a full app, not a feature module).
- Is `Zonit.Services.Dashboard` going to depend on the side-modules (so the dashboard
  has ImageSharp out of the box), or do consumers wire dashboard + side-modules
  separately?
