# Trimming and Native AOT

`Zonit.Extensions.Website` ships with `IsTrimmable=true` and `IsAotCompatible=true`. Read that
flag as "the assembly compiles clean under the analyzers", not as "every feature works after a
trimmed publish". Two headline features **turn themselves off** under `PublishTrimmed`. This page
says exactly which, so you can choose your publish mode with the facts.

## The short version

| Publish mode | Pages / forms | Prerender → circuit hydration | `PageViewBase` model persistence |
| --- | --- | --- | --- |
| default (JIT, untrimmed) | works | works | works |
| `PublishTrimmed=true` | works | **silently off** | **silently off** |
| `PublishAot=true` | works | **silently off** | **silently off** |
| Blazor WebAssembly | works | works | works |

If you need hydration, publish untrimmed — or re-enable reflective JSON (below) and accept the
trim risk.

## What warns at your call site

Build a consumer with `EnableTrimAnalyzer` / `EnableAotAnalyzer` on and exactly **two** members of
this package produce diagnostics. Measured against 10.0.0-preview.10:

```
warning IL2026 / IL3050 : Zonit.Extensions.WebsiteServiceCollectionExtensions.AddWebsite(...)
warning IL2026 / IL3050 : Zonit.Extensions.Website.ExtensionsBase.Options<TModel>()
```

Nothing else. In particular, **deriving from `PageBase`, `PageViewBase<T>` or `PageEditBase<T>`
produces no warnings** — the `[DynamicallyAccessedMembers(PublicProperties | PublicFields |
PublicConstructors)]` annotation on the type parameter is what keeps the trimmer honest about your
view model.

### `AddWebsite`

```csharp
[RequiresUnreferencedCode("Razor Components and Antiforgery use reflection. Components from area assemblies are discovered dynamically.")]
[RequiresDynamicCode("Razor Components and Antiforgery may emit dynamic code at runtime.")]
public static IServiceCollection AddWebsite(this IServiceCollection services, Action<WebsiteOptions>? configure = null)
```

This is honest, not decorative: `MapRazorComponents<TApp>()` discovers routable components across
area assemblies at runtime, and antiforgery binds reflectively. There is no annotated-safe variant.
Either publish untrimmed, or suppress at your own call site and take responsibility:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Razor Components discovery is reflective; this host is published without trimming.")]
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
    Justification = "Razor Components discovery is reflective; this host is published without trimming.")]
public static void ConfigureServices(IServiceCollection services)
    => services.AddWebsite(o => { o.RazorComponents = true; });
```

`UseWebsite<TApp>(…)` itself carries no `Requires*` attribute — only
`[DynamicallyAccessedMembers(All)]` on `TApp`.

### `ExtensionsBase.Options<T>()`

Backed by `IOptionsMonitor<TModel>`, which binds configuration by reflecting over `TModel`'s
public properties. The type parameter is annotated, so under a plain trimmed publish `TModel`'s
members survive; the `RequiresDynamicCode` is the part that genuinely cannot be satisfied under
ILC. Either suppress locally after checking your options type is a flat POCO, or resolve
`IOptionsMonitor<T>` yourself with `EnableConfigurationBindingGenerator` turned on in your project.

## What the source generator actually removes

`Zonit.Extensions.Website.SourceGenerators` emits a `ViewModelMetadata<T>` with compile-time
delegates for each view model. When it is registered, `PageEditBase` switches these five paths off
reflection entirely:

- `CleanModelData` (the `AutoTrimStrings` / `AutoNormalizeWhitespace` pass)
- `GetFieldValue`
- `IsFieldAutoSaveEnabled`
- `GetFieldAutoSaveDelay`
- `OnValueChanged<T>`

That is the whole list. The generator does **not** touch:

- **DataAnnotations validation.** `Validator.TryValidateObject` and `new ValidationContext(object)`
  are still reflective on every submit.
- **Model persistence.** `PersistentComponentState.PersistAsJson<T>` is reflective STJ.
- **The hydration bridges.** Same.
- **Razor component discovery** and antiforgery, inside `AddWebsite`.
- **`IOptionsMonitor<T>` binding.**

It also emits nothing for a generic view model, so `PageViewBase<List<OrderRow>>` keeps the
reflective fallback. Shapes it cannot handle at all (`init`-only, `required`) break the consumer's
build — see `.zonit/extensions/website/pages.md`.

Analyzers are not transitive through `ProjectReference`. NuGet consumers get the generator from
`analyzers/dotnet/cs` automatically; a source-build consumer must add it by hand or silently stays
on the reflective path.

## The reflection gate

Every JSON round trip in this package starts with:

```csharp
if (!JsonSerializer.IsReflectionEnabledByDefault)
    return;
