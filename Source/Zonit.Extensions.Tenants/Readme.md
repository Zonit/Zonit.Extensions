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
- **`ITenantProvider`** (scoped, read) — `Tenant? Current`, `TenantResolution Resolution`,
  `TenantSettings Settings`, `TSetting GetSetting<TSetting>() where TSetting : ISetting, new()`,
  `event Action? OnChange`, `event Action<SettingHydrationFailure>? OnSettingHydrationFailed`.
- **`TenantResolution`** — `None` / `SingleSite` / `Resolved` / `Unknown`. How a multi-domain host
  tells "no tenancy configured" from "this hostname has no tenant behind it".
- **`Tenant`** — a **sealed class** (not a record): `required Guid? Id`, `required string Domain`,
  `FrozenDictionary<string,string> Variables`. `Id` is required *and* nullable on purpose:
  required so forgetting it is a compile error, nullable so "no identity" is sayable without
  `Guid.Empty` standing in for it. The framework never produces `Guid.Empty`.
- **`Setting<TModel>`** — base for a setting: `Key` / `Name` / `Description` / `Value` /
  `Templates`. JSON handling is **inherited**; override `Hydrate` only for unusual rules.
- **`ITenantSettingsSerializer`** (singleton) — the write half. `Serialize(model)` produces exactly
  the blob shape settings read back, so persistence code cannot drift from the reader.
- **Four built-ins** — `SiteSetting` (`site`), `ThemeSetting` (`theme`),
  `MaintenanceSetting` (`maintenance`), `SocialMediaSetting` (`social_media`).
  `MaintenanceSetting` stores a flag; nothing in this package acts on it.
- **A Roslyn generator** (`analyzers/dotnet/cs`) that gives every `Setting<T>` you declare a
  strongly-typed accessor.

## Setup

```csharp
using Zonit.Extensions;            // AddTenantsExtension() — namespace Zonit.Extensions
using Zonit.Extensions.Tenants;    // ITenantProvider, ITenantSource, Tenant

builder.Services.AddScoped<ITenantSource, MyTenantSource>();   // multi-site only

// AddWebsite() already calls AddTenantsExtension(). Call it directly only in non-web hosts,
// or to change ConfigurationSection / ReloadOnChange — JSON metadata needs no wiring.
builder.Services.AddTenantsExtension();
```

`AddTenantsExtension()` `TryAdd`s two scoped services (`ITenantRepository`, `ITenantProvider`)
plus the singleton `ITenantSettingsSerializer`, and is idempotent — every call contributes to the
same options instance, so a host and an area can each register their own context.

