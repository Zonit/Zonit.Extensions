# Auth — the identity contract

`Zonit.Extensions.Auth` is a **framework-agnostic core**. Its whole job is to define one contract you
implement (`IAuthSource`) and to carry the resulting `Identity` value object through one unit of work
(HTTP request, Blazor circuit, background job). It has no ASP.NET Core reference at all.

Everything web — the `"Zonit"` cookie scheme, `SessionMiddleware`, `[RequirePermission]`, the Blazor
`AuthenticationStateProvider` — lives in **`Zonit.Extensions.Website`** and is wired by `AddWebsite()` /
`UseWebsite<TApp>()`. See `.zonit/extensions/website/permissions.md` and
`.zonit/extensions/website/hosting.md`.

## Read this before writing any auth code

| Trap | Reality |
|---|---|
| "I need to call `AddAuthExtension()` in my web app" | `AddWebsite()` already calls it. Calling it again is harmless (all `TryAdd`) but signals a misunderstanding. |
| "If I forget to register `IAuthSource` it will fail at startup" | It will not. `AddAuthExtension()` `TryAdd`s an internal `NullAuthSource` that answers `Identity.Empty` / `null` to everything. The app boots and every request is anonymous. **No exception, no warning, no log.** |
| "Registering `IAuthSource` also covers `IUserDirectory`" | It does not — they are two separate DI keys. Register your class under both or `IUserDirectory` silently stays `NullAuthSource`. |
| "I'll use `TryAddScoped<IAuthSource, MyAuthSource>()` to be safe" | Verified: after `AddWebsite()` / `AddAuthExtension()` this is a **silent no-op** and you stay anonymous. Use `AddScoped` (last registration wins), or register before. |
| `if (auth.Current != null)` | Compiles and is **always true**. `Identity` is a `readonly struct`. Test `auth.Current.HasValue` or `auth.IsAuthenticated`. |
| "`HasPermission` with an empty token is safe" | `me.HasPermission("")` returns **`true`** — an empty `Permission` is universally allowed. Never feed it an unvalidated string. |
| "I'll build the VOs straight from my DB strings" | `new Permission(x)` / `new Role(x)` / `new Title(x)` **throw** `ArgumentException` on malformed or over-long input, and the implicit `string` → VO conversions throw too. One bad row 500s the request. Use `TryCreate`. |
| `app.UseAuthExtension()` | **Does not exist.** There is no such method anywhere in the repo. The pipeline comes from `app.UseWebsite<TApp>(...)`. |
| `HttpContext.SignInAsync("Zonit", principal)` | **Throws.** See "Sign-in and sign-out" below. |
| `IHostedService` taking `IAuthenticatedProvider` | Fails at startup — both the provider and the repository are **scoped**. Verified: `Cannot resolve scoped service 'Zonit.Extensions.Auth.IAuthenticatedProvider' from root provider.` Create a scope inside the service. |

## What `AddAuthExtension()` actually registers

Exactly four services, all `TryAdd`, all scoped. Verified by dumping the `IServiceCollection`:

| Service | Implementation | Role |
|---|---|---|
| `IAuthenticatedRepository` | `AuthenticatedRepository` (internal) | write side — `Initialize(identity)` |
| `IAuthenticatedProvider` | `AuthenticatedService` (internal) | read side — `Current`, `IsAuthenticated`, `OnChange` |
| `IAuthSource` | `NullAuthSource` (internal) | fallback: every token → `Identity.Empty` |
| `IUserDirectory` | `NullAuthSource` (internal) | fallback: every lookup → `null` |

It registers **no** authentication scheme, **no** middleware, **no** `IAuthorizationHandler`, **no**
`IAuthorizationPolicyProvider`, and nothing Blazor. There is no `UseAuthExtension()` counterpart.

`Zonit.Extensions.Website.AddWebsite()` calls it for you, alongside Cultures, Organizations, Projects
and Tenants. In a web host you only ever write your own `IAuthSource` registration.

```csharp
using Zonit.Extensions;   // AddAuthExtension lives in namespace Zonit.Extensions

builder.Services.AddAuthExtension();   // console / worker / MAUI / WASM host
```

## The contract you implement: `IAuthSource`

