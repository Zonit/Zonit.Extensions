# Tenants — per-domain settings (`Zonit.Extensions.Tenants`)

A tenant is a *site identity* resolved from the request host: an id, a domain, and a bag of
JSON-encoded setting overrides. The package resolves one tenant per DI scope and exposes its
settings as strongly-typed models. It is framework-agnostic — it depends only on the
`Microsoft.Extensions.*` abstractions (DI, Logging, Configuration) and `Zonit.Extensions`, with no
ASP.NET Core reference. The HTTP glue (`TenantMiddleware`) lives in `Zonit.Extensions.Website` and
is `internal`; see `.zonit/extensions/website/hosting.md`.

Everything below is verified against **10.0.0-preview.11**.

## Read this before you write any code

| Trap | Reality |
|---|---|
| Calling `AddTenantsExtension()` after `AddWebsite()` | `AddWebsite()` already calls it. Call it again only to change `ConfigurationSection` / `ReloadOnChange`; JSON metadata registers itself. |
| `GetRequiredService<ITenantSource>()` | Throws in a single-site host. The package registers **no** `ITenantSource`. Use `GetService<ITenantSource>()` and handle `null`. |
| Expecting the framework to match `Tenant.Domain` | It never matches on it. You get the raw `HttpRequest.Host.Host` and own case, aliases, `www.`, punycode — and all caching. |
| `if (Tenants.Current is null)` | Correct — `Current` **is** nullable. But settings never go through it, so you rarely need the check. Read `Resolution` for *why* it is null. |
| Treating a null `Current` as "unknown host" | It is also null for a single-site app and for an uninitialised scope. `Resolution == TenantResolution.Unknown` is the misconfiguration signal. |
| Writing a `Hydrate` override and a `JsonSerializerContext` per setting | No longer needed for a flat model — the generator emits its JSON metadata. Supply a context only for nested/collection models (ZONITTS0003 tells you) or for different JSON rules. |
| Assuming blob casing matters | It does not. Reading is case-insensitive; `{"Title":…}` and `{"title":…}` both bind. |
| `Settings.Site` vs `GetSetting<SiteSetting>()` | The first returns the **model** (`SiteSettingsModel`), the second returns the **setting** — add `.Value`. |
| Your own setting on the façade | An extension **property** on C# 14+: `Settings.Pricing`, no parentheses. Below C# 14 the generator falls back to `Settings.Pricing()`. |
| `Settings.Site.Title = "x"` | Compiles, and changes what every later reader in that scope sees. Nothing is persisted. |

## Setup

`AddTenantsExtension()` lives in namespace `Zonit.Extensions` (not `…Tenants`). It `TryAdd`s two
scoped services (`ITenantRepository` → `TenantRepository`, `ITenantProvider` → `TenantService`) and
two singletons (`ITenantSettingsSerializer`, the internal configuration source). All
implementations are `internal`.

```csharp
using Zonit.Extensions;            // AddTenantsExtension()
using Zonit.Extensions.Tenants;    // ITenantProvider, ITenantSource, Tenant

// Multi-site web host — AddWebsite() already calls AddTenantsExtension(),
// so the only line you add is your data adapter.
builder.Services.AddScoped<ITenantSource, SqlTenantSource>();

// JSON metadata for setting models needs NO registration — see "AOT and trimming".
builder.Services.AddTenantsExtension();
```

Registration order does not matter for `ITenantSource` — nothing `TryAdd`s it. It *does* matter if
you want to substitute the provider itself: registrations are `TryAdd`, so register your own
`ITenantProvider` / `ITenantRepository` **before**, or use `services.Replace(...)`.

A single-site app registers no source at all; `Current` stays `null` and `Resolution` is `SingleSite`.

### Without middleware

No middleware means no resolution. In a console host, a worker, or a test you drive the repository:

```csharp
using var scope = provider.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

await repository.InitializeAsync("acme.example.com");   // goes through ITenantSource
repository.Initialize(someTenant);                      // synchronous, e.g. from persisted state
repository.Initialize(null);                            // reset — Current back to null

var tenants = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
Console.WriteLine(tenants.Settings.Site.Title);
```

`InitializeAsync` is idempotent per scope: a second call with the same domain (compared
`OrdinalIgnoreCase`) short-circuits without touching your source — including when the first call
found nothing. It returns early when no `ITenantSource` is registered, and raises `OnChange` only
when the effective tenant actually changes. `Initialize` has the same no-op guard.

## `Tenant`, `Tenant.Default` and `Resolution`

`Tenant` is a **sealed class**, not a record — no value equality, no `with` expression.