Setting models need **no registration at all**: the generator describes them and a module
initializer hands the metadata to the runtime as each assembly loads. See
[Your own setting](#your-own-setting).

**No `ITenantSource` is registered** — a single-site app leaves that seam empty and `Current`
stays `null`; a multi-site app registers its own. If a host resolves the seam
directly, use `GetService<ITenantSource>()`, because `GetRequiredService` throws when none is
registered.

Without middleware (console, worker, tests) drive the repository yourself:

```csharp
using var scope = provider.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
await repository.InitializeAsync("acme.example.com");
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

## Unknown hosts

`Current` is nullable — `Id` and `Domain` have no meaningful value before a tenant resolves — but a
null there costs no page a null check, because **settings never read it**: `Settings` and
`GetSetting<T>()` always resolve. What a null does not say is *why*, so that lives next to it:

| `Resolution` | Meaning |
|---|---|
| `None` | Nothing resolved in this scope — console, worker, test, circuit before the bridge |
| `SingleSite` | No `ITenantSource` registered. A single-site app; defaults are the answer |
| `Resolved` | A source returned a tenant |
| `Unknown` | A source was asked and did not recognise the host |

`Unknown` is the one that matters in production: a hostname pointed at the app with no tenant
behind it — a DNS record without the matching row, a typo'd alias, a staging host leaking into
production.

**In a web host this is handled for you.** `TenantMiddleware` answers `404` and logs a `Warning`
naming the host, because the alternative — serving compile-time default branding on a domain the
app does not know — is indistinguishable from a working site. Opt out when unknown hosts are
legitimate (catch-all landing page, health probes on an internal name):

```csharp
builder.Services.AddWebsite<MyApp>(o => o.UnknownHost = UnknownHostBehavior.Continue);
```

Single-site hosts are unaffected: with no `ITenantSource` the resolution is `SingleSite`, never
`Unknown`. Outside a web host, or with `Continue`, branch on it yourself:

```csharp
if (Tenants.Resolution is TenantResolution.Unknown)
    return Results.NotFound();
```

### Want a catch-all site instead of a 404?

Return one from your `ITenantSource` rather than letting the host go unresolved. The tenant is then
genuinely `Resolved`, `Current` is never `null`, and the catch-all carries its own settings —
things a framework-supplied sentinel could not do:

```csharp
public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
    => await LookupAsync(domain, ct) ?? CatchAll;

private static readonly Tenant CatchAll = new()
{
    Id = null,                       // no identity, but a real tenant
    Domain = "*",
    Variables = /* the marketing site's settings */,
};
```

This is the supported way to have a "default site" in a multi-domain app. The framework
deliberately does **not** do it for you: substituting a default on an unrecognised host is exactly
how a typo'd DNS record ends up serving a real-looking page, and it would also make `Unknown`
indistinguishable from `SingleSite`, so nothing could 404 or log.

## Settings from appsettings.json

A setting with no persisted override reads its values from configuration, so a single-site app can
be configured entirely in `appsettings.json` and never write persistence code:

```json
{
  "Tenants": {
    "site":  { "Title": "Acme", "Language": "en-US" },
    "theme": { "PrimaryColor": "#0F766E", "Roundness": "Large" },
    "acme_pricing": { "Plan": "pro", "SeatLimit": 25 }
  }
}
```

Keys under `Tenants` are `ISetting.Key` values. It is ordinary `IConfiguration`, so every provider
applies — `appsettings.{Environment}.json`, user secrets, environment variables
(`Tenants__site__Title`), Key Vault, command line. Property casing is free-form, and enums accept
either the member name (`"Large"`) or its number (`3`).

**Precedence, per setting key:** persisted `Tenant.Variables` blob → configuration → compile-time
default. So configuration acts as the house default for *every* tenant and a stored value always
wins — a multi-site host can use it for shared branding and let individual tenants override.

Editing the file re-renders open Blazor circuits: a reload invalidates hydrated settings and raises
`ITenantProvider.OnChange`, which components already subscribe to for tenant switches. Configure
with `o.ConfigurationSection = "…"` and `o.ReloadOnChange = false` if you want otherwise.

## The storage contract

`Tenant.Variables` maps `ISetting.Key` → JSON of the model. Blobs are written **camelCase** with
nulls omitted, and enums as **numbers**. Inject `ITenantSettingsSerializer` and you cannot get it
wrong:

```csharp
var json = serializer.Serialize(new SiteSettingsModel { Title = "Acme", Language = "en-US" });
// {"title":"Acme","metaDescription":"This is a new website created","language":"en-US"}
```

**Reading ignores casing.** `{"title":…}`, `{"Title":…}` and `{"TITLE":…}` all bind, so a blob
written by hand, by an older tool, or by a plain `JsonSerializer.Serialize(model)` still works. This
matters more than it looks: matching used to be case-sensitive, and a PascalCase blob then matched
nothing, hydrated the compile-time defaults, and raised **no** exception and **no** event — a page
quietly rendering "New website" was the only symptom.

What is still position-sensitive is enum *numbering*: renumbering an enum member reinterprets every
stored blob. Malformed JSON is a different case — it is logged at `Warning` (category
`Zonit.Extensions.Tenants.Services.TenantService`, event id 1) **and** raised on
`ITenantProvider.OnSettingHydrationFailed` as
`SettingHydrationFailure(Guid TenantId, string SettingKey, Exception Exception)`. The log line is
what you get for free; subscribe to the event when you want to *react* rather than record. Logging
is `Microsoft.Extensions.Logging.Abstractions` only — an optional dependency, so a host with no
logging provider still resolves `ITenantProvider`.

## Your own setting

```csharp
namespace Acme.Pricing;

public sealed class PricingModel { public string Plan { get; set; } = "free"; }

public sealed class PricingSetting : Setting<PricingModel>
{
    public override string Key         => "acme_pricing";
    public override string Name        => "Pricing";
    public override string Description => "Billing plan shown on the pricing page.";
}
```

That is the whole setting — no `Hydrate`, no JSON context. Every property of the model needs a
sensible default, because "tenant has no override" is answered with `new()`, never with `null`.

**That is also the whole AOT story — there is nothing to register.** The generator describes
`PricingModel` through `JsonMetadataServices` (the same public API System.Text.Json's own generator
emits calls to) and emits a module initializer that hands it to the runtime as the assembly loads.
No `JsonSerializerContext`, no `[JsonSerializable]`, no `Hydrate`, no wiring in `Program.cs`. A
Native AOT publish compiles through ILC with zero IL warnings.

**What the generator covers** is flat models: properties whose type is a scalar, an enum, or a
nullable of either, with public getters and setters. That is what a settings model is — a POCO with
`DataAnnotations` that an `EditForm` binds against. A model with a nested object, a collection or a
get-only property falls outside it and reports **ZONITTS0003** at build time. Declare a context for
that one; it is picked up automatically too:

```csharp
[JsonSerializable(typeof(NestedPricingModel))]
public sealed partial class AppJsonContext : JsonSerializerContext;
```

A hand-written context always wins over the generated description of the same model, so it is also
the escape hatch when you need different JSON rules. Describing a model *partially* is never done:
a missing property would bind to its default and look like data loss, so the model is skipped
whole and the diagnostic says so.

Without either, the model hydrates reflectively — correct on a JIT host, and a clear
`InvalidOperationException` naming the model under AOT.

`AddTenantsExtension(o => o.AddJsonContext(...))` still exists, and is the way to pin precedence
when two assemblies describe the same model: what a host registers by hand is consulted before
anything the generator registered.

Override `Hydrate(string json, JsonSerializerOptions options)` only when a setting needs different
JSON rules than the shared camelCase / `WhenWritingNull` contract — a custom converter, string
enums. The shared options deliberately do **not** inherit a context's own
`[JsonSourceGenerationOptions]`, so reach for the concrete `JsonTypeInfo` in that case.

The generator emits a `TenantSettingsExtensions` class into `Acme.Pricing`, so with
`using Acme.Pricing;` you read `Tenants.Settings.Pricing` — an **extension property** (C# 14
`extension` block), which is why a plugin setting reads exactly like a built-in one. A partial class
still cannot span assemblies, so it is not a real `TenantSettings` member; the four built-ins are.
A consumer who pins `<LangVersion>` below 14 gets the older extension-**method** shape,
`Tenants.Settings.Pricing()`, because extension properties do not exist there.
`GetSetting<PricingSetting>().Value` works everywhere and needs no `using`.

The setting type must be `public` — non-public types are skipped, because an accessor for one
would be `CS0122` at every call site.

## Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `ITenantRepository` | Scoped | Per-scope snapshot, no cross-request cache |
| `ITenantProvider` | Scoped | Caches hydrated settings per scope |
| `ITenantSettingsSerializer` | Singleton | Holds the frozen options and their per-type `JsonTypeInfo` cache |
| `ITenantSource` | yours, `Scoped` recommended | Not registered by the package — put your durable cache here |

## AOT / trimming

The package is `IsTrimmable` / `IsAotCompatible`, and a Native AOT publish of an app using it
compiles through ILC with **zero** IL2xxx/IL3xxx warnings.

It carries exactly two `[UnconditionalSuppressMessage]` pairs, on the reflection fallback in
`Setting<T>` and its mirror in the serializer. Both are guarded by
`JsonSerializer.IsReflectionEnabledByDefault` and **throw** rather than run when reflection is off,
so an AOT build cannot silently take that path — it fails with a message naming the model and the
fix. Register your models with `AddJsonContext` and neither is ever reached.

Configuration binding goes through `Utf8JsonWriter` driven by `JsonTypeInfo.Properties`, not
through `IConfiguration.Bind`, precisely because the reflection binder is not AOT-safe here: the
configuration binding source generator works by intercepting call sites where the bound type is
statically known, and this library's call site is behind an open generic.

## Known limitations

- **Outside a web host nothing seeds the tenant.** `ITenantRepository` is scoped and only
  `TenantMiddleware` writes to it, so a console app, a worker or a test stays on `Tenant.Default`
  and every setting renders its compile-time defaults until you call `Initialize` /
  `InitializeAsync` yourself. Inside a Blazor host this used to hit interactive circuits too — a
  different scope the middleware never runs against — and `Zonit.Extensions.Website`'s
  `TenantStateBridge` now closes that gap by persisting a `TenantSnapshot` during prerender and
  restoring it into the circuit.
  Note that persisted state is embedded in the prerendered HTML, so **everything** in
  `Tenant.Variables` travels to the browser in clear text: keep secrets in `IConfiguration`.
- **Colliding accessor names break the build.** Two settings in one namespace that reduce to the same
  accessor (`FooSetting` and `Foo`) emit duplicate extension methods → `CS0111` in generated code.
  Rename one, or move it to another namespace.
