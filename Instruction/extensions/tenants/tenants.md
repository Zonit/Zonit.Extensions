# Tenants — per-domain settings (`Zonit.Extensions.Tenants`)

A tenant is a *site identity* resolved from the request host: an id, a domain, and a bag of
JSON-encoded setting overrides. The package resolves one tenant per DI scope and exposes its
settings as strongly-typed models. It is framework-agnostic — it depends only on
`Microsoft.Extensions.DependencyInjection.Abstractions` and `Zonit.Extensions`, with no ASP.NET Core
reference. The HTTP glue (`TenantMiddleware`) lives in `Zonit.Extensions.Website` and is `internal`;
see `.zonit/extensions/website/hosting.md`.

Everything below is verified against **10.0.0-preview.10**.

## Read this before you write any code

| Trap | Reality |
|---|---|
| Calling `AddTenantsExtension()` after `AddWebsite()` | `AddWebsite()` already calls it. Harmless (both registrations are `TryAdd`) but pointless. |
| `GetRequiredService<ITenantSource>()` | Throws in a solo host. The package registers **no** `ITenantSource`; `NullTenantSource` was deleted in preview.10. Use `GetService<ITenantSource>()` and handle `null`. |
| Expecting the framework to match `Tenant.Domain` | It never reads it. You get the raw `HttpRequest.Host.Host` and own case, aliases, `www.`, punycode — and all caching. |
| Writing overrides with `JsonSerializer.Serialize(model)` | Default options are PascalCase; hydration is camelCase and **case-sensitive**. The blob matches nothing, you silently get compile-time defaults, and no exception and no event is raised. |
| `Settings.Site` vs `GetSetting<SiteSetting>()` | The first returns the **model** (`SiteSettingsModel`), the second returns the **setting** — add `.Value`. |
| Your own setting on the façade | It is an extension **method**, not a property: `Settings.Pricing()`, with parentheses. |
| `Settings.Site.Title = "x"` | Compiles, and changes what every later reader in that scope sees. Nothing is persisted — this package has no write path. |
| Two settings whose names collapse to one accessor | The consumer build fails with `CS0111` inside generated code. See *Known limitations*. |

## Setup

`AddTenantsExtension()` lives in namespace `Zonit.Extensions` (not `…Tenants`) and `TryAdd`s two
scoped services: `ITenantRepository` → `TenantRepository` and `ITenantProvider` → `TenantService`.
Both implementations are `internal`.

```csharp
using Zonit.Extensions;            // AddTenantsExtension()
using Zonit.Extensions.Tenants;    // ITenantProvider, ITenantSource, Tenant

// Multi-site web host — AddWebsite() already calls AddTenantsExtension(),
// so the only line you add is your data adapter.
builder.Services.AddScoped<ITenantSource, SqlTenantSource>();

// Non-web host (console / worker / test): wire it yourself.
services.AddScoped<ITenantSource, SqlTenantSource>();
services.AddTenantsExtension();
```

Registration order does not matter for `ITenantSource` — nothing `TryAdd`s it. It *does* matter if
you want to substitute the provider itself: `AddTenantsExtension()` uses `TryAdd`, so register your
own `ITenantProvider` / `ITenantRepository` **before** it, or use `services.Replace(...)`.

A solo site registers no source at all. `TenantMiddleware` detects that (`GetService<ITenantSource>()`
returns `null`), seeds `Tenant.Solo` directly and raises the change notification exactly once — no
async round trip. Registering a no-op source instead re-introduces the double-notification bug that
preview.9 shipped.

### Without middleware

No middleware means no resolution. In a console host, a worker, or a test you drive the repository:

```csharp
using var scope = provider.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

await repository.InitializeAsync("acme.example.com");   // goes through ITenantSource
// or, when you already hold the instance:
repository.Initialize(Tenant.Solo);                     // synchronous, always raises OnChange

var tenants = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
Console.WriteLine(tenants.Settings.Site.Title);
```

`InitializeAsync` is idempotent per scope: a second call with the same domain (compared
`OrdinalIgnoreCase`) short-circuits without touching your source. It returns early without notifying
when no `ITenantSource` is registered, and raises `OnChange` only when the resolved instance actually
differs from the current one.

