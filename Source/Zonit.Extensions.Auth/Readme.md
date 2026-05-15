# Zonit.Extensions.Auth

Authentication / authorization plug-in for ASP.NET Core, integrated with the **standard Microsoft authorization pipeline** (`AddAuthorization`, `[Authorize]`, `IAuthorizationService`, `<AuthorizeView>`) and extended with VO-typed permission and role attributes.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Auth.svg)](https://www.nuget.org/packages/Zonit.Extensions.Auth/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Auth.svg)](https://www.nuget.org/packages/Zonit.Extensions.Auth/)

```bash
dotnet add package Zonit.Extensions.Auth
```

## What you get

- A cookie-based authentication scheme `"Zonit"` that turns the `Session` cookie into a `ClaimsPrincipal` via `IdentityClaimsBuilder`.
- `[RequirePermission("orders.read")]` and `[RequireRole("admin")]` — built on .NET 8+ `IAuthorizationRequirementData`, **no manual policy registration** in `AddAuthorization`.
- `Permission` and `Role` value-object aware authorization handlers (with wildcards, e.g. `orders.*`).
- `IAuthenticatedProvider.Current` returning a lightweight `Identity` VO (Id + Name + Roles + Permissions).
- A scoped `IAuthenticatedRepository` that the `SessionMiddleware` populates once per request / circuit.
- Cascading authentication state for Blazor.

## Setup

```csharp
// Program.cs
builder.Services.AddAuthExtension();

var app = builder.Build();
app.UseAuthExtension();   // UseAuthentication → UseAuthorization → SessionMiddleware
```

Implement `IAuthSource` in your app (translate the cookie value into an `Identity`):

```csharp
internal sealed class MyDbSessionProvider(MyDb db) : IAuthSource
{
    public async Task<Identity> GetByTokenAsync(string token, CancellationToken ct)
    {
        var s = await db.Sessions.Include(x => x.User)
                                 .ThenInclude(u => u.Roles)
                                 .FirstOrDefaultAsync(x => x.Token == token, ct);

        if (s is null || s.ExpiresAt < DateTime.UtcNow) return Identity.Empty;

        return new Identity(
            id:           s.User.Id,
            name:         new Title(s.User.DisplayName),
            roles:        s.User.Roles.Select(r => new Role(r.Name)),
            permissions:  s.User.Permissions.Select(p => Permission.Create(p)));
    }
}
```

```csharp
builder.Services.AddScoped<IAuthSource, MyDbSessionProvider>();
```

## Declarative authorization

```csharp
// Controllers
[RequirePermission("orders.write")]
public IActionResult Update(...) => ...;

// Minimal API
app.MapGet("/admin", () => "ok")
   .RequireAuthorization(new RequirePermissionAttribute("admin.*"));

// Razor / Blazor pages
@attribute [RequirePermission("orders.read")]

// Components
<AuthorizeView Policy="@(new RequirePermissionAttribute("orders.read").Policy)">
  <Authorized>...</Authorized>
  <NotAuthorized>403</NotAuthorized>
</AuthorizeView>
```

Wildcards: a user holding `orders.*` satisfies `[RequirePermission("orders.read")]` automatically — the comparison goes through `Permission.Implies`.

## Reading the current identity

```razor
@inject IAuthenticatedProvider Auth

@if (Auth.IsAuthenticated)
{
    <p>Hello, @Auth.Current.Name</p>
}
```

Or via the standard `AuthenticationStateProvider` / `[CascadingParameter] Task<AuthenticationState>` — the same principal is built via `IdentityClaimsBuilder`, so cookie-based and circuit-based paths are 1:1.

## Claim shape (for advanced consumers)

| Claim type | Source |
|---|---|
| `ClaimTypes.NameIdentifier` | `Identity.Id` |
| `ClaimTypes.Name` | `Identity.Name` (skipped when empty) |
| `ClaimTypes.Role` | one per `Identity.Roles` |
| `zonit:permission` | one per `Identity.Permissions` (literal token, wildcards preserved) |

The permission claim type is a constant: `IdentityClaimsBuilder.PermissionClaimType`.

## What this package does NOT do

- It does not provide login UI, password hashing, MFA or session storage. Those are the consumer's concern. The package only exposes the *contract* (`IAuthSource`).
- It does not check authorization itself in `IWorkspaceProvider` / `ICatalogProvider`. Those are pure tenant context — authorization is one place, not two.

## License

MIT.
