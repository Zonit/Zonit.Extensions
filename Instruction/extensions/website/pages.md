# Razor pages: `PageBase`, `PageViewBase<T>`, `PageEditBase<T>`

Base components for `.razor` pages hosted by `Zonit.Extensions.Website`. All of them live in
namespace `Zonit.Extensions.Website` and ship in the `Zonit.Extensions.Website` package.

```
ComponentBase
 └─ Base                    cancellation + logger
     └─ ExtensionsBase      injected providers, T()/TM(), breadcrumbs
         └─ PageBase        + runtime LayoutKey            ← inherit this by default
             └─ PageViewBase<T>    + Model / LoadAsync / persistence
                 └─ PageEditBase<T>  + EditContext / validation / auto-save
```

## Read this first

**Every overridable lifecycle method takes a `CancellationToken`.** `Base` overrides the
framework's parameterless `OnInitializedAsync()` / `OnParametersSetAsync()` /
`OnAfterRenderAsync(bool)` and forwards to a `CancellationToken` overload. Override the
**token overload**. Overriding the parameterless one compiles and silently breaks the chain —
`LoadAsync`, breadcrumbs and everything else below stop running.

```csharp
// WRONG — hides Base's forwarder, PageViewBase.LoadAsync never runs
protected override async Task OnInitializedAsync() { … }

// RIGHT
protected override async Task OnInitializedAsync(CancellationToken cancellationToken) { … }
```

**The token is really cancelled on teardown.** `Base.Dispose(bool)` calls
`CancellationTokenSource.Cancel()` before disposing it, and `ComponentToken` returns an
already-cancelled token afterwards. Verified: a fresh component reports
`IsCancellationRequested == false`; after `Dispose()` it reports `true`. Up to and including
`10.0.0-preview.9` the source was disposed without cancelling, so every
`if (token.IsCancellationRequested)` guard in consumer pages was dead code. If you had
fire-and-forget work (audit write, cache warm) riding on a page's token, it now aborts when the
user navigates away — pass `CancellationToken.None` explicitly or move it to a service.

**`Logger` is a no-op until `OnInitialized` has run.** The lazy factory is created there, so a
constructor-time `Logger.LogX(...)` writes nowhere.

**Put the usings in `_Imports.razor` once.** Three namespaces cover everything on this page:

```razor
@using Zonit.Extensions                          @* UrlPath, Title, Permission, Identity … *@
@using Zonit.Extensions.Website                  @* PageBase & co., ZonitToasts, providers *@
@using Zonit.Extensions.Website.Authentication   @* [RequirePermission], [RequireRole] *@
```

## `Base` — what you get

| Member | Kind | Notes |
| --- | --- | --- |
| `CancellationToken ComponentToken` | `protected` | Live token; already-cancelled after `Dispose`. Never throws. |
| `CancellationTokenSource? CancellationTokenSource` | `protected`, private setter | Nulled on dispose. |
| `bool IsDisposed` | `protected` | |
| `ILogger Logger` | `protected` | Category = the concrete component's `FullName`. |
| `ILoggerFactory? LoggerFactory` | `[Inject]`, `protected` | |
| `Task OnInitializedAsync(CancellationToken)` | `protected virtual` | |
| `Task OnParametersSetAsync(CancellationToken)` | `protected virtual` | |
| `Task OnAfterRenderAsync(bool firstRender, CancellationToken)` | `protected virtual` | |
| `void Dispose(bool disposing)` | `protected virtual` | Call `base.Dispose(disposing)`. |
| `static void ThrowIfCancellationRequested(CancellationToken)` | `protected` | |

There is **no finalizer** on `Base` or `ExtensionsBase` (removed in preview.10 — they only held
managed state and were promoting every component to the finalizer queue). A derived component
owning unmanaged resources must declare its own.

## `ExtensionsBase` — the injected surface

Every one of these is a `protected` property resolved lazily from the scope's
`IServiceProvider`. **The `OnChange` subscription is installed on first access**, so a page that
never touches `Workspace` will not re-render when the workspace changes.

