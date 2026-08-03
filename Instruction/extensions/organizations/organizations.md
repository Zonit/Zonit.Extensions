# Workspaces and organizations (`Zonit.Extensions.Organizations`)

One question per scope: **which organization is the current user working in?** The package is
framework-agnostic — no `HttpContext`, no middleware, no Blazor types. It declares three contracts and a
per-scope state machine; the ASP.NET Core plumbing lives in `Zonit.Extensions.Website`.

```
YOUR CODE                     FRAMEWORK (internal)              YOUR UI
IOrganizationSource   ──►   WorkspaceRepository        ──►   IWorkspaceProvider.Organization
(scoped, you write)         (= IWorkspaceManager)            (Organization value object)
                                   ▲
                            WorkspaceMiddleware      (Website — once per HTTP request)
                            WorkspaceStateBridge     (Website — prerender → circuit)
```

## Read this first

- **A Website host must not call `AddOrganizationsExtension()` and must not add middleware.**
  `AddWebsite()` already calls it, and `UseWebsite<TApp>()` installs `WorkspaceMiddleware` — which is
  `internal`, so `app.UseMiddleware<WorkspaceMiddleware>()` does not even compile.
- **Registration order decides whether your source is used at all.** `AddOrganizationsExtension()`
  registers `NullOrganizationSource` through `TryAddScoped`. Register yours with `AddScoped`, never
  `TryAddScoped`, or you sit on the null source forever: empty workspace, empty list, every switch denied,
  no exception, no log line.
- **`InitializeAsync` and `GetOrganizationsAsync` run concurrently on the same scoped instance.** Sharing
  one `DbContext` between them is a bug.
- **`Organization` is a `readonly struct`.** `!= null` compiles and is always true. Test `HasValue`.
- **Everything on `OrganizationModel` is serialized into the prerendered HTML** by the Website hydration
  bridge — including `TaxIdentification`, `Email` and the postal address, for every organization the user
  can switch into.
- **`SwitchOrganizationAsync` returns `Task<bool>` as of 10.0.0-preview.10** (it returned `Task`). `false`
  means denied, and denied is now a no-op — it used to wipe the current workspace.

## What `AddOrganizationsExtension()` registers

| Service | Implementation | Lifetime | How |
| --- | --- | --- | --- |
| `IWorkspaceManager` | `WorkspaceRepository` (internal) | Scoped | `TryAddScoped` |
| `IWorkspaceProvider` | `WorkspaceService` (internal) | Scoped | `TryAddScoped` |
| `IOrganizationSource` | `NullOrganizationSource` (internal) | Scoped | `TryAddScoped` |

That is the whole method. No middleware, no hosted service, no cache, no assembly scanning for your
source. Everything is scoped: hydrate and read in the *same* scope, or you read an empty workspace.

```csharp
// ── Website host ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IOrganizationSource, AcmeOrganizationSource>();
builder.Services.AddWebsite();      // registers Organizations + Auth + Cultures + Projects + Tenants

// ── Console / worker host ───────────────────────────────────────────────────
services.AddScoped<IOrganizationSource, AcmeOrganizationSource>();
services.AddOrganizationsExtension();
```

```csharp
// ── The silent failure ──────────────────────────────────────────────────────
services.AddOrganizationsExtension();                                 // NullOrganizationSource lands here
services.TryAddScoped<IOrganizationSource, AcmeOrganizationSource>(); // no-op: the null source wins
```

Order between `AddScoped` and `AddWebsite()` does not matter — `TryAdd*` skips when your descriptor is
already there, and a later `AddScoped` wins the last-registration-wins resolve. Only `TryAddScoped`
(or `TryAddEnumerable`) for your own source is broken.

Namespaces: `AddOrganizationsExtension()` and the `Organization` value object are in `Zonit.Extensions`;
`IOrganizationSource`, `IWorkspaceManager`, `IWorkspaceProvider`, `WorkspaceModel`, `OrganizationModel` and
`StateModel` are in `Zonit.Extensions.Organizations`.

## Implementing `IOrganizationSource`

This is the only type you implement. It is called on the request/circuit scope, so it may inject scoped
services (your `IAuthenticatedProvider` from `Zonit.Extensions.Auth`, a `DbContext` factory, an HTTP client).

```csharp
public interface IOrganizationSource
{
    Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceModel?> SwitchOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
```

### Null semantics, per method

