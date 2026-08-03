# Zonit.Extensions.Tenants

Per-domain site identity ("tenant") plus its persisted, strongly-typed settings.
Framework-agnostic: this package has **no ASP.NET Core dependency** — it references only
`Microsoft.Extensions.DependencyInjection.Abstractions` and
[Zonit.Extensions](../Zonit.Extensions/Readme.md). The HTTP glue (`TenantMiddleware`) lives in
**Zonit.Extensions.Website**, is `internal`, and is installed by `app.UseWebsite<TApp>(...)`.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Tenants.svg)](https://www.nuget.org/packages/Zonit.Extensions.Tenants/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Tenants.svg)](https://www.nuget.org/packages/Zonit.Extensions.Tenants/)

```bash
dotnet add package Zonit.Extensions.Tenants
```

## What you get

- **`ITenantSource`** (you implement it) — `Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)`.
  You receive the raw `HttpRequest.Host.Host`; **you** own case folding, aliases, wildcards and all
  cross-request caching. The framework never compares `Tenant.Domain` against anything.
- **`ITenantRepository`** (scoped) — `Tenant? Current`, `void Initialize(Tenant?)`,
  `Task<Tenant?> InitializeAsync(string domain, CancellationToken)`, `event Action? OnChange`.
  Per-scope snapshot; the middleware drives it in web hosts, you drive it everywhere else.
- **`ITenantProvider`** (scoped, read) — `Tenant? Current`, `TenantSettings Settings`,
  `TSetting GetSetting<TSetting>() where TSetting : ISetting, new()`, `event Action? OnChange`,
  `event Action<SettingHydrationFailure>? OnSettingHydrationFailed`.
- **`Tenant`** — a **sealed class** (not a record): `required Guid Id`, `required string Domain`,
  `FrozenDictionary<string,string> Variables`, the sentinel `Tenant.Solo` (`Guid.Empty` / `"*"`) and
  `bool IsSolo`.
- **`Setting<TModel>`** — abstract base for a setting: `Key` / `Name` / `Description` / `Value` /
  `Templates` plus an AOT-safe `TModel Hydrate(string json)` you implement.
- **Four built-ins** — `SiteSetting` (`site`), `ThemeSetting` (`theme`),
  `MaintenanceSetting` (`maintenance`), `SocialMediaSetting` (`social_media`).
  `MaintenanceSetting` stores a flag; nothing in this package acts on it.
- **`TenantsJsonContext`** (public) — the source-generated `JsonTypeInfo`s for the four built-in
  models. Use it on the **write** side so the blobs you persist match what `Hydrate` reads.
- **A Roslyn generator** (`analyzers/dotnet/cs`) that gives every `Setting<T>` you declare a
  strongly-typed accessor.

## Setup

```csharp
using Zonit.Extensions;            // AddTenantsExtension() — namespace Zonit.Extensions
using Zonit.Extensions.Tenants;    // ITenantProvider, ITenantSource, Tenant

builder.Services.AddScoped<ITenantSource, MyTenantSource>();

// AddWebsite() already calls AddTenantsExtension(); call it directly only in non-web hosts.
builder.Services.AddTenantsExtension();
```

`AddTenantsExtension()` `TryAdd`s exactly two scoped services (`ITenantRepository`,
`ITenantProvider`) and is idempotent. **It registers no `ITenantSource`** — a single-site app leaves
that seam empty and the middleware seeds `Tenant.Solo`; a multi-site app registers its own. If a host
resolves the seam directly, use `GetService<ITenantSource>()`, because `GetRequiredService` throws
when none is registered.

Without middleware (console, worker, tests) drive the repository yourself:

```csharp
using var scope = provider.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
await repository.InitializeAsync("acme.example.com");   // or repository.Initialize(Tenant.Solo);
```

## Reading settings

```razor
@inject ITenantProvider Tenants

<h1>@Tenants.Settings.Site.Title</h1>
<style>:root { --primary: @Tenants.Settings.Theme.PrimaryColor; }</style>
```

`Settings.Site` returns the **model** (`SiteSettingsModel`). `GetSetting<SiteSetting>()` returns the
**setting** — add `.Value` for the equivalent expression. Both resolve to the same instance, cached
per scope and invalidated on `OnChange`. Treat the models as read-only: mutating one changes what
every later reader in the scope sees and persists nothing.

## The storage contract

`Tenant.Variables` maps `ISetting.Key` → JSON of the model. The shape is whatever the setting's
`Hydrate` deserialises with; for the built-ins that is `TenantsJsonContext`, which uses **camelCase**
and serialises enums as **numbers**.

```csharp
var json = JsonSerializer.Serialize(
    new SiteSettingsModel { Title = "Acme", Language = "en-US" },
    TenantsJsonContext.Default.SiteSettingsModel);
// {"title":"Acme","metaDescription":"This is a new website created","language":"en-US"}
```

Deserialisation is case-sensitive: a PascalCase blob (what a plain `JsonSerializer.Serialize(model)`
produces) matches nothing, hydrates the compile-time defaults, and raises **no** exception and **no**
event. Malformed JSON is different — it surfaces on `ITenantProvider.OnSettingHydrationFailed` as
`SettingHydrationFailure(Guid TenantId, string SettingKey, Exception Exception)`. This package has no
`ILogger` dependency, so subscribing and forwarding to your logger is the only way to see it.

## Your own setting

```csharp
namespace Acme.Pricing;

public sealed class PricingModel { public string Plan { get; set; } = "free"; }

public sealed class PricingSetting : Setting<PricingModel>
{
    public override string Key         => "acme_pricing";
    public override string Name        => "Pricing";
    public override string Description => "Billing plan shown on the pricing page.";

    public override PricingModel Hydrate(string json)
        => JsonSerializer.Deserialize(json, PricingJsonContext.Default.PricingModel) ?? new();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PricingModel))]
public sealed partial class PricingJsonContext : JsonSerializerContext;
```

The generator emits a `TenantSettingsExtensions` class into `Acme.Pricing`, so with
`using Acme.Pricing;` you call `Tenants.Settings.Pricing()` — an extension **method**, because a
partial class cannot span assemblies. The four built-ins are properties because they are compiled
inside this package. `GetSetting<PricingSetting>().Value` works everywhere and needs no `using`.

The setting type must be `public` (non-public types are skipped) and `Hydrate` must use a
source-generated `JsonSerializerContext` — the reflection overloads break trimming and AOT.

## Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `ITenantRepository` | Scoped | Per-scope snapshot, no cross-request cache |
| `ITenantProvider` | Scoped | Caches hydrated settings per scope |
| `ITenantSource` | yours, `Scoped` recommended | Not registered by the package — put your durable cache here |

## AOT / trimming

The package is `IsTrimmable` / `IsAotCompatible` and contains no `[UnconditionalSuppressMessage]`,
no `[RequiresUnreferencedCode]` and no `[DynamicallyAccessedMembers]`. Hydration is dispatched to
each concrete `Setting<T>.Hydrate`, so AOT-safety of a custom setting is the author's responsibility.

## Known limitations

- **Interactive Blazor circuits have no tenant.** The middleware only runs for HTTP request scopes,
  so `Current` is `null` in a circuit and settings render their compile-time defaults after
  prerender. `TenantSnapshot` (`From` / `ToTenant`) exists as the payload for a prerender→circuit
  bridge, but that bridge is not implemented yet and nothing in the SDK consumes the type.
- **Colliding accessor names break the build.** Two settings in one namespace that reduce to the same
  accessor (`FooSetting` and `Foo`) emit duplicate extension methods → `CS0111` in generated code.
  Rename one, or move it to another namespace.