| Property | Type | Namespace | On change |
| --- | --- | --- | --- |
| `Culture` | `ICultureProvider` | `Zonit.Extensions.Cultures` | UI re-render |
| `Workspace` | `IWorkspaceProvider` | `Zonit.Extensions.Organizations` | **re-runs `OnInitializedAsync`** |
| `Catalog` | `ICatalogProvider` | `Zonit.Extensions.Projects` | **re-runs `OnInitializedAsync`** |
| `Tenant` | `ITenantProvider` | `Zonit.Extensions.Tenants` | **re-runs `OnInitializedAsync`** |
| `Authenticated` | `IAuthenticatedProvider` | `Zonit.Extensions.Auth` | not subscribed |
| `Toast` | `IToastProvider` | `Zonit.Extensions.Website` | not subscribed |
| `Cookie` | `ICookieProvider` | `Zonit.Extensions.Website` | not subscribed |
| `BreadcrumbsProvider` | `IBreadcrumbsProvider` | `Zonit.Extensions.Website` | UI re-render |
| `LayoutContext` | `ILayoutContext` | `Zonit.Extensions.Website` | not subscribed |
| `Navigation` | `NavigationManager` | ASP.NET Core | `[Inject]`, plain |

Two refresh hooks, both `protected virtual async void`:

- `OnUIRefreshChangeAsync()` — `InvokeAsync(StateHasChanged)` only.
- `OnRefreshChangeAsync()` — re-runs `OnInitializedAsync(ComponentToken)` then re-renders. This
  is what makes `LoadAsync` re-run on a workspace / catalog / tenant switch. It returns
  immediately when the token is already cancelled.

`PageViewBase<T>` deliberately does **not** override `OnRefreshChangeAsync` any more. In
preview.9 it did, and because the base is `async void` the `IsLoading` guard usually had not been
set yet — one provider change produced up to two backend loads.

### Translation

```csharp
public string        T(string content, params object[] args)          // plain string
public MarkupString  TM(string content, params object[] args)         // raw HTML, unencoded
public Translation   Translate(string content, params object[] args)  // the value object
```

`TM` bypasses Blazor encoding — only for content you control. See
`.zonit/extensions/cultures/cultures.md`.

### Breadcrumbs

```csharp
protected virtual bool? ShowBreadcrumbs { get; } = false;   // tri-state
protected virtual List<BreadcrumbsModel>? Breadcrumbs { get; }
```

| `ShowBreadcrumbs` | Effect in `OnInitialized` |
| --- | --- |
| `true` | `BreadcrumbsProvider.Initialize(Breadcrumbs)` |
| `false` (default) | nothing — the previous page's crumbs stay (this is what modals want) |
| `null` | `Initialize(null)` — clears the trail |

**Trap:** `Breadcrumbs` is read in `OnInitialized`, which runs *before* `OnInitializedAsync` and
therefore before `LoadAsync`. A crumb that names loaded data will be empty. Set those crumbs
imperatively after the load instead:

```csharp
protected override async Task<Product?> LoadAsync(CancellationToken cancellationToken)
{
    var product = await _products.GetAsync(Id, cancellationToken);

    BreadcrumbsProvider.Initialize(
    [
        new BreadcrumbsModel(T("Catalog"), "/catalog"),
        new BreadcrumbsModel(product.Name),
    ]);

    return product;
}
```

`OnRefreshChangeAsync` re-runs `OnInitializedAsync`, not `OnInitialized`, so the declarative
`Breadcrumbs` property is *not* re-applied on a workspace switch.

### Options

```csharp
[RequiresUnreferencedCode] [RequiresDynamicCode]
protected TModel Options<TModel>() where TModel : class, new()
```

Reads `IOptionsMonitor<TModel>.CurrentValue` and subscribes the component to `OnChange`. It is
one of exactly two members in this package that warn under a trim/AOT analyzer — see
`.zonit/extensions/website/aot.md`.

## `PageBase`

Adds one member: `protected string? LayoutKey`, mirroring `ILayoutContext`.

| Value | Meaning |
| --- | --- |
| `null` | render with no layout (runtime `[NoLayout]`) |
| `""` | fall back to the Site / router default, overriding a static `[LayoutKey]` |
| other | resolved through `ILayoutRegistry`; unknown keys log a warning and fall back |

Prefer the class attributes `[LayoutKey("…")]` / `[NoLayout]` — they are read before the page is
instantiated, so there is no flicker. Setting `LayoutKey` after first render costs one extra
render with the new chrome. Details in `.zonit/extensions/website/layouts.md`.

## `PageViewBase<T>` — read-only pages

```csharp
protected virtual TViewModel? Model { get; set; }
protected bool IsLoading { get; }                     // private setter
protected virtual bool PersistentModel { get; } = true;

protected virtual Task<TViewModel?> LoadAsync(CancellationToken cancellationToken);
protected Task RefreshAsync(CancellationToken cancellationToken = default);
```