| Method | Returns | `null` means | If you get it wrong |
| --- | --- | --- | --- |
| `InitializeAsync` | `WorkspaceModel` (non-nullable) | — return `new WorkspaceModel()` for "nothing selected" | Returning `null!` degrades to `Organization.Empty` instead of throwing, so the bug is invisible |
| `GetOrganizationsAsync` | `IReadOnlyCollection<OrganizationModel>?` | "not known" — `IWorkspaceManager.Organizations` stays `null` | Every `SwitchOrganizationAsync` then re-calls this method to back-fill the list. Return `[]` for "member of nothing" |
| `SwitchOrganizationAsync` | `WorkspaceModel?` | **denied** — the manager returns `false` and changes nothing | Returning a workspace for an organization the user cannot access grants the switch; this method *is* the authorization check |

`WorkspaceModel.Organization` may be `null` — that is the honest encoding of "authenticated, but no
organization selected". `IWorkspaceProvider.Organization` reads `Organization.Empty` for that case.

### The two methods run at the same time

`WorkspaceRepository.InitializeAsync` starts both calls and then awaits `Task.WhenAll` — deliberately, to
halve cold-load latency. They overlap on **one scoped instance of your class**:

```csharp
var workspaceTask     = _userWorkspace.InitializeAsync(cancellationToken);
var organizationsTask = _userWorkspace.GetOrganizationsAsync(cancellationToken);
await Task.WhenAll(workspaceTask, organizationsTask);
```

A single injected `DbContext` therefore breaks the rule EF Core enforces — one operation at a time per
context instance, otherwise `InvalidOperationException: A second operation was started on this context
instance before a previous operation completed`. Inject `IDbContextFactory<T>` and create one context per
method (same for any other client that is not safe for concurrent use). `SwitchOrganizationAsync` never
runs in parallel with the other two, but it shares the instance, so keep the class stateless either way.

### Data invariants the mapper needs

`OrganizationModel` is your DTO, unvalidated; `IWorkspaceProvider` projects it into the strict `Organization`
value object. The projection is total — it never throws — which means every violation is silent data loss:

| Field | Rule | What happens otherwise |
| --- | --- | --- |
| `Id` | non-empty `Guid` | `Guid.Empty` → `Organization.Empty`: the UI reads "no organization selected" and `SwitchOrganizationAsync` can never address the row |
| `Name` | non-blank, ≤ 60 graphemes (`Title.MaxLength`) | blank → `Title.Empty` (an unlabelled entry in the switcher); longer → whitespace-normalized and cut at the 60th grapheme |
| everything else | free-form | carried verbatim, and only reachable through `IWorkspaceManager` |

Keep the legal entity name in `FullName` and a short display label in `Name`.

### It all ends up in the page source

In a Website host the prerender → circuit bridge persists the whole `StateModel` (active `WorkspaceModel`
plus every `OrganizationModel` from `GetOrganizationsAsync`) as JSON inside the response HTML, under the
key `ZonitOrganizationsExtension`. It is not encrypted. Populate `Email`, `TaxIdentification`, `Country`,
`City`, `PostalCode`, `AddressLine1/2` only when the page actually shows them — otherwise every page load
ships your customers' invoice data to the browser. Details in `.zonit/extensions/website/hydration.md`.

### Reference implementation

```csharp
internal sealed class AcmeOrganizationSource(
    IDbContextFactory<AcmeDbContext> factory,
    IAuthenticatedProvider auth) : IOrganizationSource
{
    public async Task<WorkspaceModel> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var user = auth.Current;
        if (!user.HasValue)
            return new WorkspaceModel();                 // anonymous — nothing selected

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var row = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == user.Id && m.IsCurrent)
            .Select(m => new { m.Organization, m.Permissions, m.Roles })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? new WorkspaceModel()                       // member of nothing — still not null
            : new WorkspaceModel
            {
                Organization = Map(row.Organization),
                Permissions = row.Permissions,
                Roles = row.Roles,
            };
    }

    public async Task<IReadOnlyCollection<OrganizationModel>?> GetOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        var user = auth.Current;
        if (!user.HasValue)
            return [];                                   // empty, not null

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        // Inline projection, not a call to Map(...) — EF cannot translate a method call here.
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => new OrganizationModel
            {
                Id = m.Organization.Id,
                Name = m.Organization.DisplayName,
                FullName = m.Organization.LegalName,
                CreatedDate = m.Organization.CreatedUtc,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceModel?> SwitchOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var user = auth.Current;
        if (!user.HasValue || organizationId == Guid.Empty)
            return null;                                 // null == denied

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var membership = await db.Memberships
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(
                m => m.UserId == user.Id && m.Organization.Id == organizationId,
                cancellationToken);

        if (membership is null)
            return null;                                 // not a member — denied

        await db.Memberships
            .Where(m => m.UserId == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsCurrent, m => m.Id == membership.Id),
                cancellationToken);

        return new WorkspaceModel
        {
            Organization = Map(membership.Organization),
            Permissions = membership.Permissions,
            Roles = membership.Roles,
        };
    }

    private static OrganizationModel Map(OrganizationRow o) => new()
    {
        Id = o.Id,                                       // never Guid.Empty
        Name = o.DisplayName,                            // non-blank, <= 60 graphemes
        FullName = o.LegalName,
        CreatedDate = o.CreatedUtc,
    };
}
```

