# Hydration: carrying scoped state from SSR into the circuit

`<WebsiteHydrator />` is the single component that moves Identity, culture, workspace, catalog and
cookie state across the **prerender → interactive** boundary. Place it once, in your root
component (`App.razor`), with a render mode.

## Why anything is needed at all

`app.UseWebsite<TApp>(…)` installs `CookieMiddleware`, `SessionMiddleware`, `WorkspaceMiddleware`,
`ProjectMiddleware`, `TenantMiddleware` and `CultureMiddleware` into the Site branch. They all
write into **scoped** repositories on the **HTTP request scope**.

An interactive Blazor Server component does not live in that scope. The SignalR circuit gets its
own DI scope, created when the WebSocket connects, and **no middleware ever runs against it**. So
every scoped repository starts at its default there: `Identity.Empty`, no organization, no
project, default culture, zero cookies — even though the SSR pass that produced the HTML you are
looking at had all of them populated.

The symptom is unmistakable: the page renders correctly, then flashes to a signed-out /
empty-switcher / wrong-language version the moment the circuit takes over.

`PersistentComponentState` is the framework's way across: the SSR pass serialises a snapshot into
the response HTML, the interactive pass reads it back. `IPersistentStateProvider` is this
package's per-domain adapter over it.

## Placing the component

```razor
@* App.razor *@
@using Zonit.Extensions.Cultures
@using Zonit.Extensions.Website.Hydration
@inject ICultureProvider Culture

<!DOCTYPE html>
<html lang="@Culture.Current.ValueOrDefault">
<head>
    <base href="/" />
    <ImportMap />
    <HeadOutlet @rendermode="@RenderMode.InteractiveServer" />
</head>
<body>
    <WebsiteHydrator @rendermode="@RenderMode.InteractiveServer" />

    <Routes @rendermode="@RenderMode.InteractiveServer" />

    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

Four rules, all load-bearing:

1. **`@using Zonit.Extensions.Website.Hydration`.** The component's namespace is
   `Zonit.Extensions.Website.Hydration`, not `Zonit.Extensions.Website`. Put the using in
   `_Imports.razor` if you prefer.
2. **Give it a render mode.** `App.razor` is static SSR by default. Without
   `@rendermode`, the hydrator renders on the SSR pass only — it will persist state that nothing
   ever restores, and the circuit still starts empty. Use the same render mode as `<Routes />`.
3. **Before `<Routes />`.** `Restore` runs in the hydrator's `OnInitialized`, so it must complete
   before any page body initialises; components are initialised in render order.
4. **Exactly once per root component.** A second instance installs a second set of persist
   callbacks and every snapshot gets written twice.

If you mount the Dashboard (`app.UseDashboard(…)`), its `DashboardApp.razor` already contains the
tag — do not add another.

## What the component does

```csharp
foreach (var bridge in Bridges)          // IEnumerable<IPersistentStateProvider>, scoped
{
    bridge.Restore(State);                             // interactive pass reads the snapshot
    _subscriptions.Add(bridge.RegisterPersist(State)); // SSR pass registers the writer
}
```

`Restore` first, deliberately, so an `@inject`ed provider in any page sees a populated repository
on its very first render. Subscriptions are disposed on teardown so a hot reload does not stack
duplicate callbacks.

`PersistentComponentState` in .NET 10 exposes only the *generic* `PersistAsJson<T>` /
`TryTakeFromJson<T>` — there is no `Type`-accepting overload — which is why each bridge, not a
central dispatcher, makes the typed call. A generic dispatcher would need `MakeGenericMethod`.

## The built-in bridges

All six are registered by `AddWebsite()` via
`TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, …>())`.

| Bridge | State key | Payload | Restored into | Persisted when |
| --- | --- | --- | --- | --- |
| `AuthStateBridge` | `ZonitIdentityExtension` | `Identity` | `IAuthenticatedRepository.Initialize` | `Current.HasValue` |
| `CultureStateBridge` | `ZonitCulturesExtension` | `string` (BCP 47 tag) | `ICultureManager.SetCulture` | culture `HasValue` |
| `WorkspaceStateBridge` | `ZonitOrganizationsExtension` | `Organizations.StateModel` | `IWorkspaceManager.Initialize` | `manager.State` non-null |
| `CatalogStateBridge` | `ZonitProjectsExtension` | `Projects.StateModel` | `ICatalogManager.Initialize` | `manager.State` non-null |
| `CookieStateBridge` | `ZonitCookiesExtension` | `List<CookieModel>` | `ICookiesRepository.Initialize` | at least one cookie |
| `TenantStateBridge` | `ZonitTenantsExtension` | `TenantSnapshot` | `ITenantRepository.Initialize` | tenant resolved |

The keys are the names of the ComponentBase bridges that used to do this job
(`<ZonitIdentityExtension />` and friends). Those components were **deleted** in commit `1cfc6d8`
— the keys were kept only so an SSR blob already in flight during a deploy still deserialises. If
your `App.razor` still has any of these tags, delete them; they no longer exist as types:

```razor
@* All five of these were removed. They do not compile against 10.0.0-preview.10. *@
<ZonitCulturesExtension />
<ZonitIdentityExtension />
<ZonitOrganizationsExtension />
<ZonitProjectsExtension />
<ZonitCookiesExtension />
```

Replace the lot with one `<WebsiteHydrator @rendermode="…" />`.

**Tenants cross the boundary too.** `TenantStateBridge` carries the resolved tenant as a
`TenantSnapshot`, so `ITenantProvider.Current` and `Settings.*` hold the tenant's values in an
interactive component instead of reverting to compile-time defaults. The snapshot exists because
`Tenant.Variables` is a `FrozenDictionary`, which `System.Text.Json` cannot deserialize directly.

## Writing your own bridge

Implement `IPersistentStateProvider` and register it as **scoped**. Scoped matters: the bridge
holds a constructor-injected reference to the scoped repository it writes into, so a singleton
would freeze itself to whichever scope resolved first.

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Website.Hydration;

internal sealed class BasketStateBridge(IBasketRepository repository) : IPersistentStateProvider
{
    private const string Key = "MyApp.Basket";

    private const DynamicallyAccessedMemberTypes JsonMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    [DynamicDependency(JsonMembers, typeof(BasketState))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "BasketState is rooted by the [DynamicDependency] on this method.")]
    public void Restore(PersistentComponentState state)
    {
        if (!JsonSerializer.IsReflectionEnabledByDefault)
            return;

        if (state.TryTakeFromJson<BasketState>(Key, out var restored) && restored is not null)
            repository.Initialize(restored);
    }

    [DynamicDependency(JsonMembers, typeof(BasketState))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Same rationale as Restore.")]
    public PersistingComponentStateSubscription RegisterPersist(PersistentComponentState state)
        => state.RegisterOnPersisting(() =>
        {
            if (!JsonSerializer.IsReflectionEnabledByDefault)
                return Task.CompletedTask;

            state.PersistAsJson(Key, repository.Current);
            return Task.CompletedTask;
        });
}
```

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IPersistentStateProvider, BasketStateBridge>());
```

Contract notes:

- `TryAddEnumerable` (not `AddScoped`) so your bridge stacks alongside the built-in five instead
  of replacing them.
- `Restore` must be **idempotent** — circuits re-render and the repository may already hold the
  value.
- Only write when the value is meaningful. Every persisted byte lands in the SSR HTML.
- Root every type the reflective JSON binder will walk. `[DynamicallyAccessedMembers]` on the
  payload type is **not** enough — it does not recurse, so a nested DTO reachable only through a
  property type needs its own `[DynamicDependency]`. That is why `WorkspaceStateBridge` roots
  three types, not one.
- Keep the `IsReflectionEnabledByDefault` guard. Without it the call throws during the prerender
  persist phase in a trimmed publish and takes the whole response down.

## Known limitations

**Hydration is silently disabled under `PublishTrimmed` — not only `PublishAot`.**

All five bridges (and `PageViewBase`'s model persistence) begin with
`if (!JsonSerializer.IsReflectionEnabledByDefault) return;`. The .NET SDK clears that feature
switch for **any** `PublishTrimmed` publish, and `PublishAot` implies `PublishTrimmed`. The
framework's `PersistentComponentState` uses its own `JsonSerializerOptions` instance, which
carries no `TypeInfoResolver`, so with reflection off the serializer has nothing to fall back on
and both calls would throw.

The consequence, measured in a published binary: in a trimmed Blazor Server app the identity,
active culture, workspace, catalog and cookie snapshot **do not cross the boundary**. The circuit
starts anonymous, with default culture and no organization — and

- the failure is otherwise indistinguishable from "hydration is working", because SSR still renders
  correctly; only the interactive pass is wrong.

**You control what happens instead of guessing.** `HydrationOptions.WhenSerializationUnavailable`
decides how the bridges react when reflection-based JSON is off:

```csharp
services.AddWebsite(o => { /* areas … */ });
services.Configure<HydrationOptions>(o =>
    o.WhenSerializationUnavailable = HydrationUnavailableBehavior.Throw);