```csharp
namespace Zonit.Extensions.Auth;

public interface IAuthSource
{
    Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
```

| Member | Called by the framework? |
|---|---|
| `GetByTokenAsync` | **Yes** — once per HTTP request, from `AuthenticationSchemeService.HandleAuthenticateAsync` in `Zonit.Extensions.Website`, with the value of the `Session` cookie. Return `Identity.Empty` for unknown/expired tokens; the request is then anonymous. |
| `GetByIdAsync` | **No.** Nothing in Zonit calls it. It is a required member purely so your own profile/admin pages have a named contract. |

`IUserDirectory` (one member, `GetByUserNameAsync`) is optional and likewise never called by Zonit.

```csharp
using Zonit.Extensions;        // Identity, Title, Role, Permission
using Zonit.Extensions.Auth;   // IAuthSource, IUserDirectory, UserModel, CredentialModel

internal sealed class MyAuthSource(MyDb db) : IAuthSource, IUserDirectory
{
    public Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var s = db.Sessions.FirstOrDefault(x => x.Token == token);
        if (s is null || s.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(Identity.Empty);   // unknown / expired == anonymous

        return Task.FromResult(new Identity(
            id:          s.UserId,                    // ArgumentException if Guid.Empty
            name:        Title.TryCreate(s.DisplayName, out var t) ? t : Title.Empty,
            roles:       ToRoles(s.Roles),
            permissions: ToPermissions(s.Permissions)));
    }

    // Never called by Zonit — it is here for your own profile / admin pages.
    public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = db.Users.FirstOrDefault(x => x.Id == id);
        return Task.FromResult<UserModel?>(u is null ? null : new UserModel
        {
            Id          = u.Id,
            Name        = u.UserName,
            FirstName   = u.FirstName,
            LastName    = u.LastName,
            Roles       = [.. u.Roles],
            Policy      = [.. u.Permissions],   // "Policy" holds PERMISSION tokens
            Credentials = [new CredentialModel { Method = "email", Value = u.Email }],
        });
    }

    public Task<UserModel?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var id = db.Users.FirstOrDefault(x => x.UserName == userName)?.Id ?? Guid.Empty;
        return id == Guid.Empty ? Task.FromResult<UserModel?>(null) : GetByIdAsync(id, cancellationToken);
    }

    // Role / Permission constructors THROW on malformed input. Never build them
    // straight from a database string with new Role(x) — one bad row 500s the request.
    private static List<Role> ToRoles(IEnumerable<string> raw)
    {
        List<Role> list = [];
        foreach (var s in raw)
            if (Role.TryCreate(s, out var r)) list.Add(r);
        return list;
    }

    private static List<Permission> ToPermissions(IEnumerable<string> raw)
    {
        List<Permission> list = [];
        foreach (var s in raw)
            if (Permission.TryCreate(s, out var p)) list.Add(p);
        return list;
    }
}
```

Register it. One class implementing both contracts needs **two** registrations pointing at one instance:

```csharp
builder.Services.AddScoped<MyAuthSource>();
builder.Services.AddScoped<IAuthSource>(sp => sp.GetRequiredService<MyAuthSource>());
builder.Services.AddScoped<IUserDirectory>(sp => sp.GetRequiredService<MyAuthSource>());
```

| Registration | Result (verified at runtime) |
|---|---|
| `AddScoped<IAuthSource, MyAuthSource>()` **before** `AddWebsite()` | Yours wins — the framework's `TryAdd` is skipped. |
| `AddScoped<IAuthSource, MyAuthSource>()` **after** `AddWebsite()` | Yours wins — last registration wins for `GetRequiredService`. (`GetServices<IAuthSource>()` then returns 2.) |
| `TryAddScoped<IAuthSource, MyAuthSource>()` **after** `AddWebsite()` | **`NullAuthSource` wins.** Silently anonymous. |

## Reading the current identity

```csharp
using Zonit.Extensions;
using Zonit.Extensions.Auth;

public sealed class OrderService(IAuthenticatedProvider auth)
{
    public void Guard()
    {
        if (!auth.IsAuthenticated)                       // default interface member
            throw new UnauthorizedAccessException();

        Identity me = auth.Current;                      // Identity.Empty when anonymous
        Guid id = me.Id;
        string display = me.Name.Value;                  // Name is a Title VO
        bool canRead = me.HasPermission("orders.read");  // wildcard-aware
        bool isAdmin = me.IsInRole("admin");
    }
}
```

