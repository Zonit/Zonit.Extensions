# Identity, Permission, Role, Credential

Four `readonly struct` value objects in namespace `Zonit.Extensions`, from the `Zonit.Extensions`
package. Nothing here is registered in DI — these are plain types you construct and compare.

The authorization *stack* that consumes them (`[RequirePermission]`, `IPermissionChecker`) lives in
`Zonit.Extensions.Auth`; see `.zonit/extensions/auth/auth.md`.

## Read this first

- **`Permission`'s trailing `*` matches zero or one token, not "the rest of the tree".**
  `"orders.*"` grants `orders` and `orders.read`, but **not** `orders.read.all`. See the table below.
- **An empty required permission grants access.** `identity.HasPermission(Permission.Empty)` is `true`.
  A `Permission` field you forgot to set is an open door, not a closed one.
- **Equality is by `Id` alone** for `Identity`. Two instances with the same `Id` are equal even when one
  is fully hydrated and the other is Id-only.
- `Permission.Empty.Implies(anything)` is **`false`** — an empty permission grants nothing when it is on
  the *granting* side. The asymmetry is deliberate but easy to get backwards.

## Identity: Id-only vs hydrated snapshot

`Identity` carries an actor: `Id` (`Guid`), plus an optional snapshot of `Name` (`Title`), `Roles`
(`ImmutableArray<Role>`) and `Permissions` (`ImmutableArray<Permission>`).

```csharp
// Id-only — what you get from a database column, a claim, or a Guid.
var author = new Identity(userId);
author.HasValue;      // true  (Id is non-empty)
author.HasSnapshot;   // false (Name/Roles/Permissions are empty)

// Hydrated — what you build at the auth boundary or receive as a JSON object.
var actor = new Identity(
    id: userId,
    name: new Title("Alice"),
    roles: [new Role("admin")],
    permissions: [new Permission("orders.*")]);

actor.HasSnapshot;                            // true
actor.IsInRole(new Role("admin"));            // true
actor.HasPermission(new Permission("orders.read"));   // true (wildcard expands)
```

`HasValue` and `HasSnapshot` answer different questions and both matter:

| | `HasValue` | `HasSnapshot` |
|---|---|---|
| `Identity.Empty` / `default` | `false` | `false` |
| `new Identity(id)` | `true` | `false` |
| `new Identity(id, name, …)` | `true` | `true` |

The snapshot is **never** lazily loaded. A VO does no I/O. If `HasSnapshot` is `false` and you need a
display name, fetch it — `Zonit.Extensions.Databases` exposes an opt-in extension for that.

### Guid conversions and the `Guid.Empty` trap

```csharp
Guid id = actor;                 // implicit — this is what you persist
Identity a = someGuid;           // implicit — Id-only
Identity b = Guid.Empty;         // Identity.Empty, does NOT throw

var c = new Identity(Guid.Empty);   // ArgumentException — the constructor DOES throw
```

The implicit conversion is forgiving; the constructor is not. Feed database `Guid`s through the implicit
conversion (or through a `HasConversion` that checks for `Guid.Empty`) so a null-ish column does not blow
up materialization.

### JSON shape depends on hydration

`IdentityJsonConverter` writes the smallest correct payload and reads both shapes:

```csharp
JsonSerializer.Serialize(new Identity(id));
// "11111111-1111-1111-1111-111111111111"

JsonSerializer.Serialize(new Identity(id, new Title("Alice"), [new Role("admin")], [new Permission("orders.*")]));
// {"id":"11111111-…","name":"Alice","roles":["admin"],"permissions":["orders.*"]}

JsonSerializer.Serialize(Identity.Empty);   // null
```

A consumer deserializing into `Identity` must therefore accept *string or object or null*. That is
handled for you; just do not write a schema that declares the field a plain string.

`Organization` and `Project` follow exactly the same pattern (`Id` + `HasSnapshot`, snapshot is
`Name`/`Slug`), with the same JSON string-or-object shape.

## Permission: grammar and wildcards

Format: dot-separated tokens; each token is either `[a-z0-9_-]+` or a bare `*`. Input is trimmed and
lower-cased. Max total length 200 (`Permission.MaxLength`).

```csharp
new Permission("Orders.Read").Value;   // "orders.read" — normalized to lower case
new Permission("orders.*.read");       // ok — wildcard at any position
new Permission("a*b");                 // ArgumentException — mixed literal/wildcard tokens are rejected
```

A `*` always means "this whole token". There is no prefix or suffix matching.

### `Implies` — exactly what it does

`granted.Implies(required)` asks "does the permission I hold cover the permission being demanded?".

