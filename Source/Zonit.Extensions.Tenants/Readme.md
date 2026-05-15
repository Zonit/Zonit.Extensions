# Zonit.Extensions.Tenants

Per-domain tenant settings + plugin-aware configuration system for Zonit applications.

## What it gives you

- **`Tenant`** record — id + domain + persisted setting overrides (JSON blobs in a frozen dictionary, O(1) lookup).
- **`Setting<TModel>`** — abstract base for any setting. Plugins (Areas) ship a class deriving from this with their own model class (POCO + DataAnnotations).
- **`ITenantProvider`** — read API. Strongly-typed access via auto-generated `Settings.{Site,Theme,Maintenance,SocialMedia}` plus open-typed `GetSetting<TSetting>()` for plugin settings.
- **`ITenantSource`** — *consumer-side* data source contract. Implement against your DB / cache / remote API. The middleware resolves tenants by host name (case-insensitive) on the first non-static request of each scope.
- **`TenantMiddleware`** — wired automatically by `UseWebsite()`; static-asset bypass + lazy hydration.
- **Built-in settings** — `SiteSetting`, `ThemeSetting`, `MaintenanceSetting`, `SocialMediaSetting`. All AOT-safe via source-generated `JsonSerializerContext`.

## Plugin recipe

```csharp
// 1. Define your model — vanilla POCO, DataAnnotations welcome.
public sealed class MyPluginModel
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Caption { get; set; } = "Hello";
}

// 2. Define your setting + AOT-safe Hydrate.
public sealed class MyPluginSetting : Setting<MyPluginModel>
{
    public override string Key         => "my_plugin";
    public override string Name        => "My Plugin";
    public override string Description => "Plugin-specific options.";

    public override MyPluginModel Hydrate(string json)
        => JsonSerializer.Deserialize(json, MyPluginJsonContext.Default.MyPluginModel) ?? new();
}

[JsonSerializable(typeof(MyPluginModel))]
internal partial class MyPluginJsonContext : JsonSerializerContext;
```

Now `tenantProvider.GetSetting<MyPluginSetting>().Value.Caption` works in any Razor page or component. Built-in settings additionally appear on the auto-generated `tenantProvider.Settings.{PluginName}` facade.

## Lifetime

- `ITenantRepository` — `Scoped`. Per-request snapshot, no cross-request cache.
- `ITenantProvider` — `Scoped`. Caches hydrated settings per scope; invalidated on tenant change.
- `ITenantSource` — your impl, recommended `Scoped`. **Add caching here** if needed (decorator over `IMemoryCache` / `IDistributedCache`).

## AOT / trimming

Fully compatible. Hydration uses `JsonSerializerContext` (source-generated), no reflection on hot paths, no `[UnconditionalSuppressMessage]` in this package.
