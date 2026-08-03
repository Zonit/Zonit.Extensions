# Razor base components

The five classes in this folder. Namespace: `Zonit.Extensions.Website`.

```
ComponentBase
 └─ Base                    cancellation + logger
     └─ ExtensionsBase      injected providers, T()/TM(), breadcrumbs
         └─ PageBase        + runtime LayoutKey            ← inherit this by default
             └─ PageViewBase<T>    + Model / LoadAsync / persistence
                 └─ PageEditBase<T>  + EditContext / validation / auto-save
```

The consumer-facing version of this document — with the traps, the tables and the full snippets —
ships in the package as `.zonit/extensions/website/pages.md`. This file is the source-tree summary.

## `Base`

Owns a `CancellationTokenSource` created in the constructor. `Dispose(bool)` **cancels** it before
disposing (this was inert up to 10.0.0-preview.9), and `ComponentToken` returns an already-cancelled
token afterwards instead of throwing `ObjectDisposedException`.

It overrides the framework's parameterless `OnInitializedAsync()`, `OnParametersSetAsync()` and
`OnAfterRenderAsync(bool)` and forwards each to a `CancellationToken` overload:

```csharp
protected virtual Task OnInitializedAsync(CancellationToken cancellationToken);
protected virtual Task OnParametersSetAsync(CancellationToken cancellationToken);
protected virtual Task OnAfterRenderAsync(bool firstRender, CancellationToken cancellationToken);
```

**Derived components must override the token overload.** Overriding the parameterless one compiles
and silently unhooks everything below it in the chain.

`Logger` is an `ILogger` categorised by the concrete component's `FullName`, backed by an injected
`ILoggerFactory`. It is a null logger until `OnInitialized` has run. No finalizer — the class holds
only managed state, and a finalizer on every Blazor component was pure GC cost.

## `ExtensionsBase`

Resolves nine providers lazily from the scope's `IServiceProvider`; **the `OnChange` subscription
is installed on first property access**, so a component that never reads `Workspace` will not
refresh when it changes.

| Property | Type | Reaction to `OnChange` |
| --- | --- | --- |
| `Culture` | `ICultureProvider` | `OnUIRefreshChangeAsync` |
| `Workspace` | `IWorkspaceProvider` | `OnRefreshChangeAsync` |
| `Catalog` | `ICatalogProvider` | `OnRefreshChangeAsync` |
| `Tenant` | `ITenantProvider` | `OnRefreshChangeAsync` |
| `BreadcrumbsProvider` | `IBreadcrumbsProvider` | `OnUIRefreshChangeAsync` |
| `Authenticated` | `IAuthenticatedProvider` | not subscribed |
| `Toast` | `IToastProvider` | not subscribed |
| `Cookie` | `ICookieProvider` | not subscribed |
| `LayoutContext` | `ILayoutContext` | not subscribed |

Plus `[Inject] NavigationManager Navigation`.

- `OnUIRefreshChangeAsync()` — `InvokeAsync(StateHasChanged)`.
- `OnRefreshChangeAsync()` — re-runs `OnInitializedAsync(ComponentToken)` then re-renders. Returns
  immediately when the token is already cancelled, so a provider event racing with teardown does
  not restart a load.

Translation helpers: `T(string, params object[])` → `string`,
`TM(string, params object[])` → `MarkupString` (raw HTML, unencoded),
`Translate(string, params object[])` → the `Translation` value object.

`Options<TModel>()` reads `IOptionsMonitor<TModel>.CurrentValue` and subscribes the component to
`OnChange`. It is annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.

Breadcrumbs are declarative: `ShowBreadcrumbs` is a tri-state (`true` = publish `Breadcrumbs`,
`false` = leave the current trail alone, `null` = clear it), read in `OnInitialized` — i.e. *before*
`LoadAsync`. Crumbs that name loaded data must be published imperatively through
`BreadcrumbsProvider.Initialize(...)`.

## `PageBase`

Adds `protected string? LayoutKey` over `ILayoutContext`. `null` = no layout, `""` = fall back to
the Site default (overriding a static `[LayoutKey]`), any other string = a key in `ILayoutRegistry`.
Prefer the class attributes `[LayoutKey("…")]` / `[NoLayout]`, which `ZonitRouteView` reads before
the page is instantiated — the runtime property costs one extra render.

## `PageViewBase<T>`

```csharp
protected virtual TViewModel? Model { get; set; }
protected bool IsLoading { get; }
protected virtual bool PersistentModel { get; } = true;

protected virtual Task<TViewModel?> LoadAsync(CancellationToken cancellationToken);
protected Task RefreshAsync(CancellationToken cancellationToken = default);
```