## Implementing `ITenantSource`

One method. Everything interesting is yours.

```csharp
public interface ITenantSource
{
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
}
```

| The framework does | You do |
|---|---|
| Hands you `HttpRequest.Host.Host` verbatim, once per non-static request scope | Case folding, `www.` stripping, alias tables, wildcard / punycode handling |
| Caches the result **for the current scope only** | All cross-request caching (`IMemoryCache`, `IDistributedCache`, a preloaded frozen map) |
| Substitutes `Tenant.Solo` when you return `null` | Deciding what "unknown host" means for your product |

```csharp
using System.Collections.Frozen;

internal sealed class SqlTenantSource : ITenantSource
{
    private static readonly FrozenDictionary<string, Tenant> Catalog =
        new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase)
        {
            ["acme.example.com"] = new Tenant
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Domain = "acme.example.com",
                Variables = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["site"]  = """{"title":"Acme","metaDescription":"Acme corporate site","language":"en-US"}""",
                    ["theme"] = """{"primaryColor":"#0F766E","fontFamily":1,"roundness":2}""",
                }.ToFrozenDictionary(StringComparer.Ordinal),
            },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        var host = domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? domain[4..] : domain;
        return Task.FromResult(Catalog.TryGetValue(host, out var tenant) ? tenant : null);
    }
}
```

Recommended lifetime `Scoped`.

### `Tenant` and `Tenant.Solo`

`Tenant` is a **sealed class**, not a record — no value equality, no `with` expression.

```csharp
public sealed class Tenant
{
    public required Guid Id { get; init; }
    public required string Domain { get; init; }
    public FrozenDictionary<string, string> Variables { get; init; } = FrozenDictionary<string, string>.Empty;

    public static readonly Tenant Solo;   // Id = Guid.Empty, Domain = "*"
    public bool IsSolo { get; }           // Id == Guid.Empty && Domain == "*"
}
```

`Tenant.Solo` is the sentinel the middleware substitutes in two cases: no `ITenantSource` registered
(single-site app), or a registered source returned `null` (unknown host). The point is that
`ITenantProvider.Current` is never `null` in normal request flow, so pages never null-check.
`Current?.IsSolo == true` is how you tell "defaults" from "a real tenant".

## The storage contract

`Tenant.Variables` maps **`ISetting.Key` → JSON of the model**. Keys for the built-ins are `site`,
`theme`, `maintenance`, `social_media`. Unknown keys are ignored; a missing key means the setting
resolves to its compile-time defaults.

The shape is defined by whatever `Setting<T>.Hydrate(string)` deserialises with. For the built-ins
that is `TenantsJsonContext`, which is **public since preview.10** precisely so the write side can
match it:

```csharp
using Zonit.Extensions.Tenants.Settings;

// The supported way to produce a value for Variables["site"].
var json = JsonSerializer.Serialize(
    new SiteSettingsModel { Title = "Acme", Language = "en-US" },
    TenantsJsonContext.Default.SiteSettingsModel);
// {"title":"Acme","metaDescription":"This is a new website created","language":"en-US"}
```

Its options are `PropertyNamingPolicy = CamelCase`, `WriteIndented = false`,
`DefaultIgnoreCondition = WhenWritingNull`. Consequences worth internalising:

- **camelCase, case-sensitive.** `{"Title":"Pascal"}` hydrates to `Title = "New website"`. Verified:
  no exception, `OnSettingHydrationFailed` does not fire. This is the most common way to lose tenant
  data.
- **Enums serialise as numbers** — there is no `JsonStringEnumConverter`. A default
  `ThemeSettingsModel` round-trips as `…,"fontFamily":0,"fontScale":1,"roundness":2,"shadow":1}`.
- **Nulls are omitted on write**, and absent properties keep the model default on read, so a partial
  blob is legal: `{"primaryColor":"#0F766E"}` overrides one colour and leaves the rest alone.

### Built-in models and defaults

