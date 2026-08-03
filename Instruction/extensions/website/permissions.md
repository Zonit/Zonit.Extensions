# Authorization: permissions, roles and the Session scheme

Everything here is registered by `AddWebsite`. You do **not** register policies, do **not** add
authorization handlers, and do **not** call `AddAuthorization` again.

The two attributes live in `Zonit.Extensions.Website.Authentication`.

## [RequirePermission]

```csharp
using Zonit.Extensions.Website.Authentication;

[RequirePermission("orders.read")]
public partial class OrdersPage { }
```

```razor
@page "/orders"
@using Zonit.Extensions.Website.Authentication
@attribute [RequirePermission("orders.read")]
```

```csharp
// Minimal API — the attribute is its own requirement, so pass the instance:
app.MapGet("/orders", () => Results.Ok())
   .RequireAuthorization(new RequirePermissionAttribute("orders.read"));
```

`RequirePermissionAttribute` derives from `AuthorizeAttribute` and implements both
`IAuthorizationRequirement` and `IAuthorizationRequirementData` (.NET 8+). The attribute *is* the
requirement, so there is nothing to register. It is `AllowMultiple = true` and `Inherited = true`;
multiple attributes are ANDed.

The token goes through the `Permission` value object at construction time, which means it is
validated and normalised eagerly:

```
new RequirePermissionAttribute("Orders.READ").Token   // "orders.read"
new RequirePermissionAttribute("Orders.READ").Policy  // "zonit:permission:orders.read"
new RequirePermissionAttribute("Not A Token")         // ArgumentException: Permission must
                                                      // consist of dot-separated tokens of
                                                      // [a-z0-9_-*] characters
```

Wildcards are evaluated by `Permission.Implies`. **A `*` matches exactly one token**, except that
a trailing `*` may also match zero — it is *not* the "matches any remaining depth" wildcard people
expect from AWS IAM, and the XML doc on `Permission.Implies` overstates it as "zero or more
sub-tokens". Measured:

| Granted | Required | Result |
|---|---|---|
| `admin.*` | `admin` | granted (trailing `*` matches zero) |
| `admin.*` | `admin.settings` | granted |
| `admin.*` | `admin.settings.write` | **denied** — one `*`, two tokens |
| `admin.*.*` | `admin.settings.write` | granted |
| `admin` | `admin.settings` | denied |
| `*` | `anything` | granted |
| `*` | `anything.at.all` | **denied** |
| `admin.*` | `billing.read` | denied |

Size the grant to the deepest token you actually check, or grant several tokens.

**Put wildcards in the grant, never in the requirement.** The handler asks
`granted.Implies(required)`, so a requirement of `admin.*` is satisfied only by a user who
literally holds `admin.*` (or `admin.*.*`). `admin`, `admin.users` and even `*` are all denied
against it. `[RequirePermission("admin.*")]` and `SiteOptions.Permission = "admin.*"` are
therefore almost never what you meant — use a concrete token such as `"admin.access"`.

Multiple `[RequirePermission]` attributes on one type are ANDed: two attributes produce two
requirements and the user must satisfy both.

An unauthenticated principal always fails — the handler returns without succeeding when
`User.Identity.IsAuthenticated` is false.

## The policy provider, and the trap in it

`AddWebsite` installs `PermissionPolicyProvider` with `services.Replace(...)`, so it is the app's
one and only `IAuthorizationPolicyProvider`. `GetPolicyAsync(name)` tries, in order:

1. `name` starts with `zonit:permission:` → synthesise a permission policy for the suffix.
2. `Permission.TryCreate(name)` succeeds → synthesise a permission policy for `name`.
3. otherwise → defer to `DefaultAuthorizationPolicyProvider` (i.e. `AddPolicy(...)` registrations).

Step 2 is what makes bare tokens usable as policy names everywhere ASP.NET takes a policy string:

```razor
<AuthorizeView Policy="orders.read">
    <Authorized><OrdersTable /></Authorized>
    <NotAuthorized><p>No access.</p></NotAuthorized>
</AuthorizeView>
```

```csharp
app.UseWebsite<App>("/admin", o => o.Permission = "admin.access");  // SiteOptions.Permission
```

**The trap:** step 2 runs *before* step 3, and `Permission.TryCreate` trims, lowercases and then
matches `^([a-z0-9_-]+|\*)(\.([a-z0-9_-]+|\*))*$`. So an ordinary custom policy name is silently
swallowed:

