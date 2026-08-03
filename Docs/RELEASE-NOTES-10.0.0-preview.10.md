# Zonit.Extensions 10.0.0-preview.10 â€” release notes

Upgrading from **10.0.0-preview.9**. Every package moves together; there is no partial upgrade path.

This release unblocks preview.9 (both source generators emitted code that could not compile in any
consumer), then makes a set of breaking corrections that were not safe to defer. Read
[Breaking changes](#breaking-changes) before you upgrade â€” several are silent behaviour changes rather
than compile errors.

---

## Fixed: two ship blockers

**preview.9 could not be consumed.** Both source generators produced source that failed to compile in
the consumer's own compilation. If you worked around this with `ExcludeAssets="analyzers"`, **remove
that now** â€” the generators are the AOT-safe path and you lose it by excluding them.

```xml
<!-- delete this workaround -->
<PackageReference Include="Zonit.Extensions.Website" Version="10.0.0-preview.9"
                  ExcludeAssets="analyzers" />
```

### CS0534 â€” anyone deriving from `PageViewBase<T>`

The Website generator emitted a `JsonSerializerContext` subclass per view model, assuming
System.Text.Json's own generator would complete it. Roslyn does not run one generator over another's
output, so the partial class stayed abstract:

```
error CS0534: '__ZonitVMJsonContext_â€¦_MyVm' does not implement inherited abstract member
              'JsonSerializerContext.GetTypeInfo(Type)'
error CS0534: â€¦ does not implement inherited abstract member
              'JsonSerializerContext.GeneratedSerializerOptions.get'
```

`ZonitViewModelMetadata.g.cs` now contains only the metadata class and its `[ModuleInitializer]`
registration. `ViewModelMetadata<TViewModel>.JsonTypeInfo` is gone from the public API â€” nothing in the
framework ever read it. **If you hand-wrote a `ViewModelMetadata<T>` subclass that overrode
`JsonTypeInfo`, delete the override.** There is no replacement: `PersistentComponentState` has no public
`JsonTypeInfo`-accepting API in .NET 10 (`PersistAsBytes` / `TryTakeBytes` exist but are `internal`).

### CS0103 â€” anyone declaring a `Setting<T>`

The Tenants generator emitted `partial class TenantSettings` **into the consumer's compilation**,
referencing a `Provider` member that only exists in the hand-written half inside
`Zonit.Extensions.Tenants`:

```
error CS0103: The name 'Provider' does not exist in the current context
error CS0436: the type 'TenantSettings' conflicts with the imported type
```

Plugin settings are now surfaced as **extension methods in the setting's own namespace**, and the
`partial class TenantSettings` emission is gated to the single compilation that owns the hand-written
half, so it can never reappear in a consumer. See
[the Tenants breaking change](#plugin-setting-accessors-are-now-extension-methods) for the new call
shape.

A minimal consumer, `Example/Zonit.Extensions.ConsumerGate`, is now built by the solution. It declares
one `Setting<T>` and one `PageViewBase<T>` view model, so generator output that fails to compile in a
consumer breaks this repo's build first.

> **Caveat:** at the time of writing the gate project is still **untracked in git** (only
> `Example/Directory.Build.props` is committed), so the regression protection exists in the working tree
> and not yet in CI. Commit `Example/Zonit.Extensions.ConsumerGate/` and the solution entry before
> relying on it. Its coverage is also narrow â€” one plain-class view model and one plain setting. It does
> **not** exercise `PageEditBase`, records, init-only properties, `required` members, inherited
> properties, or colliding setting names, all of which are still broken (see known limitation 3).

---

## Breaking changes

### The `TimeZone` value object is renamed to `Zone`

`Zonit.Extensions.TimeZone` â†’ `Zonit.Extensions.Zone`. Same namespace, same struct; the rename touched
the type identifier and nothing else. The file moved from `ValueObjects/Time/TimeZone.cs` to
`ValueObjects/Time/Zone.cs`.

**Why.** `System.TimeZone` still exists in .NET 10, and `ImplicitUsings` puts `using System;` in every
file. Any consumer that wrote the type unqualified therefore got

```
error CS0104: 'TimeZone' is an ambiguous reference between 'Zonit.Extensions.TimeZone' and 'System.TimeZone'
```

and had to add `using TimeZone = Zonit.Extensions.TimeZone;` per file (and again in `_Imports.razor`) or
fully qualify every mention. A value object you cannot name without ceremony is a design defect, not a
documentation problem, so the type was renamed instead. `Zone` needs no alias.

**Member names did not change.** Only the type identifier moved:

| | preview.9 | preview.10 |
|---|---|---|
| Type | `Zonit.Extensions.TimeZone` | `Zonit.Extensions.Zone` |
| `ICultureState` read side | `TimeZone TimeZone { get; }` | `Zone TimeZone { get; }` |
| `ICultureManager` write side | `void SetTimeZone(TimeZone timeZone)` | `void SetTimeZone(Zone timeZone)` |
| `CultureOption` | `string DefaultTimeZone` | `string DefaultTimeZone` â€” unchanged |
| Statics | `TimeZone.Empty` / `TimeZone.Utc` / `TimeZone.Local` | `Zone.Empty` / `Zone.Utc` / `Zone.Local` |
| JSON converter type | `TimeZoneJsonConverter` | `TimeZoneJsonConverter` â€” unchanged |

Separately from the rename, the type gained `public const int Zone.MaxLength = 64` in this release â€”
use it to size the storage column instead of a literal.

`ICultureState.TimeZone`, `ICultureManager.SetTimeZone` and `CultureOption.DefaultTimeZone` keep their
names deliberately: they were never ambiguous and they name the concept, not the type. So the shape
reads `Zone TimeZone { get; }`, which is intentional.

**Migration.** Rename the type; leave every member alone.

```csharp
// preview.9
using TimeZone = Zonit.Extensions.TimeZone;      // delete this line
manager.SetTimeZone(new TimeZone(-5));
manager.SetTimeZone(TimeZone.Empty);
TimeZone tz = state.TimeZone;

// preview.10
manager.SetTimeZone(new Zone(-5));
manager.SetTimeZone(Zone.Empty);
Zone tz = state.TimeZone;                        // the property is still called TimeZone
```

Call sites that only pass strings â€” `manager.SetTimeZone("Europe/Warsaw")`, the `DefaultTimeZone`
option, the `"Culture:DefaultTimeZone"` configuration key â€” never named the type and need no change.
Persisted data is unaffected: the JSON converter still writes the canonical id as a plain string.

One observable side effect: `ValueObjectTypeConverter<T>` builds its validation message from
`typeof(T).Name`, so a rejected model-bound value now reports **`Zone is invalid.`** instead of
`TimeZone is invalid.` Re-baseline any test or UI string asserting on the old message.

### `Schedule` binary format is a fixed 20 bytes, with no backward compatibility

`MaxExecutions` and the `IsNow` flag are now persisted, which the old layout could not express.

| | preview.9 | preview.10 |
|---|---|---|
| `Schedule.StorageSize` | `public const int` = **16** | `public static readonly int` = **20** |
| `ToBytes()` | 16 bytes; `MaxExecutions` silently dropped | 20 bytes; `[15]` = flags (bit 0 = `IsNow`), `[16..20]` = `MaxExecutions` (Int32 LE, 0 = unlimited) |
| `FromBytes` on a 16-byte blob | read it | returns `Schedule.Empty` â€” **silently**, no exception, no log |
| `WriteToSpan` | needs â‰Ą 16 bytes | needs â‰Ą 20, else `ArgumentException` |
| JSON | 24-char base64 | 28-char base64 |

There is **one** format version and **no reader for the old layout**. An interim design with a version
byte and a `StorageSizeV1` constant was implemented and then removed: nothing had been persisted yet, so
carrying a compatibility path was cost without benefit. `StorageSizeV1` does not exist â€” if you saw it
in an intermediate build, delete the reference.

**Migration.** Widen the column to `BINARY(20)` / `VARBINARY(20)` **before** deploying:

```sql
ALTER TABLE Jobs ALTER COLUMN [Schedule] BINARY(20) NULL;
```

Rows that SQL Server right-pads with zeros on widening still read correctly â€” the first 16 bytes are
unchanged, the tag byte is still `1`, and the zeroed tail means `MaxExecutions = null`, which is what
those rows always meant. What does **not** work is a value that is still physically 16 bytes when it
reaches `FromBytes` (a `VARBINARY` column, a blob store, or old base64 in a JSON column): that is
rejected as `Schedule.Empty`, silently. Rewrite those rows from their source definition.

Size the column from the constant, never a literal:

```csharp
modelBuilder.Entity<Job>()
    .Property(j => j.Schedule)
    .HasConversion(v => v.ToBytes(), v => Schedule.FromBytes(v))
    .HasMaxLength(Schedule.StorageSize);
```

`StorageSize` changed from `const` to `static readonly` **on purpose**. A public `const` is inlined into
consuming assemblies at *their* compile time, so a downstream binary built against the old package would
have kept using `16` until recompiled and failed at runtime with no build-time warning.

Also: `Schedule.GetHashCode()` now folds in `MaxExecutions` and the flags byte, so hash values differ
from preview.9. Fine for in-memory dictionaries; never persist a `Schedule` hash code.

### `Schedule.Empty` and `default(Schedule)` are genuinely empty

- **before:** `Schedule.Empty.HasValue == true`; all calendar fields read `0`; `DayOfWeek` read
  `Sunday`; `ToString()` was `"Month=0, Day=0, DayOfWeek=Sunday, Hour=0, Minute=0, Second=0"`; and
  `Schedule.Empty != new Schedule()`.
- **after:** `HasValue == false`; every calendar property and `Interval` is `null`;
  `ToString()` is `"(empty)"`; `Schedule.Empty == new Schedule() == default(Schedule)`.

The same now holds for `new Schedule[n]` elements and EF-materialised `NULL` columns, which previously
looked like "midnight on day 0 of month 0".

**Migration.** Code that tested `schedule.HasValue` to decide whether to run a job now correctly gets
`false` for an unset schedule and stops scheduling those â€” that *is* the fix, but audit any call site
that relied on the old `true`. Code reading `.Hour` / `.Second` from a possibly-default `Schedule` now
gets `null` and must handle the nullable.

### `Asset.Data` returns a defensive copy

- **before:** `asset.Data` returned the live internal array. Mutating it changed the `Asset` in place
  while `Sha256`, `Size` and `Signature` kept their construction-time values. `byte[] b = asset;` handed
  out the same live array.
- **after:** `Data`, the implicit `byte[]` conversion and the new `Asset.ToArray()` each allocate a fresh
  copy â€” up to `Asset.MaxSize` (100 MB) **per call**. `ReferenceEquals(a.Data, a.Data)` is now `false`.

**Migration.** Replace `asset.Data` in hot paths with `asset.AsSpan()` / `asset.AsMemory()`, and use
`asset.ToStream()` for streaming â€” all three are allocation-free read-only views. Keep `Data` /
`ToArray()` only where you genuinely need an independent mutable array. Code that relied on `Data`
returning a stable reference (reference equality, or writing through it to update the `Asset`) silently
stops working.

> The **ingest** direction is unchanged and is a deliberate ownership contract: the constructor stores
> your array as-is rather than cloning a 100 MB payload. If you mutate the array you passed in, the
> payload changes behind a stale `Sha256`. Treat the array as consumed.

### `AssetValidationOptions.ValidateSignature` changed meaning

- **before:** "a magic-byte signature must be detectable". Because the table knows only 21 formats,
  every `.txt`, `.csv`, `.doc`, `.xls`, `.ppt`, `.flac` and prolog-less `.svg` failed the `Images()`,
  `Documents()`, `Audio()` and `Video()` presets. Meanwhile a disguised file (PNG bytes named
  `report.pdf`) *passed*, because a signature was present.
- **after:** "the detected signature must not **contradict** the file name". No signature â†’ passes.
  Container signatures (ZIP, XML, HTML, GZIP) â†’ pass, since one signature backs many extensions. MP4 and
  MOV are interchangeable. An error is raised only on a definite mismatch:
  `File content is 'image/png' but the name 'report.pdf' claims 'application/pdf'.`

New method `Asset.IsSignatureConsistent()` answers that question directly. `Asset.IsSignatureValid()` is
unchanged and still answers only "was a signature detected" â€” it is **not** a validity gate.

**Migration.** The old error string (`"Could not detect file signatureâ€¦"`) no longer exists â€” re-check
any UI or test asserting on it. Uploads whose content contradicts their name are now rejected, so re-run
fixtures that deliberately use mismatched bytes and names. Anyone who passed `validateSignature: false`
to work around the old behaviour can drop that argument.

### `Switch*Async` returns `Task<bool>`, and a denied switch is a no-op

Both twins changed identically:

```csharp
// before
Task SwitchOrganizationAsync(Guid organizationId, CancellationToken ct = default);
Task SwitchProjectAsync(Guid projectId, CancellationToken ct = default);

// after
Task<bool> SwitchOrganizationAsync(Guid organizationId, CancellationToken ct = default);
Task<bool> SwitchProjectAsync(Guid projectId, CancellationToken ct = default);
```

When the source answers `null` ("no access") the call now returns `false` and **leaves the previous
selection untouched** â€” no state write, no `OnChange`. Previously it cleared the workspace/catalog.

**Migration.** `await Manager.SwitchOrganizationAsync(id);` still compiles (the `bool` is discarded), but
this is **binary**-breaking: recompile. Anyone who *implements* `IWorkspaceManager` / `ICatalogManager`
must change the signature. Surface a refusal by branching on the result:

```csharp
if (!await Manager.SwitchOrganizationAsync(id, ct))
    Toast.AddError(T("You do not have access to that organization."));
```

`IOrganizationSource` / `IProjectSource` â€” the interfaces you actually implement â€” are unchanged.

### Switching on a scope that was never hydrated now reaches the source

- **before:** `if (_state is null) return;` â€” the switch never called the source, nothing changed,
  nothing was reported.
- **after:** the source is always called. On success the repository materialises a state model, stores
  the selection, back-fills the list if that scope never had one, and raises `OnChange`.

**Migration.** No API change, but your `IOrganizationSource` / `IProjectSource` implementation must now
tolerate `SwitchOrganizationAsync` / `SwitchProjectAsync` being called **without a preceding
`InitializeAsync` on the same scope**.

### Malformed source data degrades instead of throwing

`IWorkspaceProvider.Organization` and `ICatalogProvider.Project` / `.Visible` no longer throw
`ArgumentException` on bad rows:

| Source data | preview.9 | preview.10 |
|---|---|---|
| `Id == Guid.Empty` | `ArgumentException` | `Organization.Empty` / `Project.Empty`; the row is dropped from `Visible` |
| blank / whitespace name | `ArgumentException` | `Title.Empty` |
| name > 60 graphemes | `ArgumentException` | whitespace-normalised, cut at the 60th grapheme |

**Migration.** Two observable consequences. (1) `ICatalogProvider.Visible.Length` can now be **smaller**
than `ICatalogManager.Projects.Count` â€” never index one by the other, join on `Id`. (2) `Name.Value` may
be truncated or empty where the source supplied more; read the verbatim text from
`OrganizationModel.Name` / `ProjectModel.Name` through the manager. If you want strict rejection, validate
inside your own source.

### `INavigationProvider` is transient, not singleton

```csharp
// before
services.TryAddSingleton<INavigationProvider, NavigationService>();

// after
services.TryAddSingleton<NavigationRegistry>();                    // internal; holds the data + OnChanged
services.TryAddTransient<INavigationProvider, NavigationService>();// Site-aware facade
```

**No migration is required.** The data moved to a singleton behind the facade, so `Add` / `Clear` /
`Refresh` / `OnChanged` stay process-wide, and `@inject INavigationProvider` keeps working in
components exactly as before.

Transient rather than scoped is the deliberate choice, because it is the only lifetime that satisfies
both halves of the problem the change was made for:

- a singleton `IHostedService` taking `INavigationProvider` still resolves from the root provider and
  still starts, so the "seed menus at startup" pattern is untouched;
- a resolution that happens inside a request or circuit still sees that scope's `ICurrentSite`, so
  per-Site filtering is correct in an interactive render — which a singleton reaching through
  `IHttpContextAccessor` could never do (it silently skipped filtering when there was no `HttpContext`).

```csharp
// keeps working unchanged
internal sealed class NavData(INavigationProvider navigation) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        navigation.Add(new NavGroup { /* … */ });
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### The five `<Zonit*Extension />` components were deleted

`<ZonitCulturesExtension />`, `<ZonitIdentityExtension />`, `<ZonitOrganizationsExtension />`,
`<ZonitProjectsExtension />` and `<ZonitCookiesExtension />` no longer exist. Their names survive only as
`PersistentComponentState` keys inside the replacement bridges.

```razor
@* before - in App.razor *@
<ZonitCulturesExtension />
<ZonitIdentityExtension />
<ZonitOrganizationsExtension />
<ZonitProjectsExtension />
<ZonitCookiesExtension />

@* after - one tag, and the render mode is mandatory *@
@using Zonit.Extensions.Website.Hydration
<WebsiteHydrator @rendermode="@RenderMode.InteractiveServer" />
```

Without `@rendermode` the component only renders on the SSR half and nothing is restored into the
circuit.

### Plugin setting accessors are now extension methods

```csharp
// before (never actually compiled - see the CS0103 ship blocker)
@Tenant.Settings.Pricing.Headline

// after - note the parentheses, and add a using for the setting's namespace
@using Acme.Pricing
@Tenant.Settings.Pricing().Headline
```

The generator now emits one `public static class TenantSettingsExtensions` per namespace that declares
settings, in that namespace. Built-in settings (`Site`, `Theme`, `Maintenance`, `SocialMedia`) are
unaffected and remain **properties**, because they compile inside `Zonit.Extensions.Tenants` itself.
`provider.GetSetting<PricingSetting>().Value` works unchanged and needs no `using`.

**One new constraint:** the generator now skips non-public setting types (previously it emitted an
accessor that failed with CS0122). Make a plugin `Setting<T>` `public` if you want an accessor.

### `ITenantSource` is no longer auto-registered

`AddTenantsExtension()` used to `TryAddScoped<ITenantSource, NullTenantSource>()`. `NullTenantSource` is
deleted and nothing is registered in its place, so in a host that registers no source:

```csharp
services.GetService<ITenantSource>();          // null (was: a no-op instance)
services.GetRequiredService<ITenantSource>();  // throws InvalidOperationException
```

`TenantRepository`'s constructor parameter became optional (`ITenantSource? source = null`), so the
repository itself still resolves and the solo-tenant branch (`Tenant.Solo`) is now reachable. Multi-site
hosts that register their own source are unaffected.

Related: solo-mode and unknown-host requests now raise the tenant change notification **once** instead of
twice, so a single-site page no longer re-runs its data load twice per render pass.

### `ITenantProvider` gained a second event

```csharp
public event Action<SettingHydrationFailure>? OnSettingHydrationFailed;
```

where `SettingHydrationFailure` is a new `public record (Guid TenantId, string SettingKey, Exception
Exception)` in `Zonit.Extensions.Tenants.Settings`. Only affects code that **implements**
`ITenantProvider` (a test double, a decorator) â€” add the member; an auto-event you never raise is a valid
implementation.

Relatedly, `TenantService` now only catches `JsonException` around `Setting<T>.Hydrate`. A
`NullReferenceException` or `InvalidOperationException` thrown by a plugin's `Hydrate` used to be
swallowed, leaving the caller with compile-time defaults indistinguishable from "no override"; it now
propagates.

### Component cancellation tokens are actually cancelled on `Dispose`

`Base.Dispose(bool)` used to dispose the `CancellationTokenSource` without cancelling it, so the tokens
handed to `OnInitializedAsync(ct)`, `LoadAsync(ct)`, `SubmitAsync(ct)` and friends never transitioned to
cancelled and every `if (token.IsCancellationRequested)` guard in derived pages was unreachable. It now
calls `Cancel()` first, and a new `protected CancellationToken ComponentToken` returns an
already-cancelled token once the source is gone.

**Migration.** Work that was (accidentally) relying on continuing after the user navigates away â€” a
fire-and-forget save, an audit write, a cache warm â€” must stop using the component's token. Pass
`CancellationToken.None` explicitly, or start it from a service with its own lifetime. Also: a component
that already declares its own member named `ComponentToken` now gets CS0108; add `new` or rename.

### Other component-model changes

- **`PageViewBase<T>` no longer overrides `OnRefreshChangeAsync`.** The base was `async void`, so its
  `IsLoading` guard usually ran before the flag was set and a provider change could trigger up to two
  `LoadAsync` calls. Measured effect: 3 provider changes now produce 3 backend loads instead of up to 6.
  A page that wants extra work per change can override `OnRefreshChangeAsync` itself and call `base`
  first.
- **Finalizers removed from `Base` and `ExtensionsBase`.** They were promoting every Blazor component
  instance to the finalizer queue for a `Dispose(false)` that had nothing to release. A derived component
  owning unmanaged resources must declare its own finalizer.

### Text, XML and money corrections

- **`TextBase<T>.WithSeparators` / `WithSplitOptions` no longer mutate the receiver.**
  `Text.Count("a|b|c").WithSeparators('|').Words` used to return `1` (the configuration landed on an
  object the caller had just thrown away); it now returns `3`. **Use the return value.** Code that called
  these for their side effect and kept reading the original instance now gets the defaults.
  `WithSeparators()` with an empty array keeps the current separators instead of silently switching to
  whitespace; `WithSeparators(null)` throws.
- **`TextNormalizer.ReplaceSmartQuotes` actually works now.** The U+201C/U+201D replacements had been
  mangled into ASCII-to-ASCII identities. All four curly double quotes now become `"`, and curly single
  quotes (including the typographic apostrophe U+2019 in "don't") now become `'`. Re-baseline golden
  files and search indexes.
- **`XmlConvertible.Serialize()` returns actual XML.** It previously returned `""` for every document
  short enough to fit the writer's buffer, because `ToString()` ran before the writer was flushed. Output
  is now invariant-culture with round-trip `"O"` dates and a `utf-8` declaration, byte-identical on
  en-US and pl-PL. Callers that treated an empty return as "nothing to serialize" now receive real XML.
  Documents hand-written on a comma-decimal culture (`19,99`) will now fail to deserialize.
- **`Money.TryParse` / `Price.TryParse` reject input longer than 512 characters.** The normalizer sized a
  stack buffer from the caller's string, so ~400 KB of digits produced an uncatchable
  `StackOverflowException` that killed the process. A `decimal` holds at most 29 significant digits, so
  nothing that could ever have parsed is affected.

### Cultures

- **Missing-translation recording is opt-in and capped.** Previously every unmatched `Translate` call
  permanently added an entry to a process-wide, unbounded singleton with no way to disable it. Now gated
  on `CultureOption.TrackMissingTranslations` (default `false`) and capped at
  `MaxTrackedMissingTranslations` (default 1000). A configured ceiling of â‰¤ 0 throws
  `ArgumentOutOfRangeException` when the singleton is first resolved.

  ```csharp
  services.AddCulturesExtension(o =>
  {
      o.TrackMissingTranslations = env.IsDevelopment();
      o.MaxTrackedMissingTranslations = 5000;
  });
  ```

  Check `IsFull` to know whether the report is a truncated sample, and `Clear()` after each flush. Do not
  enable it in production against user-facing input â€” the keys are the raw `Translate()` arguments.
- **The fallback pass uses `CultureOption.DefaultCulture`, not a hardcoded `"en-US"`.** An app configured
  with `DefaultCulture = "pl-pl"` previously got no fallback to its own default language, and its
  missing-key semantics were inverted. Comparison is `OrdinalIgnoreCase`, so canonical `"pl-PL"` matches
  the lowercase config/cookie/URL convention. Apps that left the default at `"en-US"` see no change.
- **`default(Translation)` now equals `Translation.Empty`.** `Value`, `ToString()` and the implicit string
  conversion return `string.Empty` instead of `null`. Remove any `?? fallback` around
  `Translation.Value`. Code that used a `null` `Value` to distinguish "never assigned" from "assigned an
  empty string" must switch to an explicit flag (e.g. `Translation?`).

### Removed public types

All five had zero implementations and zero registrations â€” `GetRequiredService` on them always threw.

| Removed | Use instead |
|---|---|
| `Zonit.Extensions.Organizations.IOrganizationProvider` | implement `IOrganizationSource.GetOrganizationsAsync`; read `IWorkspaceManager.Organizations` |
| `Zonit.Extensions.Organizations.IOrganizationManager` | move the `Guid` â†’ model lookup into your own service (not to be confused with `IWorkspaceManager`, which is unaffected) |
| `Zonit.Extensions.Organizations.IOrganizationEntity` | a `Guid` FK plus the `Organization` value object |
| `Zonit.Extensions.Organizations.Entities.Organization` | the `Zonit.Extensions.Organization` value object (the entity shadowed it by simple name) |
| `Zonit.Extensions.Projects.IProjectEntity` | a `Guid` FK plus the `Project` value object |

### MudBlazor add-on

- **`ZonitTextField<T>` with `OpenNewTab=true` only opens absolute `http`/`https` URLs.** The adornment
  used to be wired unconditionally and passed the raw text to `window.open`, so `javascript:alert(1)`,
  `data:text/html,â€¦`, `file:///C:/â€¦` and relative paths were all opened verbatim. The click callback is
  now wired only when the text parses to an absolute http(s) URL; otherwise MudBlazor renders a plain
  `MudIcon` (`<span class="mud-icon-root â€¦">`) instead of `<button class="mud-input-adornment-icon-button">`.
  **Any CSS or E2E selector assuming the button exists must tolerate the span**, e.g.
  `.mud-input-adornment-icon, button.mud-input-adornment-icon-button`.
- **The string handed to `window.open` is the canonical `Url.Value`, not the raw input.**
  `  HTTPS://Example.COM/Path?q=1  ` now opens `https://example.com/Path?q=1`. Interop mocks asserting on
  the exact argument must expect the canonical form.
- **`ZonitTextField<T>` now overrides `BuildRenderTree`** to refresh the adornment state. A component
  deriving from it and overriding `BuildRenderTree` **must call `base.BuildRenderTree(builder)`**.

---

## Fixed defects

**Zonit.Extensions**
- `Price` / `Money` parsing no longer crashes the process on very long input (see the 512-character gate
  above).
- `XmlConvertible.Serialize()` no longer returns an empty string; `SetPropertyValue` parses with the
  invariant culture and handles `DateTimeOffset` / `TimeSpan`.
- `TextBase<T>` fluent configuration composes instead of being discarded.
- Files with no detectable signature (plain text, CSV, legacy Office, FLAC, prolog-less SVG) are no
  longer rejected by the validation presets.

**Zonit.Extensions.Cultures**
- The translation fallback honours the configured default language.
- `default(Translation)` is no longer a null-bearing struct that violated its own "never null" contract.

**Zonit.Extensions.Organizations / .Projects**
- A single malformed row no longer takes down the whole `Visible` list.
- `ICatalogProvider.Project` / `.Visible` are cached snapshots rebuilt on `OnChange` rather than
  recomputed per read, so reading them inside `@foreach` is now free.
  *Consequence:* state you mutate behind the manager's back is invisible until you call
  `Initialize(state)` â€” no `OnChange` is raised otherwise.
- `<PackageTags>` no longer advertises `Blazor;AspNetCore` on packages that have no ASP.NET Core
  reference.

**Zonit.Extensions.Tenants**
- Corrupt setting JSON falls back to defaults *and* now reports through `OnSettingHydrationFailed`
  instead of being swallowed silently.
- `TenantsJsonContext` is now public, so the write side has a supported way to produce blobs in the shape
  `Hydrate` expects.

**Zonit.Extensions.Website**
- Model persistence and all five hydration bridges no longer **throw** under `PublishTrimmed` /
  `PublishAot`. See the first known limitation below for what replaced the crash.
- Every `IL3050` suppression on the hydration path was deleted rather than re-justified; the remaining
  `IL2026` justifications are backed by real `[DynamicDependency]` attributes.
- Component tokens, the duplicate `LoadAsync`, and the finalizers (all above).

---

## Known limitations

These are open. They are listed here rather than in a tracker so nobody rediscovers them the hard way.

1. **Hydration and model persistence silently no-op under `PublishTrimmed`, not just `PublishAot`.**
   The SDK sets `JsonSerializer.IsReflectionEnabledByDefault = false` for **any** trimmed publish. All
   five bridges and `PageViewBase`'s model persistence check that switch and return early. The result is
   a trimmed Blazor Server app whose interactive render starts anonymous, with default culture, no
   workspace, no catalog and no cookie-consent state â€” and only `PageViewBase` logs anything (at
   `Debug`); the bridges log nothing at all. This is indistinguishable from "hydration is working".
   Opt back in and accept the trim risk:

   ```xml
   <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
   ```

   Blazor WebAssembly is unaffected â€” its SDK sets the switch back to `true`.

2. **There is no tenant hydration bridge.** `TenantSnapshot` (a public, JSON-safe DTO that round-trips a
   `Tenant`, whose `FrozenDictionary` System.Text.Json cannot deserialize) shipped, but no
   `IPersistentStateProvider` consumes it and nothing registers one. An interactive circuit still starts
   with `ITenantProvider.Current == null`, so tenant settings revert to defaults after prerender. Drive
   `ITenantRepository.Initialize` yourself if you need it now.

3. **Both source generators still emit uncompilable code for three ordinary inputs**, with no generator
   diagnostic â€” the consumer sees a raw compiler error in code they did not write:

   | Input | Error |
   |---|---|
   | view model with `{ get; init; }` (class or `record`) | `CS8852` â€” the generator emits `vm.Name = v!` without checking `IsInitOnly` |
   | view model with `required` members | `CS9035` â€” the emitted `CreateInstance() => new();` cannot satisfy them |
   | two settings whose accessor names collide (`FooSetting` and `Foo`) | `CS0111` â€” suffix-stripping has no uniqueness pass |

   Two more silently misbehave: **inherited view-model properties are dropped** from the generated
   metadata (`GetMembers()` is not walked up the base chain), and a consumer pinned to `LangVersion` â‰¤ 10
   chokes on the emitted `file` types with `CS8936`.

4. **`IsAotCompatible=true` is not honest for every package.** Six of the eight are ILC-clean.
   `Zonit.Extensions.Website` has one real unsuppressed `IL2069` in
   `Layouts/Repositories/LayoutSeed.cs` â€” the record puts `[property: DynamicallyAccessedMembers(â€¦)]` on
   the property but leaves the positional constructor parameter unannotated â€” which the in-repo Roslyn
   analyzers do not catch, so the solution still builds 0/0. `Zonit.Extensions.Website.MudBlazor`
   inherits the flag while wrapping MudBlazor 9.7.0, which declares `IsTrimmable` **without**
   `IsAotCompatible` and calls `MakeGenericType` in
   `MudBlazor.Utilities.Converter.Dispatcher.DelegateHelper`.

5. **27 `[UnconditionalSuppressMessage]` attributes remain**, 26 of them in `Zonit.Extensions.Website`
   (14 in `PageEditBase`, 2 in `PageViewBase`, 10 across the five bridges) plus one in
   `Reflection/AssemblyProvider.cs`. Most are honest. Two are not:
   - `PageEditBase.GetFieldValueReflective`'s `IL2075` justification claims "`FieldIdentifier.Model` is
     the form-bound `TViewModel`", but that method is reached **only** when the caller's
     `fieldIdentifier.Model is TViewModel` test failed â€” i.e. exactly when the claim is untrue.
   - The `IL3050` on `TryValidate` is fragile rather than false: under Native AOT, validation reached
     purely through the object-typed `Validator.TryValidateObject` path silently returned `valid = true`
     for a model violating `[MinLength(3)]`. The shipped shape (a DAM-annotated generic call) is correct,
     but the safety margin is one refactor wide and the failure mode is silent acceptance of invalid form
     data.

   Six of the `PageEditBase` `IL3050`s suppress a diagnostic that is never emitted â€” `Type.GetProperty`,
   `Type.GetProperties`, `Validator.TryValidateObject` and `new ValidationContext(object)` carry only
   `[RequiresUnreferencedCode]` in the .NET 10 ref pack. Unnecessary, not false.

6. **`TenantSnapshot.Variables`' XML doc references `Setting<T>.Dehydrate`, which does not exist.** That
   design was implemented and deliberately reverted (it was a compile break for every existing
   derivative). The stale doc ships in `Zonit.Extensions.Tenants.xml`.

7. **`AddCulturesExtension()` now requires `IConfiguration` in the container.**
   `MissingTranslationRepository` is built from a factory that resolves `IOptions<CultureOption>`, which
   via `BindConfiguration("Culture")` needs `IConfiguration`. A bare
   `new ServiceCollection().AddCulturesExtension(â€¦)` throws
   `No service for type 'Microsoft.Extensions.Configuration.IConfiguration' has been registered` when the
   singleton is first resolved. Harmless in an ASP.NET host; it breaks minimal console and test
   containers that register only what they use.

8. **`ValidateSignature` provides less protection than its name suggests.** `IsSignatureConsistent()`
   returns `true` whenever no signature is detected, and the magic-byte table knows 21 formats with no
   MZ/PE entry. An MZ executable named `report.txt` therefore passes `Documents()`, and the same bytes
   named `logo.png` pass `Images()` â€” both were rejected before. The container exemption is also
   unconditional rather than scoped to zip-backed extensions, so a raw ZIP named `report.pdf` passes
   `Documents()`. This is the requested relaxation, not a regression, but do not treat the flag as an
   anti-malware control.

9. **`Color` does not survive its own string round-trip.** `Color.CssOklch` (and `ColorJsonConverter`)
   emits percentage lightness â€” `oklch(65.31% 0.1347 242.69)` â€” but `Color.TryParse` captures the number
   *outside* the `%` and reads `65.31` as a 0-1 lightness, which clamps to 1. `#3498DB` serialised and
   deserialised comes back `#AAFFFF`. Persist `Color.Hex` or the raw `L`/`C`/`H`/`Alpha` doubles.

10. **`AddNavigationsExtension()` does not work standalone**, despite an XML doc that says it does. A
    container with only that call fails `ValidateOnBuild` with
    `Unable to resolve service for type 'Zonit.Extensions.Website.WebsiteAreaRegistry'`. Use
    `AddWebsite()`.

---

## Upgrade checklist

1. Remove any `ExcludeAssets="analyzers"` you added to work around preview.9.
2. Rename the type `TimeZone` â†’ `Zone` and delete every `using TimeZone = Zonit.Extensions.TimeZone;`
   (including the one in `_Imports.razor`). Leave `ICultureState.TimeZone`, `SetTimeZone` and
   `DefaultTimeZone` spelled as they are.
3. Widen every `Schedule` column to 20 bytes **before** deploying; rewrite any row that can still reach
   `FromBytes` as a 16-byte array.
4. Replace the five `<Zonit*Extension />` components in `App.razor` with one `<WebsiteHydrator />` â€”
   **with a `@rendermode`**.
5. Change every `Tenant.Settings.MyPlugin` to `Tenant.Settings.MyPlugin()` and add a `using` for the
   setting's namespace. Make plugin `Setting<T>` types `public`.
6. Nothing to do for `INavigationProvider` — it became transient, so injecting it into a singleton
   `IHostedService` still works and per-Site filtering now also works inside a circuit.
7. Replace `asset.Data` in hot paths with `AsSpan()` / `AsMemory()` / `ToStream()`.
8. Recompile everything â€” `Switch*Async` returning `Task<bool>` is binary-breaking.
9. Re-baseline any test asserting on smart-quote output, the old signature-validation error string,
   `XmlConvertible.Serialize()` returning `""`, or the `TimeZone is invalid.` binding message.
10. If you publish trimmed, decide explicitly between losing prerender hydration and setting
    `JsonSerializerIsReflectionEnabledByDefault=true`.

---

## Build diagnostics you may now see

The two source generators report problems instead of emitting code that cannot compile. All six are
raised in **your** build, anchored on your declaration. If you build with `TreatWarningsAsErrors`,
the `Warning`-severity ones become errors — that is intended, they all mark real defects.

| Id | Severity | Meaning | What to do |
| --- | --- | --- | --- |
| `ZONITVM0001` | Warning | A view-model property has an `init`-only or non-public setter, so no setter delegate can be generated. Only reported for a view model used with `PageEditBase<T>`, the one type that writes through the metadata. | Give the property a settable accessor, or use `PageViewBase<T>` if the page never writes. |
| `ZONITVM0002` | Warning | No metadata class could be emitted (abstract, generic, inaccessible, or no public parameterless constructor); the page falls back to reflection. | Make the view model a concrete, accessible, non-generic type with a public parameterless constructor. |
| `ZONITVM0003` | Info | The view model has `required` members; the generated `CreateInstance()` satisfies them with an object initializer assigning defaults. | Nothing — but be aware the instance the framework creates has those members at their default value. |
| `ZONITVM0004` | Warning | The project pins `LangVersion` below C# 9, which the emitted `[ModuleInitializer]` needs, so nothing was generated. | Raise `LangVersion` to 9.0 or later, or accept the reflective fallback. |
| `ZONITTS0001` | Warning | Two `Setting<T>` types in one namespace reduce to the same accessor name (the name with a trailing `Setting` stripped), so only one accessor is generated. | Rename one of the settings. |
| `ZONITTS0002` | Warning | A `Setting<T>` reduces to an accessor name `TenantSettings` already defines, so no accessor is generated. | Rename the setting. |

Reach any of these through `ITenantProvider.GetSetting<T>()` / `ViewModelMetadata<T>.Instance` if you
need the behaviour the generator declined to emit an accessor for.