```csharp
public sealed class Tenant
{
    public required Guid? Id { get; init; }
    public required string Domain { get; init; }
    public FrozenDictionary<string, string> Variables { get; init; } = FrozenDictionary<string, string>.Empty;

    public static readonly Tenant Default;              // Id = null, Domain = "*"
    public bool IsDefault { get; }                      // Id is null && Domain == "*"
}
```

`Id` is **required and nullable**, which answers two different questions. `required` means you have
to state what the id is — forgetting it is a compile error, not a record that quietly carries a
meaningless value. Nullable means "this tenant has no identity" is sayable at all, which a
single-site host needs. `Guid.Empty` is never produced by the framework and should not be used as
a stand-in: it is a real `Guid`, so nothing would distinguish "no id" from "somebody forgot the
id", and the second reads as the first at every use site — including a database lookup that then
matches nothing.

`Tenant.Default` is **not** manufactured by the framework any more: an unresolved scope has a
`null` `Current`. It stays available for a host that deliberately wants a non-null tenant in a
single-site app (`repository.Initialize(Tenant.Default)`).

**In a multi-domain app, do not reach for it as a fallback — return a catch-all from your
`ITenantSource` instead:**

```csharp
public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
    => await LookupAsync(domain, ct) ?? CatchAll;

private static readonly Tenant CatchAll = new()
{
    Id = null, Domain = "*", Variables = /* the marketing site's settings */,
};
```

The host is then `Resolved`, `Current` is never null, and the catch-all carries its own settings.
Verified behaviour with three tenant domains:

| Source | Host | `Resolution` | `Current` | `Settings.Site.Title` |
|---|---|---|---|---|
| plain | `acme.com` | `Resolved` | the tenant | `"Acme"` |
| plain | `typo.com` | `Unknown` | `null` | from configuration (and the middleware 404s first) |
| catch-all | `acme.com` | `Resolved` | the tenant | `"Acme"` |
| catch-all | `typo.com` | `Resolved` | the catch-all | the catch-all's own title |

The framework deliberately does not substitute a default itself: doing so is exactly how a typo'd
DNS record ends up serving a real-looking page, and it would collapse `Unknown` into `SingleSite`
so that nothing could 404 or log.

`Current` is **nullable** on both `ITenantRepository` and `ITenantProvider`, and starts `null`. It
carries identity — `Id`, `Domain`, `Variables` — and none of those has a meaningful value before a
tenant resolves; a sentinel would make `@Tenant.Current.Domain` render `"*"`.

**Reading settings does not go through it.** `Settings` and `GetSetting<T>()` always resolve,
falling back to configuration and then to compile-time defaults, so a null tenant costs no page a
null check. `Resolution` says *why* it is null:

| `TenantResolution` | Meaning |
|---|---|
| `None` | Nothing resolved in this scope — console, worker, test, or a circuit before the state bridge |
| `SingleSite` | No `ITenantSource` registered. A single-site app; defaults are the answer |
| `Resolved` | A source returned a tenant for this host — the only case where `Current` is not null |
| `Unknown` | A source was asked and did not recognise the host |

**`Unknown` means a hostname is pointed at the app with no tenant behind it** — a DNS record without
the matching row, a typo'd alias, a staging host leaking into production.

In a web host `TenantMiddleware` **answers `404` by default** and does not run the rest of the
pipeline, because serving compile-time default branding on an unknown domain is indistinguishable
from a working site. Opt out when unknown hosts are legitimate:

```csharp
builder.Services.AddWebsite<MyApp>(o => o.UnknownHost = UnknownHostBehavior.Continue);
```

Either way it is logged at `Warning` (category
`Zonit.Extensions.Tenants.Repositories.TenantRepository`, event id 2) with the offending host.
Outside a web host, or with `Continue`, branch on it yourself:

```csharp
if (Tenants.Resolution is TenantResolution.Unknown)
    return Results.NotFound();
```

`Tenant.Solo` / `IsSolo` still exist as `[Obsolete]` aliases for `Default` / `IsDefault` and will be
removed in the next preview.

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
| Leaves `Current` null and sets `Resolution = Unknown` when you return `null` | Deciding what "unknown host" means for your product (the middleware 404s by default) |

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

## Where a setting's value comes from

Three layers, resolved per `ISetting.Key`:

1. **`Tenant.Variables[key]`** — this tenant's persisted JSON blob.
2. **`Tenants:{key}` in `IConfiguration`** — the house default shared by every tenant.
3. **The model's compile-time defaults.**

### 1. The storage contract