```razor
@page "/orders"
@inherits PageViewBase<List<OrderRow>>
@using Zonit.Extensions

<h1>@T("Orders for {0}", Workspace.Organization.Name)</h1>

@if (IsLoading)
{
    <p>@T("Loading…")</p>
}
else if (Model is null || Model.Count == 0)
{
    <p>@T("No orders yet.")</p>
}
else
{
    <ul>
        @foreach (var row in Model)
        {
            <li><a href="@(new UrlPath($"/orders/{row.Id}").ToHref())">@row.Customer</a></li>
        }
    </ul>
}

@code {
    protected override bool? ShowBreadcrumbs => true;

    protected override List<BreadcrumbsModel>? Breadcrumbs =>
    [
        new(T("Home"), "/"),
        new(T("Orders")),
    ];

    protected override Task<List<OrderRow>?> LoadAsync(CancellationToken cancellationToken)
        => _orders.ListAsync(cancellationToken);
}
```

`UrlPath` lives in `Zonit.Extensions`, not `Zonit.Extensions.Website` — add
`@using Zonit.Extensions` (or put it in `_Imports.razor`). `ToHref()` is mandatory under a
non-root mount; see `.zonit/extensions/website/ui-services.md`.

**Model persistence.** With `PersistentModel == true` (the default) the model is written to
`PersistentComponentState` under the key `$"{GetType().Name}_Model"` during the prerender
persist phase and taken back on the interactive pass, so `LoadAsync` runs once per navigation
rather than twice.

- The key is the **component type name only**. Two pages with the same class name in different
  namespaces share a key.
- Both calls are gated on `JsonSerializer.IsReflectionEnabledByDefault`. Under
  `PublishTrimmed`/`PublishAot` persistence silently turns off (one `LogDebug`) and the page
  re-loads after hydration. See `.zonit/extensions/website/aot.md`.
- Set `PersistentModel => false` for volatile data you want re-fetched on every render pass.
  When it is `false`, `OnParametersSetAsync` loads whenever `Model` is `null`, so the page
  re-loads on parameter changes too.

`IsLoading` is guarded: a second load started while one is in flight returns immediately, so
`RefreshAsync` cannot stack loads.

## `PageEditBase<T>` — forms

`where TViewModel : class, new()`. `Model` is overridden as **non-nullable** and carries
`[SupplyParameterFromForm]`, so static-SSR form posts bind straight into it.

```csharp
protected EditContext? EditContext { get; }
protected ValidationMessageStore? ValidationMessages { get; }
protected bool Processing { get; set; }
protected bool HasChanges { get; }
public    bool IsValid { get; }

protected virtual bool AutoTrimStrings          => true;
protected virtual bool AutoNormalizeWhitespace  => true;
protected virtual bool TrackChanges             => true;
protected virtual bool PreventDuplicateSubmissions => true;
protected virtual TimeSpan AutoSaveDelay        => TimeSpan.FromMilliseconds(800);

protected virtual Task SubmitAsync(CancellationToken cancellationToken = default);
protected virtual void OnBeforeSubmit();
protected virtual void OnAfterSubmit(bool success);
protected virtual void HandleInvalidSubmit(string message);           // ← override this one
protected virtual Task OnModelChanged(string fieldName, object? oldValue, object? newValue, CancellationToken ct = default);
protected virtual Task AutoSaveAsync(string fieldName, object? oldValue, object? newValue, CancellationToken ct = default);
protected virtual Task HandleFieldAutoSaveError(string fieldName, Exception exception, CancellationToken ct = default);
protected virtual bool IsFieldAutoSaveEnabled(string fieldName);
protected EventCallback<TValue> OnValueChanged<TValue>(TValue modelValue);

public async Task HandleValidSubmit(EditContext editContext);         // ← callback, NOT virtual
public void HandleInvalidSubmit();                                    // ← callback, NOT virtual
public void ResetModel();
public void AddValidationMessage(string fieldName, string message);
public void ClearValidationMessages();
public void MarkAsChanged();
public void MarkAsUnchanged();
```

### The two `HandleInvalidSubmit`s

This is the single easiest thing to get wrong. There are two members with that name:

- `public void HandleInvalidSubmit()` — the **`EditForm` callback**. Not virtual. It walks
  `EditContext.GetValidationMessages()` and calls the other one per message.
- `protected virtual void HandleInvalidSubmit(string message)` — the **hook you override**.
  Default implementation is `Toast.AddError(message)`.

