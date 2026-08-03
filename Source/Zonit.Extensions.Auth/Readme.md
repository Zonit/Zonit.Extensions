# Zonit.Extensions.Auth

Framework-agnostic authentication **core**. It defines the one contract you implement — `IAuthSource`
(session token → `Identity`) — and carries the resulting `Identity` value object through a single unit
of work (HTTP request, Blazor circuit, background job).

No ASP.NET Core dependency. The cookie scheme, middleware, `[RequirePermission]` and the Blazor
`AuthenticationStateProvider` ship in [Zonit.Extensions.Website](../Zonit.Extensions.Website/Readme.md)
and are wired by `AddWebsite()`.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Auth.svg)](https://www.nuget.org/packages/Zonit.Extensions.Auth/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Auth.svg)](https://www.nuget.org/packages/Zonit.Extensions.Auth/)

```bash
dotnet add package Zonit.Extensions.Auth
```

## What `AddAuthExtension()` registers

Four scoped services, all via `TryAdd`, so it is idempotent and every slot is overridable:

| Service | Implementation | Role |
|---|---|---|
| `IAuthenticatedRepository` | internal `AuthenticatedRepository` | write side — `Initialize(identity)` |
| `IAuthenticatedProvider` | internal `AuthenticatedService` | read side — `Current`, `IsAuthenticated`, `OnChange` |
| `IAuthSource` | internal `NullAuthSource` | fallback: every token → `Identity.Empty` |
| `IUserDirectory` | internal `NullAuthSource` | fallback: every lookup → `null` |

It registers **no** authentication scheme, **no** middleware, **no** authorization handlers and **no**
policy provider. There is no `UseAuthExtension()`.

`Zonit.Extensions.Website.AddWebsite()` already calls this, so a web host does not call it again.

```csharp
using Zonit.Extensions;                    // AddAuthExtension
builder.Services.AddAuthExtension();       // console / worker / MAUI / WASM host
```

> Because the fallback exists, a host that never registers an `IAuthSource` boots normally and is
> **silently anonymous** — no exception, no log entry.

## Implementing `IAuthSource`

`GetByTokenAsync` is the only member Zonit ever calls (once per HTTP request, from the `"Zonit"` cookie
scheme in `Zonit.Extensions.Website`). `GetByIdAsync` and the optional `IUserDirectory` exist so your
own profile / admin pages have a named contract.

```csharp
using Zonit.Extensions;        // Identity, Title, Role, Permission
using Zonit.Extensions.Auth;   // IAuthSource, IUserDirectory, UserModel

internal sealed class MyAuthSource(MyDb db) : IAuthSource, IUserDirectory
{
    public Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var s = db.Sessions.FirstOrDefault(x => x.Token == token);
        if (s is null || s.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(Identity.Empty);      // unknown / expired == anonymous

        return Task.FromResult(new Identity(
            id:          s.UserId,                       // ArgumentException if Guid.Empty
            name:        Title.TryCreate(s.DisplayName, out var t) ? t : Title.Empty,
            roles:       ToRoles(s.Roles),
            permissions: ToPermissions(s.Permissions)));
    }

    // Neither of these is ever called by Zonit — they exist for your own pages.
    public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = db.Users.FirstOrDefault(x => x.Id == id);
        return Task.FromResult<UserModel?>(u is null ? null : new UserModel
        {
            Id     = u.Id,
            Name   = u.UserName,
            Roles  = [.. u.Roles],
            Policy = [.. u.Permissions],   // "Policy" holds PERMISSION tokens
        });
    }

    public Task<UserModel?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var id = db.Users.FirstOrDefault(x => x.UserName == userName)?.Id ?? Guid.Empty;
        return id == Guid.Empty ? Task.FromResult<UserModel?>(null) : GetByIdAsync(id, cancellationToken);
    }

    // Role / Permission / Title constructors THROW on malformed or over-long input.
    // Never build them straight from a database string — use TryCreate.
    private static List<Role> ToRoles(IEnumerable<string> raw)
    {
        List<Role> list = [];
        foreach (var s in raw) if (Role.TryCreate(s, out var r)) list.Add(r);
        return list;
    }

    private static List<Permission> ToPermissions(IEnumerable<string> raw)
    {
        List<Permission> list = [];
        foreach (var s in raw) if (Permission.TryCreate(s, out var p)) list.Add(p);
        return list;
    }
}
```

Registering `IAuthSource` does **not** cover `IUserDirectory` — they are separate DI keys:

```csharp
builder.Services.AddScoped<MyAuthSource>();
builder.Services.AddScoped<IAuthSource>(sp => sp.GetRequiredService<MyAuthSource>());
builder.Services.AddScoped<IUserDirectory>(sp => sp.GetRequiredService<MyAuthSource>());
```

Use `AddScoped`, not `TryAddScoped`: after `AddWebsite()` the framework's fallback is already in the
collection, so `TryAdd` is a silent no-op and you stay anonymous.

## Reading the current identity

```csharp
public sealed class OrderService(IAuthenticatedProvider auth)
{
    public void Guard()
    {
        if (!auth.IsAuthenticated) throw new UnauthorizedAccessException();

        Identity me = auth.Current;                      // Identity.Empty when anonymous
        bool canRead = me.HasPermission("orders.read");  // a holder of "orders.*" passes
        bool isAdmin = me.IsInRole("admin");
    }
}
```

```razor
@inject IAuthenticatedProvider Auth
@if (Auth.IsAuthenticated) { <p>Hello, @Auth.Current.Name</p> }
```

`Identity` is a `readonly struct`, so `Current != null` compiles and is always true — test `HasValue`
(or `IsAuthenticated`). Wildcards go through `Permission.Implies`: a trailing `*` matches **zero or
more** sub-tokens, so `orders.*` implies both `orders.read` and a bare `orders`.

## Sign-in and sign-out

Both are entirely your code. The `"Zonit"` scheme is a read-only `AuthenticationHandler` — it is not an
`IAuthenticationSignInHandler`, so `HttpContext.SignInAsync("Zonit", …)` and `SignOutAsync("Zonit")`
throw `InvalidOperationException`. This package contributes only the two names:

| Constant | Value |
|---|---|
| `AuthExtensions.SchemeName` | `"Zonit"` |
| `AuthExtensions.SessionCookieName` | `"Session"` |

```csharp
// sign in: persist your own session token, then write the cookie the scheme reads
http.Response.Cookies.Append(AuthExtensions.SessionCookieName, token, new CookieOptions
{
    HttpOnly = true, IsEssential = true, Secure = http.Request.IsHttps,
});

// sign out: delete the cookie and revoke the token server-side
http.Response.Cookies.Delete(AuthExtensions.SessionCookieName);
```

Inside a live Blazor circuit the cookie write is not observed by the middleware — call
`IAuthenticatedRepository.Initialize(identity)` to refresh `<AuthorizeView>`. Note that `Initialize`
raises `OnChange` only when the `Identity.Id` changed (equality is by id alone), so a permission change
for the same signed-in user does not by itself trigger a re-render.

## Non-web hosts

```csharp
using var scope = services.CreateScope();

var identity = await scope.ServiceProvider
    .GetRequiredService<IAuthSource>().GetByTokenAsync(token, ct);

scope.ServiceProvider
    .GetRequiredService<IAuthenticatedRepository>().Initialize(identity);
```

The provider and the repository are scoped; resolving them from the root provider throws, so a
singleton `IHostedService` must create a scope.

## Namespaces

| Type | Namespace |
|---|---|
| `AddAuthExtension` | `Zonit.Extensions` |
| `IAuthSource`, `IUserDirectory`, `IAuthenticatedProvider`, `UserModel`, `CredentialModel`, `AuthExtensions` | `Zonit.Extensions.Auth` |
| `IAuthenticatedRepository` | `Zonit.Extensions.Auth.Repositories` |
| `Identity`, `Permission`, `Role`, `Title` | `Zonit.Extensions` |

## What this package does NOT do

- No login UI, password hashing, MFA or session storage — it only defines the `IAuthSource` contract.
- No authentication scheme, middleware or authorization handlers. `[RequirePermission]` / `[RequireRole]`,
  `IdentityClaimsBuilder` and the cascading Blazor auth state all live in `Zonit.Extensions.Website`
  (namespace `Zonit.Extensions.Website.Authentication`) and are enabled by `AddWebsite()`.
- No authorization checks in `IWorkspaceProvider` / `ICatalogProvider` — those are pure tenant context.

## Known limitation

In a Blazor app published with `PublishTrimmed` (or `PublishAot`), the prerender → circuit state bridge
in `Zonit.Extensions.Website` is disabled, so the `Identity` does not cross the boundary and the
interactive render starts anonymous. Nothing is logged and there is no opt-out. This package itself is
trim- and AOT-clean.

## License

MIT.