`Tenant.Variables` maps **`ISetting.Key` → JSON of the model**. Built-in keys are `site`, `theme`,
`maintenance`, `social_media`. Unknown keys are ignored.

Blobs are written **camelCase**, nulls omitted, enums as **numbers**. Do not hand-roll it — inject
`ITenantSettingsSerializer`, which exposes the exact options the reader uses:

```csharp
public sealed class TenantWriter(ITenantSettingsSerializer serializer)
{
    public string ToBlob(SiteSettingsModel model) => serializer.Serialize(model);
    // {"title":"Acme","metaDescription":"This is a new website created","language":"en-US"}
}
```

- **Reading ignores casing.** `{"Title":"Pascal"}` binds fine. This used to be the most common way
  to lose tenant data: matching was case-sensitive, the blob matched nothing, and the setting fell
  back to defaults with no exception and no event.
- **Enums are numbers.** Renumbering an enum member reinterprets every stored blob.
- **Nulls are omitted on write**, and absent properties keep the model default on read, so a partial
  blob is legal: `{"primaryColor":"#0F766E"}` overrides one colour and leaves the rest alone.

### 2. Configuration

```json
{
  "Tenants": {
    "site":  { "Title": "Acme", "Language": "en-US" },
    "theme": { "PrimaryColor": "#0F766E", "Roundness": "Large", "FontScale": 2 },
    "acme_pricing": { "Plan": "pro", "SeatLimit": 25 }
  }
}
```

Ordinary `IConfiguration`, so every provider applies — `appsettings.{Environment}.json`, user
secrets, environment variables (`Tenants__site__Title`), Key Vault, command line. Property casing is
free-form and enums accept either the member name (`"Large"`) or its number (`2`).

A tenant that stores its own blob overrides configuration for that key only. Editing the file
re-renders open Blazor circuits: a reload invalidates hydrated settings and raises
`ITenantProvider.OnChange`, which components already subscribe to. Change the section name with
`o.ConfigurationSection` and disable reload with `o.ReloadOnChange = false`.

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

@* consumer-declared setting — a PROPERTY, same as a built-in *@
<span>@Tenants.Settings.Pricing.Plan</span>
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