Same for the happy path: `HandleValidSubmit(EditContext)` is a non-virtual callback. To run your
own code, override `SubmitAsync` / `OnBeforeSubmit` / `OnAfterSubmit`.

### Wiring the form

```razor
@page "/profile"
@inherits PageEditBase<ProfileForm>

<EditForm EditContext="EditContext"
          FormName="profile"
          OnValidSubmit="HandleValidSubmit"
          OnInvalidSubmit="HandleInvalidSubmit">

    <label>@T("Name")</label>
    <InputText @bind-Value="Model.Name" />
    <ValidationMessage For="() => Model.Name" />

    <label>@T("Bio")</label>
    <InputTextArea @bind-Value="Model.Bio" />

    <button type="submit" disabled="@Processing">@T("Save")</button>
</EditForm>

@if (HasChanges)
{
    <p>@T("You have unsaved changes.")</p>
}

@code {
    protected override bool AutoNormalizeWhitespace => false;   // keep line breaks in Bio

    protected override Task<ProfileForm?> LoadAsync(CancellationToken cancellationToken)
        => _profiles.GetAsync(cancellationToken);

    protected override async Task SubmitAsync(CancellationToken cancellationToken)
    {
        await _profiles.SaveAsync(Model, cancellationToken);
        Toast.AddSuccess(T("Saved"));
    }
}
```

```csharp
public sealed class ProfileForm
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(64)]
    public string Name { get; set; } = "";

    [EmailAddress]
    public string Email { get; set; } = "";

    [AutoSave(1500)]                 // ms; parameterless [AutoSave] = 800 ms
    public string Bio { get; set; } = "";
}
```

- `FormName` is required for **static SSR** posts — `[SupplyParameterFromForm]` needs a name to
  match the POST against. Interactive render modes do not need it, but setting it is harmless.
- **Do not add `<DataAnnotationsValidator />`.** `PageEditBase` already validates DataAnnotations
  from `EditContext.OnValidationRequested` into its own `ValidationMessageStore`, translating
  each `ErrorMessage` through `ICultureProvider`. Adding the component registers a *second*
  store over the same attributes on the same `EditContext`: measured, `GetValidationMessages()`
  returns 2 messages instead of 1 for a single failing `[Required]`, and `HandleInvalidSubmit()`
  raises one toast per message — so every error is reported twice.
- `LoadAsync` must return a **non-null** instance. `PageEditBase` creates `new TViewModel()` and
  its `EditContext` in `OnInitialized`, before `LoadAsync` runs; the load then assigns whatever
  it returned, including `null`.

### The behaviour switches

| Property | Default | What it actually does |
| --- | --- | --- |
| `AutoTrimStrings` | `true` | `Trim()` on every writable `string` property, **only inside `HandleValidSubmit`, just before `SubmitAsync`**. Not on keystroke. |
| `AutoNormalizeWhitespace` | `true` | Same pass, applies `Regex(@"\s+") → " "`. |
| `TrackChanges` | `true` | Drives `HasChanges`; set on any field change, cleared after a successful submit. |
| `PreventDuplicateSubmissions` | `true` | Drops a submit landing within **1 s** of the previous one *finishing*. The threshold is a private `readonly` — not configurable. |
| `AutoSaveDelay` | 800 ms | Fallback debounce for fields whose `[AutoSave]` carries no explicit delay. |

**`AutoNormalizeWhitespace` destroys line breaks.** It is on by default and the regex is `\s+`,
so `"a\n\nb"` submits as `"a b"` — verified. Any page with an `InputTextArea` almost certainly
wants `protected override bool AutoNormalizeWhitespace => false;`.

### Auto-save

A field auto-saves only when `IsFieldAutoSaveEnabled(fieldName)` is `true`, which by default
means the property carries `[AutoSave]`. Setting `AutoSaveDelay` alone does nothing.

```csharp
protected override async Task AutoSaveAsync(
    string fieldName, object? oldValue, object? newValue, CancellationToken cancellationToken)
{
    await _profiles.PatchAsync(fieldName, newValue, cancellationToken);
    await InvokeAsync(StateHasChanged);          // ← see below
}
```

The debounce uses `System.Threading.Timer`, so **`AutoSaveAsync` runs on a thread-pool thread**,
outside the renderer's synchronisation context. Any UI touch from there (`StateHasChanged`, or a
`Toast.*` you expect to repaint) must go through `InvokeAsync`. Exceptions are routed to
`HandleFieldAutoSaveError`, which by default swallows them.