```

The .NET SDK clears that feature switch for **any** `PublishTrimmed` publish — `PublishAot`
implies `PublishTrimmed`, so it covers both. `PersistentComponentState` serialises through the
framework's own `JsonSerializerOptions` instance, which carries no `TypeInfoResolver`, so without
reflection the call throws. The gate turns a crash into a no-op.

Affected surface:

| Site | Behaviour with the switch off |
| --- | --- |
| `AuthStateBridge` | identity does not cross into the circuit; **no log** |
| `CultureStateBridge` | culture resets to default; **no log** |
| `WorkspaceStateBridge` | organization/workspace empty; **no log** |
| `CatalogStateBridge` | project/catalog empty; **no log** |
| `CookieStateBridge` | cookie snapshot empty; **no log** |
| `PageViewBase<T>` persistence | `LoadAsync` re-runs after hydration; one `LogDebug` |

Four of the six report nothing at all, and the app looks healthy because SSR still renders
correctly. Treat "hydration works in dev, breaks in prod" as this, first.

To keep the behaviour and stay trimmed:

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

That re-arms the reflective serializer for the whole app; the payload types are rooted with
`[DynamicDependency]` inside the bridges, so the built-in five survive trimming. Your own view
models are covered by the `[DynamicallyAccessedMembers]` on `TViewModel` for their **own**
members — the annotation does not recurse, so a nested DTO in the graph needs its own root.

There is no per-feature opt-in and no build-time warning. Full detail in
`.zonit/extensions/website/hydration.md`.

## Why there is no `JsonTypeInfo` path

The obvious fix — hand a source-generated `JsonTypeInfo` to the persistence calls — is not
reachable through the API the bridges use. In .NET 10 `PersistentComponentState` exposes only the
generic reflective `PersistAsJson<T>` / `TryTakeFromJson<T>`; `PersistAsBytes` / `TryTakeBytes`
exist but are `internal`. Verified against the shipped reference assemblies: those two methods
carry `[RequiresUnreferencedCode]` and **not** `[RequiresDynamicCode]`.

Up to 10.0.0-preview.9 the generator also emitted a `[JsonSerializable]` partial deriving from
`JsonSerializerContext`. Roslyn does not chain generators, so STJ's generator never completed it
and **every** consumer build failed with `CS0534`. That emission is gone, along with the dead
`ViewModelMetadata<T>.JsonTypeInfo` property it fed. If you want an AOT-safe context for a view
model for your own purposes, declare it in your own assembly:

```csharp
[JsonSerializable(typeof(BasketState))]
internal partial class AppJsonContext : JsonSerializerContext;
```

`Microsoft.AspNetCore.Components.PersistentComponentStateSerializer<T>` is public and overridable
in .NET 10 and is the framework's intended seam for AOT-safe persistence. This package does not use
it yet.

## Suppressions still in the assembly, and which ones to trust

26 `[UnconditionalSuppressMessage]` attributes remain in `Zonit.Extensions.Website`: 10 in the
hydration bridges, 2 in `PageViewBase`, 14 in `PageEditBase`. Being explicit about their quality
is the point of this page:

| Group | Judgement |
| --- | --- |
| Hydration ×10 (IL2026) | **Honest.** Backed by real `[DynamicDependency]` roots, a hand-written `JsonConverter` (`Identity`) or a primitive payload (`string`), and gated at runtime on top. |
| `PageViewBase` ×2 (IL2026) | **Honest.** `[DynamicallyAccessedMembers]` on `TViewModel` plus the runtime gate. |
| `PageEditBase` ×6 (IL3050) | **Unnecessary but not false.** They sit over `Type.GetProperty/GetProperties`, `Validator.TryValidateObject` and `new ValidationContext(object)` — none of which is `[RequiresDynamicCode]` in .NET 10, so they suppress a diagnostic that is never emitted. Cosmetic noise, not a hidden failure. |
| `PageEditBase` ×1 (IL2075, `GetFieldValueReflective`) | **Demonstrably false.** The justification claims the model is always the form-bound `TViewModel`, but that method is only reached when the caller's `fieldIdentifier.Model is TViewModel` test *failed* — i.e. exactly when the claim is untrue. |
| `PageEditBase` IL3050 on `TryValidate` | **Fragile.** The shipped shape routes through a `TViewModel`-typed `ValidationContext`, which behaves correctly. An independent Native AOT build that reached the same validation only through the `object`-typed path returned `valid = true` for a model violating `[MinLength(3)]` — silently accepting invalid form data. One refactor of margin. |

## Known limitations

- **`Zonit.Extensions.Website.MudBlazor` declares `IsAotCompatible=true` and is not.** It inherits
  the flag from the shared build props while its entire job is wrapping MudBlazor, which ILC
  reports with `IL3050` (`MakeGenericType` in MudBlazor's converter dispatcher) and `IL2075` in
  `MudFormComponent<,>`. Do not treat that package's badge as a guarantee. See
  `.zonit/extensions/mudblazor/mudblazor.md`.
- **One real ILC warning originates in this assembly.** `Layouts/Repositories/LayoutSeed.cs`
  declares `record LayoutSeed(string Key, [property: DynamicallyAccessedMembers(...)] Type LayoutType)` —
  the annotation lands on the property but not on the positional constructor parameter, so the
  compiler-generated backing-field store is unannotated and ILC emits `IL2069`. It is unsuppressed
  and the in-repo Roslyn analyzers do not catch it, which is the same blind spot that shipped
  preview.9.
- **Command-line `-p:PublishAot=true` fails at restore for `ProjectReference` consumers.** The
  `netstandard2.0` source-generator projects report `NETSDK1207`; the `<PublishAot>false</PublishAot>`
  reset inside them cannot beat a global property, and `UndefineProperties` does not cover the
  restore graph. Set `<PublishAot>true</PublishAot>` inside your csproj instead — that path works.
  NuGet consumers are unaffected.
- **`PageEditBase`'s validation is reflective.** `[Required]`, `[MaxLength]` etc. are rooted by the
  framework; **your own** `ValidationAttribute` subclasses must be public so the trimmer keeps
  them.

## Checklist for a trimmed publish

1. Suppress `AddWebsite`'s IL2026/IL3050 at your call site, with a justification that says which
   publish mode you actually use.
2. Decide about hydration: either set
   `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>`,
   or accept that circuits start anonymous with default culture and no workspace.
3. Keep view models as plain `{ get; set; }` classes so the generator can emit metadata.
4. Make custom `ValidationAttribute` types public.
5. Publish once with `-p:TrimmerSingleWarn=false` and read the ILC output — the in-repo analyzers
   do not see everything ILC does.

## See also

- `.zonit/extensions/website/hydration.md` — the gate in full, and how to write a bridge that respects it
- `.zonit/extensions/website/pages.md` — which view-model shapes the generator supports
- `.zonit/extensions/website/hosting.md` — `AddWebsite` / `UseWebsite<TApp>`