| granted | required | result |
|---|---|---|
| `orders.*` | `orders.read` | `true` |
| `orders.*` | `orders` | `true` (trailing `*` matches zero tokens) |
| `orders.*` | `orders.read.all` | **`false`** |
| `orders.*.read` | `orders.eu.read` | `true` |
| `orders.*.read` | `orders.eu.write` | `false` |
| `orders.read` | `orders.*` | `false` (the specific does not imply the general) |
| `*` | `anything.at.all` | **`false`** |
| `*` | `anything` | `true` |
| *(empty)* | `orders.read` | `false` |
| `orders.read` | *(empty)* | `true` |
| *(empty)* | *(empty)* | `false` |

The two bold rows are the ones that surprise people. A trailing `*` consumes **at most one** token, so
`admin.*` is not a super-user grant and `*` is not "everything". If you want a true super-user, grant
the exact permissions, or grant `*` **and** check with a single-token permission, or add an explicit
short-circuit in your own policy:

```csharp
static bool IsSuperUser(Identity actor) =>
    actor.Permissions.Contains(new Permission("*"));
```

Note this contradicts the XML doc on `Permission.Implies`, which claims "a trailing `*` matches zero or
more sub-tokens" (AWS IAM style). The implementation matches zero or one. Trust the table.

### Empty permission = no constraint

```csharp
var actor = new Identity(userId);              // no permissions at all
actor.HasPermission(Permission.Empty);         // true  — nothing was demanded
actor.HasPermission(new Permission("orders.read"));   // false
```

`Identity.HasPermission` short-circuits to `true` when the *required* permission is empty. That is what
makes an unset `Permission` on a nav item or an attribute mean "visible to everyone". It also means a
typo that produces `Permission.Empty` silently disables a guard — always construct permissions from
constants, and prefer `Permission.TryCreate` over the implicit conversion when the value comes from
config:

```csharp
if (!Permission.TryCreate(configuredValue, out var required))
    throw new InvalidOperationException($"'{configuredValue}' is not a valid permission.");
```

## Role

A single token: `^[a-z0-9][a-z0-9_-]*$`, 1-64 characters (`Role.MaxLength`), lower-cased on
construction. No dots, no wildcards — that is what distinguishes it from `Permission`.

```csharp
new Role("Admin").Value;      // "admin"
new Role("super-editor");     // ok
new Role("-admin");           // ArgumentException — must start with a letter or digit
Role.TryCreate(input, out var role);   // false instead of throwing
```

`Identity.IsInRole` is an ordinal comparison over the snapshot and returns `false` for
`Role.Empty` and for any Id-only identity. Roles are *not* expanded into permissions by this package;
projecting roles → permissions is your policy layer's job.

## Credential

One type for "whatever the user typed at the identity boundary". The kind is auto-detected:

```csharp
new Credential("Alice@Example.COM").Kind;    // CredentialKind.Email    — Value "alice@example.com"
new Credential("+48 600-100-200").Kind;      // CredentialKind.Phone    — Value "+48600100200"
new Credential("jkowalski").Kind;            // CredentialKind.Username
new Credential(Guid.NewGuid()).Kind;         // CredentialKind.Id       — Id property is populated
```

Detection order is Guid → phone (after stripping spaces, dashes and parentheses) → e-mail → username.
Unrecognized input throws `ArgumentException` from the constructor; `Credential.TryCreate` returns
`false`.

| Kind | Accepted shape |
|---|---|
| `Id` | any parseable non-empty `Guid` |
| `Phone` | optional `+`, then 7-15 digits (spaces/dashes/parens stripped first) |
| `Email` | `x@y.z`, no whitespace, lower-cased |
| `Username` | `^[a-z0-9][a-z0-9._\-]{2,63}$`, lower-cased |
| `Unknown` | never stored — construction fails instead |

Length is gated **before** normalization at `Credential.MaxLength` (254). A megabyte of text from a
public login endpoint is rejected without running the regexes:

```csharp
Credential.TryCreate(new string('a', 300), out _);   // false — length gate, no regex work
Credential.TryCreate("ab", out _);                   // false — below MinLength (3)
```

Credentials are deliberately **not** part of `Identity`. Do not cache them alongside actor data; load
them on demand from whatever service owns them.

## Model binding and forms

`Permission`, `Role` and `Credential` use `ValueObjectTypeConverter<T>`, so ASP.NET Core model binding
and `IConfiguration` binding validate them and produce a real error message. `Identity` uses a dedicated
`IdentityTypeConverter` that accepts a `Guid` or a Guid-formatted string and produces an **Id-only**
identity — it never hydrates a snapshot. Details in `.zonit/extensions/core/binding.md`.

## Known limitations

- `Permission.Implies` does not implement the AWS IAM / Casbin "trailing wildcard matches the whole
  subtree" semantics its own XML documentation claims. Depth beyond one token is not covered.
- `Identity`'s JSON reader constructs `new Title(name)` directly, so an object payload whose `name`
  exceeds `Title.MaxLength` (60) throws `ArgumentException` out of `JsonSerializer.Deserialize`. The
  Id-only string form has no such failure mode.
- `Credential.Kind` is derived, not stored. Persist `Value` only; the kind is recomputed on load.