`OnModelChanged(field, old, new, ct)` fires for **every** field change regardless of `[AutoSave]`,
from the change handler itself.

### `IsValid` and `OnValueChanged` — two sharp edges

```csharp
public bool IsValid => EditContext?.GetValidationMessages().Any() is false;
```

It reads the *current* message store; it does not run validation. Before the first
`Validate()` it reports `true` for an invalid model, and it reports `false` when `EditContext`
is `null`.

`OnValueChanged<TValue>(TValue modelValue)` builds an `EventCallback` by scanning the model for a
property of type `TValue` **whose current value equals `modelValue`** and writing to the first
match. With two `string` properties currently holding the same text it will update the wrong
one. Prefer `@bind-Value="Model.Something"`.

## What the source generator supports

`Zonit.Extensions.Website.SourceGenerators` ships inside the NuGet package (`analyzers/dotnet/cs`)
and needs no wiring. For every `T` used as `PageViewBase<T>` / `PageEditBase<T>` in **your**
assembly it emits a `ViewModelMetadata<T>` with compile-time delegates plus a `[ModuleInitializer]`
that registers it. When present, `PageEditBase` uses those delegates instead of reflection.

It emits metadata only when the view model:

- is a **non-generic** named type — `PageViewBase<List<OrderRow>>` or `PagedResult<T>` gets
  nothing, `PageViewBase<OrderRow>` does;
- is `public` or `internal`, not `abstract`;
- has a public parameterless constructor;

and it only walks **properties declared on that exact type** that are public with a public setter.

| Shape | Result |
| --- | --- |
| plain class, `{ get; set; }` | works |
| `[AutoSave]` / `[AutoSave(ms)]` | captured into the metadata |
| generic view model (`List<T>`, `PagedResult<T>`) | no metadata; reflective fallback |
| **`{ get; init; }` (class *or* record)** | **`error CS8852` in the consumer's build** |
| **`required` member** | **`error CS9035` in the consumer's build** |
| **property inherited from a base class** | **compiles, silently missing from the metadata** |
| positional record `record Vm(string X)` | no parameterless ctor → skipped (and rejected by `PageEditBase`'s `new()` constraint anyway) |

Use plain `{ get; set; }` classes for view models. Everything else is either a build break you did
not write or a silent gap.

Note that analyzers do **not** flow through `ProjectReference`. If you consume the library by
project reference rather than by package, add the generator yourself:

```xml
<ProjectReference Include="…\Zonit.Extensions.Website.SourceGenerators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

Without it everything still works — `PageEditBase` falls back to reflection.

## Known limitations

- **The generator has no diagnostics.** The `CS8852` / `CS9035` rows above are raw compiler errors
  inside `ZonitViewModelMetadata.g.cs`, in code you did not write, with no opt-out. All three
  generator rows were reproduced against 10.0.0-preview.10.
- **Dropped inherited properties are silent.** They are not trimmed by `AutoTrimStrings`, not seen
  by `[AutoSave]`, and not readable by the field-value lookup — but the build is green. The
  reflective fallback handled them, so this only appears once the generator is present. Flatten the
  view model, or keep the fields on the leaf type.
- **`PageViewBase` model persistence is off under `PublishTrimmed` / `PublishAot`.** Not just AOT
  — the SDK clears `JsonSerializer.IsReflectionEnabledByDefault` for any trimmed publish. The page
  still works; it just re-runs `LoadAsync` after hydration. Full detail in
  `.zonit/extensions/website/aot.md` and `.zonit/extensions/website/hydration.md`.
- **The generated file uses C# `file` types**, so a consumer pinning `LangVersion` ≤ 10 gets
  `CS8936`. `net10.0` defaults are fine.
- **`ExtensionsBase.Options<T>()` is annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`**
  and will warn at your call site under a trim/AOT analyzer.

## See also

- `.zonit/extensions/website/hosting.md` — `AddWebsite` / `UseWebsite<TApp>` and mount ordering
- `.zonit/extensions/website/layouts.md` — `[LayoutKey]`, `[NoLayout]`, `ZonitRouteView`
- `.zonit/extensions/website/ui-services.md` — toasts, cookies, navigation, `UrlPath.ToHref()`
- `.zonit/extensions/website/permissions.md` — `[RequirePermission]` on a page
- `.zonit/extensions/website/hydration.md` — why the SSR and circuit scopes differ