```

| `HydrationUnavailableBehavior` | Behaviour |
| --- | --- |
| `Warn` (default) | each bridge logs a warning once, then skips — loud enough to find in logs |
| `Throw` | fail fast at startup rather than serve a silently anonymous circuit |
| `Silent` | the old behaviour; only pick this if you know hydration is not needed |

`HydrationSerialization.IsAvailable` is the predicate the bridges consult, so a custom bridge can
make the same decision the built-in ones do.

If you need the state and you are publishing trimmed, opt reflection back in and accept the trim
risk:

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

Otherwise publish untrimmed. Blazor **WebAssembly** is unaffected — its SDK explicitly restores
the switch to `true`.

There is a framework seam that would fix this properly —
`Microsoft.AspNetCore.Components.PersistentComponentStateSerializer<T>` is public and overridable
in .NET 10, and a subclass backed by a source-generated `JsonTypeInfo` compiles against the
shipped package. It is not wired up here. `PersistAsBytes` / `TryTakeBytes` are `internal`, so
there is no way to hand a `JsonTypeInfo` to the calls the bridges actually make today.

**Two settings a bridge cannot carry.** `TenantStateBridge` moves the tenant itself, but the
snapshot is a point-in-time copy: a tenant record edited between the SSR pass and the circuit
starting is not re-read, and nothing invalidates the restored value until the next full request.

## See also

- `.zonit/extensions/website/hosting.md` — the middleware pipeline that populates the request scope
- `.zonit/extensions/website/pages.md` — `PageViewBase<T>.PersistentModel`, same gate
- `.zonit/extensions/website/aot.md` — the full trim/AOT story
- `.zonit/extensions/website/ui-services.md` — `ICookieProvider.RefreshAsync()`, the manual
  fallback for cookies in a circuit
- `.zonit/extensions/tenants/tenants.md` — tenant settings