`GetSetting<TSetting>()` is constrained `where TSetting : ISetting, new()`. Results are cached per
scope keyed by `ISetting.Key`; the cache is cleared when the tenant changes or configuration
reloads (`ITenantProvider.OnChange`).

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
}
```

That is the whole setting. **No `Hydrate` override and no `JsonSerializerContext`** — those were
required through preview.10 and are not any more.

| Requirement | Why |
|---|---|
| `TModel : class, new()` | Defaults are materialised with `new()` when there is no override. |
| Every model property needs a sensible default | "No override" is answered with `new()`, never with `null`. |
| Public parameterless ctor on the setting | `GetSetting<T>()` carries a `new()` constraint. |
| Setting type must be `public` | The generator skips non-public types — an accessor for them would be `CS0122`. |

Override `Hydrate(string json, JsonSerializerOptions options)` **only** when a setting needs
different JSON rules than the shared camelCase / `WhenWritingNull` contract — a custom converter,
string enums. Reach for the concrete `JsonTypeInfo` there, because the shared options deliberately
do not inherit a context's own `[JsonSourceGenerationOptions]`. Do not catch `JsonException` inside
it: the framework catches it, keeps the defaults, logs, and raises `OnSettingHydrationFailed`;
swallowing it makes a corrupt blob indistinguishable from "no override".

## AOT and trimming

The package is `IsTrimmable` / `IsAotCompatible`, and a Native AOT publish of a consuming app
compiles through ILC with zero IL2xxx/IL3xxx warnings.

Hydration takes one of two paths per model:

| Your app | Path | What you write |
|---|---|---|
| JIT, untrimmed (the default) | Reflection | Nothing. The model just works. |
| `PublishAot` / `PublishTrimmed` | Source-generated `JsonTypeInfo` | One `[JsonSerializable]` line + one `AddJsonContext` call |

**There is nothing to write.** Declaring a `Setting<TModel>` is the whole AOT story — no
`JsonSerializerContext`, no `[JsonSerializable]`, no `Hydrate`, no registration call.

The generator describes each flat setting model through `JsonMetadataServices` (the same public API
System.Text.Json's own generator emits calls to) into an internal
`Zonit.Extensions.Tenants.Generated.TenantSettingsJsonMetadata` resolver, and emits a
`[ModuleInitializer]` that registers it with `TenantSettingsMetadata` as the assembly loads. Scalar
property types are covered once by the runtime package; enums are emitted per assembly, because
`GetEnumConverter<TEnum>` needs the closed generic.

The registry is consulted **at lookup time**, not snapshotted when the options are built. That
matters: assemblies load lazily, so an app whose layout reads a built-in setting before anything
touches a plugin builds its options first — verified, a snapshot taken there misses the plugin
permanently and silently downgrades it to reflection.

`AddTenantsExtension(o => o.AddJsonContext(...))` still exists and is consulted **before** anything
the generator registered, which is how you pin precedence if two assemblies describe one model.

**Covered:** properties whose type is a scalar (`string`, numerics, `bool`, `char`, `Guid`,
`DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Uri`, `Version`), an enum, or a
nullable of either — with public getters and setters.

**Not covered:** nested objects, collections, dictionaries, get-only or init-only properties. Such
a model reports **ZONITTS0003** and is skipped *whole* — describing it partially would let a
missing property bind to its default and look like data loss. Supply a context for it:

```csharp
[JsonSerializable(typeof(NestedPricingModel))]
public sealed partial class AppJsonContext : JsonSerializerContext;
```

That context is registered automatically too, **before** the generated metadata,
so a hand-written context always wins for the models it covers. That is also the escape hatch when
a model needs different JSON rules; pair it with a `Hydrate` override if the rules differ from the
shared camelCase / `WhenWritingNull` contract.

Under AOT with neither, the setting throws `InvalidOperationException` naming the model and the fix
— it does not silently hand back an empty object.

Note the one thing that genuinely cannot be generated: `[JsonSerializable]` on a
`JsonSerializerContext`. Roslyn generators do not observe each other's output, so an attribute
emitted by one is invisible to the System.Text.Json generator and the context fails to compile with
`CS0534`. That is why the metadata is emitted directly rather than by delegating to that generator.

For the same reason configuration binding does not use `IConfiguration.Bind`: the configuration
binding source generator intercepts call sites where the bound type is statically known, and this
library's call site sits behind an open generic. Values are converted to JSON via `Utf8JsonWriter`
driven by `JsonTypeInfo.Properties`, which is source-generated metadata and needs no reflection.

## What the generator emits

`Zonit.Extensions.Tenants.SourceGenerators` ships inside the nupkg at `analyzers/dotnet/cs` and runs
on **your** compilation. It scans the current compilation's syntax trees only — settings arriving
through a referenced assembly already carry that assembly's own façade. For the example above it
writes `TenantSettingsExtensions.Acme.Pricing.g.cs`:

```csharp
namespace Acme.Pricing;

public static class TenantSettingsExtensions
{
    extension(global::Zonit.Extensions.Tenants.TenantSettings settings)
    {
        public global::Acme.Pricing.PricingModel Pricing
            => settings.Get<global::Acme.Pricing.PricingSetting>().Value;
    }
}
```

so the call site is `Tenants.Settings.Pricing` once `Acme.Pricing` is imported. Notes:

- **A C# 14 `extension` block, so the accessor is a property** — no parentheses, and a plugin
  setting reads exactly like a built-in one. Below C# 14 the generator falls back to the older
  extension-**method** shape (`Settings.Pricing()`), because extension properties do not exist
  there. A partial class still cannot span assemblies, so your setting is never a real
  `TenantSettings` member; the four built-ins are.
- **One static class per namespace**, always named `TenantSettingsExtensions`. Settings in different
  namespaces never collide.
- **Accessor name** = type name with a trailing `Setting` stripped: `PricingSetting` → `Pricing`.
  A type that does not end in `Setting`, or one named exactly `Setting`, keeps its full name.
- **Colliding accessors are diagnosed, not broken.** Two settings that reduce to the same name
  (`FooSetting` and `Foo`) report `ZONITTS0001` and only one gets an accessor; a name
  `TenantSettings` already defines reports `ZONITTS0002` and is skipped. Read the loser with
  `settings.Get<TSetting>()`, which is public API.
- The generator also reaches assemblies that get the package **transitively**. Analyzers do not flow
  across `ProjectReference`, so in a source-built solution add the generator project yourself with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.

## Corrupt override blobs

When a blob exists but is malformed the framework catches `JsonException`, keeps the compile-time
defaults, logs at `Warning` (category `Zonit.Extensions.Tenants.Services.TenantService`, event id 1)
and raises an event:

```csharp
public sealed record SettingHydrationFailure(Guid TenantId, string SettingKey, Exception Exception);