On the middleware path the `CancellationToken` is `HttpContext.RequestAborted`, so an abandoned request
stops your database work — honour it, do not swallow it.

## Reading the workspace

`IWorkspaceProvider` has exactly two members: `Organization Organization { get; }` and `event Action? OnChange`.

```razor
@inject IWorkspaceProvider Workspace

@if (Workspace.Organization.HasValue)
{
    <p>@Workspace.Organization.Name — @Workspace.Organization.Id</p>
}
```

Inside a page deriving from `PageBase` / `PageViewBase<T>` the provider is already there as the protected
`Workspace` property — do not `@inject` it a second time. It is a `Lazy<T>`: the `OnChange` subscription
that re-runs `OnInitializedAsync` after an organization switch is only wired **the first time your page
touches `Workspace`**. A page that never reads it will not reload its data when the user switches.

```csharp
var organization = workspace.Organization;
if (!organization.HasValue)          // readonly struct — never test != null
    return query.Where(_ => false);

Guid id = organization;              // implicit Organization -> Guid
return query.Where(m => m.Organization.Id == id);
```

- `Organization.Name` is a `Title`, possibly truncated or empty (see the invariants table). For the
  verbatim text read `IWorkspaceManager.Workspace?.Organization?.Name` / `.FullName`.
- `Organization.Slug` is **always** `UrlSlug.Empty` here — the mapper never populates it.
- Outside a hydrated scope (anonymous request, a scope you created yourself and never initialized) the
  value is `Organization.Empty`. That is not an error state; branch on `HasValue`.

## Switching organization

`IWorkspaceManager` is the write surface — seven members, no more:

| Member | Notes |
| --- | --- |
| `void Initialize(StateModel model)` | seeds the scope from a snapshot; raises `OnChange` |
| `Task<StateModel> InitializeAsync(CancellationToken)` | calls the source (both read methods in parallel); raises `OnChange` |
| `Task<bool> SwitchOrganizationAsync(Guid, CancellationToken)` | `false` = denied, nothing changed |
| `WorkspaceModel? Workspace` | `null` until the scope is hydrated |
| `IReadOnlyCollection<OrganizationModel>? Organizations` | switchable list, `null` until hydrated |
| `StateModel? State` | both of the above; `null` means "this scope never hydrated" |
| `event Action? OnChange` | what `IWorkspaceProvider` and `ExtensionsBase` components listen to |

```csharp
if (!await manager.SwitchOrganizationAsync(organizationId, cancellationToken))
{
    // Denied: the source answered null. Nothing changed — the previously
    // selected organization is still active and no OnChange was raised.
    return "You do not have access to that organization.";
}

return $"Switched to {manager.Workspace?.Organization?.Name}.";
```

| Outcome | State write | `OnChange` | Return |
| --- | --- | --- | --- |
| source returns a `WorkspaceModel` | workspace replaced; org list back-filled if the scope had none | raised | `true` |
| source returns `null` | none — previous organization kept | not raised | `false` |

A switch on a scope that was never hydrated (no middleware pass, a circuit whose bridge did not restore)
is no longer a silent no-op: the source is called, the snapshot is materialized, and the visible list is
fetched. Your source must therefore tolerate `SwitchOrganizationAsync` arriving without a preceding
`InitializeAsync` on the same scope.

`manager.Organizations` is the switcher's data source — `IReadOnlyCollection<OrganizationModel>?`, `null`
until something hydrated it:

```csharp
foreach (var org in manager.Organizations ?? [])
    yield return (org.Id, org.Name);
```

## Hosts without middleware (console, worker, tests)

`WorkspaceMiddleware` only exists in a Website host, and it hydrates only when the request is
authenticated and `State is null`. Everywhere else you drive the manager yourself, once per scope,
before anything reads the provider. `IWorkspaceManager` is **scoped**, so a singleton `BackgroundService`
must create a scope:

```csharp
internal sealed class OrganizationReportJob(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var manager = scope.ServiceProvider.GetRequiredService<IWorkspaceManager>();
        await manager.InitializeAsync(stoppingToken);

        foreach (var org in manager.Organizations ?? [])
            Console.WriteLine(org.Name);
    }
}
```

Resolution order inside the scope does not matter: `IWorkspaceProvider` hydrates in its constructor and
again on every `IWorkspaceManager.OnChange`, which both `Initialize` and `InitializeAsync` raise.

`Initialize(StateModel)` is the synchronous seam for state you already have — it is how the Website
prerender bridge re-seeds a circuit, and how a test fixture pins a workspace without a source:

```csharp
// StateModel is ambiguous when Zonit.Extensions.Projects is also imported — qualify it.
manager.Initialize(new Zonit.Extensions.Organizations.StateModel
{
    Workspace = new WorkspaceModel { Organization = new OrganizationModel { Id = id, Name = "Acme Retail" } },
    Organizations = [new OrganizationModel { Id = id, Name = "Acme Retail" }],
});
```

`Zonit.Extensions.Organizations.StateModel` and `Zonit.Extensions.Projects.StateModel` both exist, so a
file importing both namespaces — the normal shape for an app that uses workspaces *and* catalogs — fails
with `CS0104: 'StateModel' is an ambiguous reference`. Qualify it as above or alias it
(`using OrgState = Zonit.Extensions.Organizations.StateModel;`). The same applies in
`.zonit/extensions/projects/projects.md`.

## Authorization is not here

`WorkspaceModel.Permissions` and `WorkspaceModel.Roles` are carried across the wire but **nothing in the
framework reads them** — no policy, no `[RequirePermission]`, no `AuthorizeView` consults them. Authorization
runs off the `Identity` your `IAuthSource` returns (see `.zonit/extensions/auth/auth.md`). If permissions
differ per organization, re-issue the identity when the workspace changes; filling in these two arrays
alone changes nothing.

## Removed in 10.0.0-preview.10

| Removed | Replacement |
| --- | --- |
| `IOrganizationProvider` (`GetOrganizationAsync`, `GetUserOrganizationAsync`, `GetUserOrganizationsAsync`) | none — those lookups belong in your own data layer; the framework only needs `IOrganizationSource` |
| `IOrganizationManager.GetAsync(Guid)` | none — same |
| `IOrganizationEntity` | none — it was an unused marker; put `Guid OrganizationId` on your entity and filter with `IWorkspaceProvider.Organization` |
| `Zonit.Extensions.Organizations.Entities.Organization` | the `Zonit.Extensions.Organization` value object, or your own entity |
| `<ZonitOrganizationsExtension />` component | `<WebsiteHydrator />` from `Zonit.Extensions.Website`, placed once in `App.razor` |
| `OrganizationsMiddleware` | never existed as a public type; `UseWebsite<TApp>()` installs the real one |
| `IsPermission(...)` / `IsRole(...)` on the provider | `Zonit.Extensions.Auth` |

Also changed: `IWorkspaceManager.SwitchOrganizationAsync` is `Task<bool>` (binary-breaking — recompile), and
a denied switch no longer clears the workspace.

## Known limitations

- **Hydration is silently disabled under trimming.** `WorkspaceStateBridge` gates both `Restore` and the
  persist callback on `JsonSerializer.IsReflectionEnabledByDefault`, which the SDK turns off for *any*
  `PublishTrimmed` publish (not just `PublishAot`). In such a build the workspace does not survive the
  prerender → circuit boundary, nothing is logged, and the circuit starts with
  `IWorkspaceProvider.Organization == Organization.Empty` until something re-initializes the manager. This
  package itself is trim/AOT clean; the bridge in `Zonit.Extensions.Website` is the affected part.
- **No caching, no de-duplication.** `WorkspaceRepository` holds the snapshot for the scope and nothing
  more; every new request scope calls your source again. Cache inside your `IOrganizationSource` if the
  round-trip is expensive.
- **`Permissions` / `Roles` on `WorkspaceModel` are inert**, and `Organization.Slug` is never populated by
  this pipeline.

Related: `.zonit/extensions/projects/projects.md` (the identical `IProjectSource` → `ICatalogProvider`
pipeline, one level below organizations), `.zonit/extensions/website/hosting.md` (what `AddWebsite()` /
`UseWebsite()` wire), `.zonit/extensions/core/value-objects.md` (`Organization`, `Title`, `HasValue`).