| Key | Setting | Model property | Default |
|---|---|---|---|
| `site` | `SiteSetting` | `Title` (required, 3–30) | `"New website"` |
| | | `MetaDescription` (required, 10–160) | `"This is a new website created"` |
| | | `Language` (required, 2–10, BCP 47) | `"pl-PL"` |
| | | `LogoUrl`, `FaviconUrl` (`string?`, ≤200) | `null` |
| `theme` | `ThemeSetting` | `PrimaryColor` / `SecondaryColor` / `AccentColor` | `#2563EB` / `#7C3AED` / `#DC2626` |
| | | `NeutralColor` / `SurfaceColor` / `ContentColor` | `#F1F5F9` / `#FFFFFF` / `#0F172A` |
| | | `FontFamily` (`Inter, Roboto, OpenSans, Poppins, Montserrat, Nunito, PlusJakartaSans`) | `Inter` (0) |
| | | `FontScale` (`Small, Normal, Large`) | `Normal` (1) |
| | | `Roundness` (`None, Small, Medium, Large`) | `Medium` (2) |
| | | `Shadow` (`None, Small, Medium, Large`) | `Small` (1) |
| `maintenance` | `MaintenanceSetting` | `IsActive` | `false` |
| | | `MaintenanceMessage` (`string?`, 10–2000) | `null` |
| `social_media` | `SocialMediaSetting` | `Facebook, X, Instagram, LinkedIn, YouTube, TikTok, Pinterest, Snapchat, Reddit, Twitch, Threads, Discord` — all `string?`, `[Url]`, ≤200 | `null` |

`MaintenanceSetting` is data only. Nothing in this package or in `Zonit.Extensions.Website`
short-circuits a request when `IsActive` is true — that is your middleware or your layout.

The six theme colours carry `[ColorPicker]` from namespace `Zonit.Extensions.Tenants` (**not**
`.Settings`). It validates `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA`, passes `null`, and tells the
Blazor renderer to swap `InputText` for a colour-picker control.

## Reading settings

```razor
@using Zonit.Extensions.Tenants
@using Acme.Pricing                        @* brings the generated accessor into scope *@
@inject ITenantProvider Tenants

<h1>@Tenants.Settings.Site.Title</h1>
<style>:root { --primary: @Tenants.Settings.Theme.PrimaryColor; }</style>

@if (Tenants.Settings.Maintenance.IsActive)
{
    <p>@Tenants.Settings.Maintenance.MaintenanceMessage</p>
}

@* consumer-declared setting — a METHOD *@
<span>@Tenants.Settings.Pricing().Plan</span>
```

Inside a `PageBase` / `ExtensionsBase` component the provider is already available as
`protected ITenantProvider Tenant`, and the base class subscribes to `OnChange` for you — do not
subscribe again and do not inject a second copy. See `.zonit/extensions/website/pages.md`.

Two equivalent paths with different return types:

```csharp
SiteSettingsModel a = provider.Settings.Site;                   // model
SiteSettingsModel b = provider.GetSetting<SiteSetting>().Value; // setting → .Value
// ReferenceEquals(a, b) is true within a scope
```

`GetSetting<TSetting>()` is constrained `where TSetting : ISetting, new()` — the type needs a public
parameterless constructor. Results are cached per scope keyed by `ISetting.Key`, and the cache is
cleared whenever the tenant changes (`ITenantProvider.OnChange`).

Two sharp edges on that shared instance:

```csharp
provider.Settings.Site.Title = "mutated";
provider.Settings.Site.Title;                       // "mutated" — scope-wide, persists nothing

provider.GetSetting<SiteSetting>().Value = new SiteSettingsModel { Title = "replaced" };
provider.Settings.Site.Title;                       // still the OLD model: the built-in façade
                                                    // caches the model reference per instance
```

Treat hydrated models as read-only.

## Authoring a `Setting<TModel>`