```razor
@inject IAuthenticatedProvider Auth

@if (Auth.IsAuthenticated)
{
    <p>Hello, @Auth.Current.Name</p>
}
```

`IsAuthenticated` is a *default interface member* — reachable only through the `IAuthenticatedProvider`
type, not through a concrete implementation.

Wildcard semantics come from `Permission.Implies` in `Zonit.Extensions`: a trailing `*` matches **zero
or more** sub-tokens. Measured against a holder of `orders.*`:

| Check | Result |
|---|---|
| `HasPermission("orders.read")` | `true` |
| `HasPermission("orders")` | `true` — trailing `*` also matches the bare prefix |
| `HasPermission("Orders.Read")` | `true` — the constructor lowercases |
| `HasPermission("billing.read")` | `false` |
| `HasPermission("")` | **`true`** — empty permission is universally allowed |
| `HasPermission("orders read")` | throws `ArgumentException` from the implicit conversion |

Details of the value objects themselves: `.zonit/extensions/core/auth-value-objects.md`.

## Sign-in and sign-out are entirely your code

The `"Zonit"` scheme is a **read-only** `AuthenticationHandler`. It implements neither
`IAuthenticationSignInHandler` nor `IAuthenticationSignOutHandler`. Verified against a live host:

```text
http.SignInAsync("Zonit", principal)
  -> InvalidOperationException: The authentication handler registered for scheme 'Zonit' is
     'AuthenticationSchemeService' which cannot be used for SignInAsync.

http.SignOutAsync("Zonit")
  -> InvalidOperationException: ... which cannot be used for SignOutAsync.
```

Do not call them. Signing in means: validate the credentials yourself, persist a session token in your
own store, and write the cookie the scheme reads. The package contributes only the two names.

| Constant | Value |
|---|---|
| `AuthExtensions.SchemeName` | `"Zonit"` |
| `AuthExtensions.SessionCookieName` | `"Session"` |

```csharp
using Zonit.Extensions.Auth;   // AuthExtensions

public static async Task<IResult> SignIn(
    HttpContext http, string userName, string password, MyDb db, CancellationToken ct)
{
    var token = await IssueSessionTokenAsync(db, userName, password, ct);
    if (token is null)
        return Results.Unauthorized();

    http.Response.Cookies.Append(AuthExtensions.SessionCookieName, token, new CookieOptions
    {
        HttpOnly    = true,
        IsEssential = true,
        Secure      = http.Request.IsHttps,
        SameSite    = SameSiteMode.Lax,
        Expires     = DateTimeOffset.UtcNow.AddDays(14),
    });

    return Results.Redirect("/");
}

public static IResult SignOut(HttpContext http)
{
    http.Response.Cookies.Delete(AuthExtensions.SessionCookieName);   // also revoke it server-side
    return Results.Redirect("/");
}
```

`AuthExtensions` is a class of **constants only** — despite the name it has no extension methods.

### Refreshing a live Blazor circuit

A cookie write does not reach an already-running circuit; the middleware only runs on HTTP requests.
Push the new identity into the scoped repository and `<AuthorizeView>` re-renders:

```csharp
using Zonit.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Auth.Repositories;   // <-- different namespace

public sealed class CircuitSignIn(IAuthSource source, IAuthenticatedRepository repository)
{
    public async Task ApplyAsync(string token, CancellationToken ct = default)
    {
        var identity = await source.GetByTokenAsync(token, ct);
        repository.Initialize(identity);   // raises OnChange -> AuthenticationStateProvider notifies
    }

    public void SignOut() => repository.Initialize(Identity.Empty);
}
```

**`Initialize` compares by `Identity.Id` only.** Verified: re-initializing the same user with a richer
snapshot updates `Current` but does **not** raise `OnChange`. Granting a permission to the signed-in
user mid-circuit therefore will not refresh `<AuthorizeView>` — force a re-render yourself, or
round-trip through a full page load.

## Non-web hosts

There is no pipeline to install. Create a scope, resolve, initialize:

```csharp
using var scope = services.CreateScope();

var identity = await scope.ServiceProvider
    .GetRequiredService<IAuthSource>()
    .GetByTokenAsync(token, ct);

scope.ServiceProvider
    .GetRequiredService<IAuthenticatedRepository>()
    .Initialize(identity);

// everything resolved from `scope` now sees this identity
var provider = scope.ServiceProvider.GetRequiredService<IAuthenticatedProvider>();
```

Resolving `IAuthenticatedProvider` from the **root** provider throws — it is scoped.

## Namespaces

| Type | Namespace |
|---|---|
| `AddAuthExtension` | `Zonit.Extensions` |
| `IAuthSource`, `IUserDirectory`, `IAuthenticatedProvider`, `UserModel`, `CredentialModel`, `AuthExtensions`, `IUserEntity` | `Zonit.Extensions.Auth` |
| `IAuthenticatedRepository` | `Zonit.Extensions.Auth.Repositories` |
| `Identity`, `Permission`, `Role`, `Title` | `Zonit.Extensions` (package `Zonit.Extensions`) |
| `IdentityClaimsBuilder`, `RequirePermissionAttribute`, `RequireRoleAttribute`, `AuthenticationSchemeService` | `Zonit.Extensions.Website.Authentication` (package `Zonit.Extensions.Website`) |

## What `Zonit.Extensions.Website` adds on top

Installed automatically by `AddWebsite()` / `UseWebsite<TApp>()`:

- the `"Zonit"` cookie scheme (`AuthenticationSchemeService`) — reads the `Session` cookie, calls your
  `IAuthSource.GetByTokenAsync`, builds a `ClaimsPrincipal`. Registered only if the consumer has not
  already brought their own `IAuthenticationSchemeProvider`;
- `SessionMiddleware` — projects `HttpContext.User` back into `IAuthenticatedRepository`. It does **not**
  re-query `IAuthSource` (one lookup per request), it skips static-asset requests, and it writes only
  while the scope's identity is still empty;
- `IdentityClaimsBuilder` — emits `ClaimTypes.NameIdentifier` / `Name` / `Role` plus `zonit:permission`
  (constant `IdentityClaimsBuilder.PermissionClaimType`), and the reverse `Read(ClaimsPrincipal)`, which
  drops malformed role/permission claims silently;
- `[RequirePermission("orders.read")]` / `[RequireRole("admin")]` plus the wildcard-aware handlers and
  policy provider — see `.zonit/extensions/website/permissions.md`;
- `AddCascadingAuthenticationState()` and an `AuthenticationStateProvider` bound to
  `IAuthenticatedProvider.OnChange`.

## Known limitations

- **Identity does not survive prerender → circuit in a trimmed app.** `AuthStateBridge` (in
  `Zonit.Extensions.Website`) round-trips the `Identity` through `PersistentComponentState`, but both its
  `Restore` and its persist callback return early when `JsonSerializer.IsReflectionEnabledByDefault` is
  `false` — and the SDK turns that switch off for **any** `PublishTrimmed` publish, not only
  `PublishAot`. The interactive render then starts anonymous and **nothing is logged**. There is no
  opt-out knob. The same holds for culture, workspace, catalog and cookie state. See
  `.zonit/extensions/website/hydration.md`.
- **`Zonit.Extensions.Auth` itself is trim/AOT clean** — no reflection, no dynamic code, ILC reports
  nothing against it. The limitation above lives entirely in the Website bridge.
- **`GetByIdAsync`, `IUserDirectory` and `UserModel` are framework-dead.** You must implement
  `GetByIdAsync` to satisfy the interface, but no Zonit code path reaches it. Do not expect the framework
  to read anything out of `UserModel` — in particular `UserModel.Policy` (permission tokens) and
  `UserModel.Roles` are never folded into an `Identity`.
- **`IUserEntity` is referenced by nothing** in the framework. Treat it as a naming convention you may
  adopt, not a hook.
- **`CredentialModel` (two unvalidated strings, `Method` / `Value`) overlaps the validated `Credential`
  value object** in `Zonit.Extensions`. `CredentialModel` exists only as the element type of
  `UserModel.Credentials`; prefer `Credential` in your own domain.