`OnInitializedAsync` registers the persistence callback, tries to take the model back from
`PersistentComponentState` under `$"{GetType().Name}_Model"`, and only calls `LoadAsync` when
there was no snapshot. `IsLoading` guards against overlapping loads.

The class deliberately does **not** override `OnRefreshChangeAsync` any more. It used to, and
because the base is `async void` the `IsLoading` guard had usually not been set when the override's
second load started — one provider change produced up to two backend calls.

Both persistence calls are gated on `JsonSerializer.IsReflectionEnabledByDefault`, which the SDK
clears for any `PublishTrimmed` publish. Persistence then no-ops with a single `LogDebug` and the
component re-loads after hydration. There is no `JsonTypeInfo` alternative: in .NET 10
`PersistentComponentState` exposes only the reflective generic pair, and `PersistAsBytes` /
`TryTakeBytes` are `internal`.

The `[DynamicallyAccessedMembers]` annotation on `TViewModel` is what makes the two `IL2026`
suppressions honest. It does **not** recurse — a view model with a nested DTO graph needs its own
roots in the consumer, or persistence turned off.

## `PageEditBase<T>`

`where TViewModel : class, new()`. `Model` is re-declared non-nullable with
`[SupplyParameterFromForm]`, and an `EditContext` + `ValidationMessageStore` are created in
`OnInitialized` / `OnParametersSet` whenever the model instance changes.

Callbacks (wire these to `EditForm`, they are **not** virtual):

```csharp
public async Task HandleValidSubmit(EditContext editContext);
public void HandleInvalidSubmit();
```

Hooks (override these):

```csharp
protected virtual Task SubmitAsync(CancellationToken cancellationToken = default);
protected virtual void OnBeforeSubmit();
protected virtual void OnAfterSubmit(bool success);
protected virtual void HandleInvalidSubmit(string message);        // note: same name, one arg
protected virtual Task OnModelChanged(string fieldName, object? oldValue, object? newValue, CancellationToken ct = default);
protected virtual Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue, CancellationToken ct = default);
protected virtual Task HandleFieldAutoSaveError(string fieldName, Exception exception, CancellationToken ct = default);
protected virtual bool IsFieldAutoSaveEnabled(string fieldName);
```

Validation is installed on `EditContext.OnValidationRequested`: `Validator.TryValidateObject`
against the model, each `ErrorMessage` translated through `ICultureProvider`, results written into
the class's own `ValidationMessageStore`. Consumers must **not** add `<DataAnnotationsValidator />`
on top — a second store over the same rules doubles every message and therefore every toast.

Switches: `AutoTrimStrings` (default `true`), `AutoNormalizeWhitespace` (`true`, collapses `\s+`
to one space — destroys textarea line breaks), `TrackChanges` (`true`),
`PreventDuplicateSubmissions` (`true`, fixed 1 s window), `AutoSaveDelay` (800 ms fallback).
Trimming/normalisation runs once inside `HandleValidSubmit`, immediately before `SubmitAsync` —
never on keystroke.

Auto-save fires only for properties carrying `[AutoSave]` (or when `IsFieldAutoSaveEnabled` is
overridden). The debounce is a `System.Threading.Timer`, so `AutoSaveAsync` runs on a thread-pool
thread — UI work from there needs `InvokeAsync`.

`OnValueChanged<TValue>(TValue modelValue)` locates the target property by **value equality**
across all properties of that type; two properties currently holding the same value make it write
to the wrong one. `IsValid` reads the current message store without running validation.

## The source generator

`Zonit.Extensions.Website.SourceGenerators` emits a `ViewModelMetadata<T>` (compile-time property
delegates, `StringProperties` subset, `CreateInstance`) plus a `[ModuleInitializer]` registration,
for every non-generic `T` used as `PageViewBase<T>` / `PageEditBase<T>` in the consuming assembly.
When registered, `PageEditBase` uses those delegates instead of reflection for `CleanModelData`,
`GetFieldValue`, `IsFieldAutoSaveEnabled`, `GetFieldAutoSaveDelay` and `OnValueChanged`. Validation
and persistence stay reflective.

Current gaps, all reproduced against 10.0.0-preview.10:

- `{ get; init; }` properties (classes and records) emit `vm.X = v!` → **`CS8852`** in the
  consumer's build.
- `required` members break the emitted `CreateInstance() => new()` → **`CS9035`**.
- Properties **inherited** from a base class are silently absent — `GetMembers()` is not walked up
  the chain, so the generated path skips fields the old reflective path handled.
- The emitted file uses `file` types, so `LangVersion` ≤ 10 gives `CS8936`.

None of these produce a generator diagnostic. `Example/Zonit.Extensions.ConsumerGate` is the
regression gate; it currently covers one plain-class view model through `PageViewBase` and does not
exercise any of the shapes above.