```csharp
namespace Acme.Pricing;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zonit.Extensions.Tenants.Settings;

public sealed class PricingModel
{
    [Display(Name = "Plan", Description = "Billing plan slug.")]
    [Required, StringLength(20)]
    public string Plan { get; set; } = "free";

    [Display(Name = "Seat limit")]
    [Range(1, 1000)]
    public int SeatLimit { get; set; } = 5;

    [Zonit.Extensions.Tenants.ColorPicker]
    public string BadgeColor { get; set; } = "#2563EB";
}

public sealed class PricingSetting : Setting<PricingModel>
{
    public override string Key         => "acme_pricing";   // storage key, lower_snake_case
    public override string Name        => "Pricing";        // admin UI label
    public override string Description => "Billing plan shown on the pricing page.";

    // Optional presets an admin UI can offer. null — the base default — means "no presets".
    public override IReadOnlyCollection<PricingModel>? Templates { get; } =
    [
        new PricingModel { Plan = "free", SeatLimit = 5 },
        new PricingModel { Plan = "pro",  SeatLimit = 25 },
    ];

    // MUST be AOT-safe: a source-generated JsonTypeInfo, never JsonSerializer.Deserialize<T>(string).
    // MUST NOT throw on bad data — the contract is "fall back to new()".
    public override PricingModel Hydrate(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, PricingJsonContext.Default.PricingModel) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}

// Public, so whoever writes Variables["acme_pricing"] can produce the exact shape Hydrate reads.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PricingModel))]
public sealed partial class PricingJsonContext : JsonSerializerContext;
```

| Requirement | Why |
|---|---|
| `TModel : class, new()` | Defaults are materialised with `new()` when there is no override. |
| Public parameterless ctor on the setting | `GetSetting<T>()` carries a `new()` constraint. |
| Setting type must be `public` | The generator skips non-public types — an accessor for them would be `CS0122`. |
| `Hydrate` uses a `JsonSerializerContext` | The reflection overloads trip IL2026 / IL3050 under `PublishAot`. The core package carries zero suppressions; keeping the whole graph clean is the plugin author's job. |
| `Hydrate` returns rather than throws | The framework catches only `JsonException`; anything else propagates and takes the page down. |

### What the generator emits

`Zonit.Extensions.Tenants.SourceGenerators` ships inside the nupkg at `analyzers/dotnet/cs` and runs
on **your** compilation. It scans the current compilation's syntax trees only — settings arriving
through a referenced assembly already carry that assembly's own façade. For the example above it
writes `TenantSettingsExtensions.Acme.Pricing.g.cs`:

```csharp
namespace Acme.Pricing;

public static class TenantSettingsExtensions
{
    public static global::Acme.Pricing.PricingModel Pricing(this global::Zonit.Extensions.Tenants.TenantSettings settings)
        => settings.Get<global::Acme.Pricing.PricingSetting>().Value;
}
```

so the call site is `Tenants.Settings.Pricing()` once `Acme.Pricing` is imported. Notes:

- **Method, not property.** A partial class cannot span assemblies, so your setting can never become
  a `TenantSettings` member. preview.9 emitted `partial class TenantSettings` into consumer
  assemblies and broke every build that declared a `Setting<T>` (`CS0103` on `Provider`, plus
  `CS0436` at every use site); that shape is now gated to the single compilation that declares the
  hand-written half. Extension *methods* rather than C# 14 extension properties, so the emitted code
  compiles under any pinned `LangVersion`.
- **One static class per namespace**, always named `TenantSettingsExtensions`. Settings in different
  namespaces never collide.
- **Accessor name** = type name with a trailing `Setting` stripped: `PricingSetting` → `Pricing`,
  `ThemeSetting` → `Theme`. A type that does not end in `Setting`, or one named exactly `Setting`,
  keeps its full name.
- Inside `Zonit.Extensions.Tenants` itself the generator emits the partial half instead, which is why
  the four built-ins are properties (`Settings.Site`, `Settings.Theme`, `Settings.Maintenance`,
  `Settings.SocialMedia`) while yours is a method.
- The generator also reaches assemblies that get the package **transitively** — verified in a project
  whose only `PackageReference` is `Zonit.Extensions.Website`. Analyzers do not flow across
  `ProjectReference`, so in a source-built solution add the generator project yourself with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.
- `settings.Get<TSetting>()` is public API and is the seam the generated code uses — call it directly
  if you ever need to bypass the accessor.

## Corrupt override blobs

When a blob exists but is malformed the framework catches `JsonException`, keeps the compile-time
defaults, and reports it. Nothing is logged: this package has no `ILogger` dependency.