```csharp
services.AddAuthorization(o => o.AddPolicy("Over18", p => p.RequireClaim("age", "18")));
// [Authorize(Policy = "Over18")] now evaluates a permission check for the token "over18".
// The registered policy is never consulted; a user with age=18 is DENIED.
```

Any policy name made only of letters, digits, `_`, `-`, `.` and `*` is shadowed — `Over18`,
`AdminOnly`, `can-edit`, `tier.gold`. Names containing a character outside that set fall through
correctly (`"Admin Only"` works, because of the space). If you need custom policies alongside
Zonit permissions, give them a name the permission grammar rejects — a space, a colon or a slash
is enough — or express the rule as a permission token instead.

## [RequireRole] — do not use it as an attribute

```csharp
// BROKEN as an attribute. Throws at request/render time:
[RequireRole("admin")]
public partial class AdminPage { }
```

```
InvalidOperationException: The AuthorizationPolicy named: 'zonit:role:admin' was not found.
```

`RequireRoleAttribute` sets `Policy = "zonit:role:{token}"`, and when ASP.NET combines endpoint /
component authorization metadata it resolves that `Policy` name through the policy provider.
`PermissionPolicyProvider` only understands the `zonit:permission:` prefix; `zonit:role:admin`
contains colons, so it fails the permission grammar too and the default provider has no such
policy registered. The `[RequirePermission]` twin does not have this problem because its prefix
*is* handled.

What does work:

```csharp
// 1. Blazor / endpoints — the built-in role check reads the same ClaimTypes.Role claims.
[Authorize(Roles = "admin")]
public partial class AdminPage { }
```

```csharp
// 2. The requirement object passed directly to IAuthorizationService.
var result = await authorization.AuthorizeAsync(
    user, resource: null, new[] { new RequireRoleAttribute("admin") });
```

The `RoleAuthorizationHandler` behind `RequireRoleAttribute` is registered and correct — only the
attribute's policy-name round trip is broken. `Role` tokens are validated and lowercased at
construction (`^[a-z0-9][a-z0-9_\-]*$`, max 64), so `new RequireRoleAttribute("bad role!")` throws
`ArgumentException`.

## The claim contract

`IdentityClaimsBuilder` (static, `Zonit.Extensions.Website.Authentication`) is the single place
that converts between the `Identity` value object and a `ClaimsPrincipal`. Every part of the stack
goes through it — the cookie handler, the Blazor `AuthenticationStateProvider` and
`SessionMiddleware` — so the shapes cannot drift.

| Constant | Value |
|---|---|
| `IdentityClaimsBuilder.AuthenticationType` | `"Zonit"` |
| `IdentityClaimsBuilder.PermissionClaimType` | `"zonit:permission"` |

`Build(Identity)` emits:

| Claim type | Source | Notes |
|---|---|---|
| `ClaimTypes.NameIdentifier` | `Identity.Id` | always, as a Guid string |
| `ClaimTypes.Name` | `Identity.Name` | only when the `Title` has a value |
| `ClaimTypes.Role` | one per `Identity.Roles` | so `User.IsInRole("admin")` and `[Authorize(Roles=…)]` work |
| `zonit:permission` | one per `Identity.Permissions` | wildcards preserved; `Implies` does the matching |

An `Identity` without a value produces an empty `ClaimsIdentity` (unauthenticated).

`Read(ClaimsPrincipal?)` is the exact inverse and is deliberately forgiving:

```csharp
Identity identity = IdentityClaimsBuilder.Read(httpContext.User);
```

- not authenticated → `Identity.Empty`
- `NameIdentifier` missing, unparseable, or `Guid.Empty` → `Identity.Empty`
- a malformed individual role/permission claim is skipped, not thrown on
- claims are collected across **all** inner identities of the principal, so a second scheme
  (OIDC, cookie) contributes too; on a conflict the first authenticated identity wins

**`Build` and `Read` must stay in sync.** If you add a claim to one, add it to the other — nothing
enforces the pairing, and the failure mode is state that survives SSR and silently disappears
after hydration.

## The Session cookie scheme

Unless the host already has authentication, `AddWebsite` registers:

- scheme name `"Zonit"` (`AuthExtensions.SchemeName`), set as both `DefaultAuthenticateScheme` and `DefaultChallengeScheme`
- handler `AuthenticationSchemeService`, which reads the cookie named `"Session"`
  (`AuthExtensions.SessionCookieName`), calls `IAuthSource.GetByTokenAsync(value, ct)` and builds
  the principal through `IdentityClaimsBuilder`

Behaviour: no cookie → `AuthenticateResult.NoResult()`; cookie present but the source returns no
identity → `AuthenticateResult.Fail("Unauthorized")`.

The rest of the flow per request:

1. `UseAuthentication()` runs the scheme once and caches the result on the request.
2. `SessionMiddleware` projects `HttpContext.User` back into the scoped `IAuthenticatedRepository`
   via `IdentityClaimsBuilder.Read` — **no second database round trip**, and only when the
   repository is still empty for that scope.
3. `SessionAuthenticationService` (the Blazor `AuthenticationStateProvider`) builds the cascading
   `AuthenticationState` from `IAuthenticatedProvider.Current` and re-raises it on
   `IAuthenticatedProvider.OnChange`, so a sign-in inside a live circuit updates `<AuthorizeView>`
   without a page reload.
4. `AuthStateBridge` carries the identity across the prerender → circuit boundary. See
   `.zonit/extensions/website/hydration.md`.

Static assets, `/_framework/`, `/_content/` and `/lib/` skip steps 2–3 entirely.

You supply `IAuthSource` — it is the consumer contract. See `.zonit/extensions/auth/auth.md`.

### The AddAuthentication-before-AddWebsite trap

`AddWebsite` registers the Zonit scheme only when nothing else has registered an
`IAuthenticationSchemeProvider` yet:

```csharp
// BROKEN — the "Zonit" scheme is never registered, the Session cookie is never read,
// and every request is anonymous no matter what IAuthSource returns.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddWebsite(o => o.AddArea<ShopArea>());
```

Observed result: schemes `[Cookies]`, no `Zonit`.

```csharp
// CORRECT — AddWebsite first; extra schemes stack on top and the Zonit defaults stay.
builder.Services.AddWebsite(o => o.AddArea<ShopArea>());
builder.Services.AddAuthentication().AddCookie("External");
```

Observed result: schemes `[Zonit, External]`, `DefaultAuthenticateScheme = Zonit`.

If you genuinely want a different default scheme, call `AddWebsite` first and then
`services.Configure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = "…")`, so the Zonit
handler is still registered and available.

## Gating whole mounts

`SiteOptions.Permission` applies `RequireAuthorization(value)` to the Site's Razor Components
endpoints:

```csharp
app.UseWebsite<App>("/admin", o =>
{
    o.Permission = "admin.access";  // resolved by PermissionPolicyProvider
    o.AddArea<AdminArea>();
});
```

This covers the routable pages and the `/_blazor` hub endpoints of that mount. It does **not**
cover minimal-API endpoints added from `IWebsiteArea.MapEndpoints` / `SiteOptions.MapEndpoints`,
nor the static-assets endpoint — verified by inspecting the built endpoint metadata. Guard those
explicitly:

```csharp
public void MapEndpoints(IEndpointRouteBuilder endpoints)
    => endpoints.MapPost("/orders/import", () => Results.Ok())
                .RequireAuthorization("orders.write");
```

## Known limitations

- **`[RequireRole]` throws when used as an attribute** (`zonit:role:` policy names are not
  resolvable). Use `[Authorize(Roles = "…")]`, or pass the requirement object to
  `IAuthorizationService` directly.
- **`PermissionPolicyProvider` shadows custom policy names** that happen to match the permission
  grammar, including ones registered through `AddAuthorization(o => o.AddPolicy(...))`. The
  registered policy is never evaluated and there is no warning.
- **`SiteOptions.Permission` does not reach area/site minimal-API endpoints.**
- **Identity does not survive the prerender→circuit boundary under `PublishTrimmed`.**
  `AuthStateBridge` is gated on `JsonSerializer.IsReflectionEnabledByDefault`, which the SDK turns
  off for any trimmed publish — not only `PublishAot` — and it logs nothing when it skips. The
  interactive render then starts anonymous. See `.zonit/extensions/website/hydration.md`.
- **`RequirePermissionAttribute`'s XML doc names `FormatException`** for an invalid token; the
  constructor actually throws `ArgumentException` via `Permission.Create`.