// scoped — subscribe from a scoped service, a middleware, or a circuit-lifetime component
provider.OnSettingHydrationFailed += failure =>
    logger.LogError(failure.Exception, "Tenant {TenantId} setting '{Key}' failed to hydrate",
                    failure.TenantId, failure.SettingKey);
```

The log line is free; subscribe when you want to *react* (surface a banner, flag the tenant for
repair) rather than record. Anything that is not a `JsonException` propagates and takes the request
down — that is deliberate, because it is a code bug rather than bad data.

## Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `ITenantRepository` | Scoped | Per-scope snapshot plus `Initialize` / `InitializeAsync`. No cross-request cache. |
| `ITenantProvider` | Scoped | Read API. Caches hydrated settings per scope; cleared on tenant change and on configuration reload. |
| `ITenantSettingsSerializer` | Singleton | Frozen `JsonSerializerOptions` and their per-type `JsonTypeInfo` cache. |
| `ITenantSource` | yours, `Scoped` recommended | Not registered by the package. Put your durable cache here. |

A hand-written `ITenantProvider` double must declare both events (`OnChange` and
`OnSettingHydrationFailed`; auto-events you never raise are valid), expose non-nullable `Current`
and a `Resolution`, and can build the façade from itself — `TenantSettings` has a public
constructor: `Settings => _settings ??= new TenantSettings(this)`.

## Known limitations

- **Persisted circuit state is client-visible.** `Zonit.Extensions.Website`'s `TenantStateBridge`
  carries the tenant across the prerender → interactive boundary by embedding a `TenantSnapshot` in
  the prerendered HTML. That means **everything** in `Tenant.Variables` reaches the browser in clear
  text, not just the values a page renders. Keep secrets in `IConfiguration` or a secret store,
  which the circuit can reach directly. Related: the bridges are silently disabled under
  `PublishTrimmed` — see `.zonit/extensions/website/hydration.md`.
- **No write path.** This package reads tenants. Persisting an edited setting is the host's job;
  `ITenantSettingsSerializer.Serialize` gives you the correct blob shape, but storing it is yours.
- **`Templates` is advisory.** Nothing consumes it — it exists for admin UIs to offer presets.

## Upgrading from 10.0.0-preview.10

| Change | What to do |
|---|---|
| `Tenant.Solo` → `Tenant.Default`, `IsSolo` → `IsDefault` | Rename. The old names still work as `[Obsolete]` aliases and go away next preview. |
| `Current` stays nullable | Unchanged from preview.10. `Initialize(null)` resets the scope. Settings never read it, so no null checks are needed for `Settings` / `GetSetting<T>()`. |
| New `TenantResolution Resolution` on both interfaces | Implementers (test doubles, decorators) must add it. Multi-domain hosts should handle `Unknown`. |
| `Setting<T>.Hydrate(string)` → `Hydrate(string, JsonSerializerOptions)`, and now `virtual` | Delete your override **and** your per-setting `JsonSerializerContext` — the generator emits the metadata. Keep them only for nested/collection models or custom JSON rules; if you keep the override, add the parameter. |
| Settings are AOT-safe via generated metadata | Nothing to do — a module initializer registers it. Only models the generator cannot describe (ZONITTS0003) still need a context, and that context is picked up automatically too. |
| Blob property matching is now case-insensitive | Nothing to do. Blobs that silently produced defaults now bind — verify you were not relying on the broken behaviour. |
| Settings read `Tenants:{key}` from `IConfiguration` when no blob exists | Nothing to do unless you already have a `Tenants` config section meaning something else — rename it or set `o.ConfigurationSection`. |
| A generated plugin accessor is a property, not a method | Drop the parentheses: `Settings.Pricing()` → `Settings.Pricing`. |
| Unknown hosts log a `Warning` | Expect the new line in multi-site logs; it is pointing at a real misconfiguration. |
| Unknown hosts return 404 by default in web hosts | Multi-site hosts that legitimately serve unknown domains must set `o.UnknownHost = UnknownHostBehavior.Continue`. Single-site hosts are unaffected. |
| Registration of JSON contexts is automatic | Delete hand-written `AddJsonContext(X.Default)` chains. Keep one only to pin precedence when two assemblies describe the same model. |
| New `ZONITTS0003` warning | Add the named `[JsonSerializable]` entry, or `<NoWarn>` the id if the model is deliberately reflective. |
| `Tenant.Id` is now `required Guid?` | Set it explicitly. A real tenant gets its store's id; a tenant with no identity gets `null`. Replace any `Guid.Empty` you were using as "no id". `SettingHydrationFailure.TenantId` and `TenantSnapshot.Id` are `Guid?` to match. |