```csharp
public sealed record SettingHydrationFailure(Guid TenantId, string SettingKey, Exception Exception);

// scoped — subscribe from a scoped service, a middleware, or a circuit-lifetime component
provider.OnSettingHydrationFailed += failure =>
    logger.LogError(failure.Exception, "Tenant {TenantId} setting '{Key}' failed to hydrate",
                    failure.TenantId, failure.SettingKey);
```

Subscribing is the only way to tell "corrupt override" from "no override". A *well-formed but
wrong-cased* blob is neither — it is silent data loss.

## Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `ITenantRepository` | Scoped | Per-scope snapshot plus `Initialize` / `InitializeAsync`. No cross-request cache. |
| `ITenantProvider` | Scoped | Read API. Caches hydrated settings per scope; cleared on tenant change. |
| `ITenantSource` | yours, `Scoped` recommended | Not registered by the package. Put your durable cache here. |

A hand-written `ITenantProvider` double must declare both events (`OnChange` and
`OnSettingHydrationFailed`; auto-events you never raise are valid) and can build the façade from
itself — `TenantSettings` has a public constructor: `Settings => _settings ??= new TenantSettings(this)`.

## Known limitations

- **A Blazor interactive circuit sees no tenant.** `TenantMiddleware` runs against HTTP request
  scopes; a circuit is a different scope the middleware never touches, so `Current` is `null` there
  and every setting renders its compile-time default after a prerender that showed the real values.
  `Models/TenantSnapshot.cs` (`From` / `ToTenant` — a serialisable mirror of `Tenant`, needed because
  `FrozenDictionary` cannot be deserialised) is the payload for the bridge that would fix this, but
  **no bridge exists**: nothing in the repository constructs or consumes `TenantSnapshot`, and the
  `IPersistentStateProvider` implementations in `Zonit.Extensions.Website` cover Auth, Culture,
  Workspace, Catalog and Cookie only. Treat `TenantSnapshot` as a type you may use to move a tenant
  across scopes yourself (`repository.Initialize(snapshot.ToTenant())`), not as a working feature.
  Related: the bridges that do exist are silently disabled under any `PublishTrimmed` publish — see
  `.zonit/extensions/website/hydration.md`.
- **Colliding accessor names break the consumer build.** Two settings in one namespace whose names
  collapse to the same accessor — `FooSetting` and `Foo`, or `SeoSetting` and `Seo` — emit two
  extension methods with identical signatures. Reproduced: `error CS0111 … 'TenantSettingsExtensions'
  already defines a member named 'Seo'`, pointing into `TenantSettingsExtensions.<ns>.g.cs`, code the
  consumer never wrote. There is no uniqueness pass and no diagnostic. Workaround: rename one type or
  move it to another namespace.
- **`TenantSnapshot.Variables` XML doc references `Setting<T>.Dehydrate`.** That method does not
  exist — there is no serialisation half on `Setting<T>`. Write blobs with your own
  `JsonSerializerContext`, as shown above.
- **No write path.** This package reads tenants. Persisting an edited setting is entirely the host's
  job, and the shape you must write is defined by `Hydrate`.

## Upgrading from 10.0.0-preview.9

| Change | What to do |
|---|---|
| The generator emits an extension-method façade instead of a broken `partial class TenantSettings` | Nothing to migrate — preview.9 could not compile. Rebuild, then call `Settings.MyThing()` with parentheses. `GetSetting<T>().Value` is unchanged. |
| Non-public `Setting<T>` types are skipped | Make the type `public` if you want an accessor. |
| `NullTenantSource` deleted; `ITenantSource` no longer auto-registered | Switch any `GetRequiredService<ITenantSource>()` to `GetService<ITenantSource>()`. Hosts that register their own source are unaffected. |
| `ITenantProvider` gained `event Action<SettingHydrationFailure>? OnSettingHydrationFailed` | Only affects *implementers* (test doubles, decorators): add the auto-event. Consumers of the interface need no change. |
| `TenantsJsonContext` is now `public` | Use it to write built-in blobs instead of reverse-engineering camelCase by hand. |
| Solo / unknown-host requests raise `OnChange` once instead of twice | Change handlers now run half as often; make them idempotent. |
| Hydration no longer swallows non-`JsonException` failures | A plugin `Hydrate` that used to throw silently now takes the page down. Fix it to return `new()`. |
