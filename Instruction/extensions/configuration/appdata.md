# Configuration — one file per topic

`Zonit.Extensions.Configuration` folds every JSON file under `AppData/Settings` into the host's
configuration, so each concern gets its own file instead of sharing one `appsettings.json`.

The whole API is one call, and it extends `IHostApplicationBuilder` — web, worker and console hosts
are identical here.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddAppData();
```

There is a second overload on `IServiceCollection`, for callers that never see the builder:

```csharp
builder.Services.AddAppData();
```

It reaches the same place: the host registers its `ConfigurationManager` as the `IConfiguration`
service and that type implements `IConfigurationBuilder` as well, so adding a source to the resolved
instance is immediately visible through `builder.Configuration` — it is the same object. This is how
`services.AddWebsite()` loads settings without ever touching the builder.

**Call it before `Build()`** either way. Kestrel and the logging providers read configuration while
the host is being built, so the first line after `CreateBuilder(args)` is the right place; reaching
the configuration manager is not the same as beating its readers. A missing settings directory is a
valid state: the host runs on whatever other sources it has.

`AddAppData()` is idempotent across **both** overloads — they share one marker service, so a repeat
call keeps the first call's options and adds nothing twice.

## With Zonit.Extensions.Website

Put `AddAppData` **above** `AddWebsite`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddAppData();
builder.Services.AddWebsite(o => o.AddArea<ShopArea>());
```

`AddWebsite` deliberately does not call this for you, and the order is a requirement rather than a
preference. `o.AddArea<T>()` runs each area's `ConfigureServices` **during** `AddWebsite`, and areas
read configuration there — a plugin resolving its connection string, for one — so a call from inside
`AddWebsite` would always be too late for the areas it just registered.

Get it backwards and an area fails at startup, which is the good outcome: loud, with the culprit in
the stack.

```
Zonit.Extensions.Databases.DatabaseException: Database configuration section not found.
   at SomeArea.ConfigureServices(...)
   at WebsiteOptions.AddArea[TArea]()
```

## Layout

```
AppData/Settings/
  database.json            connection string
  kestrel.json             endpoints and certificate
  cultures.json            default culture and the supported list
  serilog.json             sinks and filters
  tenants.json             site identity

  kestrel.dev.json         Development only — reserved ".dev.json" suffix
  database.local.json      machine-local secrets, gitignored

  Staging/                 folder name = ASPNETCORE_ENVIRONMENT
    kestrel.json
```

Files use the same section names you would write in `appsettings.json`:

```json
{
  "Culture": {
    "DefaultCulture": "pl-pl",
    "SupportedCultures": [ "en-us", "pl-pl" ]
  }
}
```

They are **merged, not chosen** — every file contributes. Keep one topic per file: when two files
declare the same section the winner is decided by filename order, which is not a property anyone
should have to reason about.

## Load order

Later wins:

| # | Source |
| --- | --- |
| 1 | `appsettings.json` |
| 2 | `appsettings.{Environment}.json` |
| 3 | `AppData/Settings/*.json` — alphabetical |
| 4 | `AppData/Settings/*.dev.json` — Development only |
| 5 | `AppData/Settings/*.local.json` |
| 6 | `AppData/Settings/{Environment}/…` — same three groups again |
| 7 | user secrets |
| 8 | environment variables |
| 9 | command line |

The sources are inserted **directly after the last `appsettings*.json`**, never appended. Appending
would put them above environment variables — the mechanism a container and a CI pipeline configure
the app with — and quietly beat them. With no `appsettings.json` present the anchor falls back to
the front of the list, so everything else still overrides.

## Two ways to vary by environment

`.dev.json` is a **reserved suffix** for Development. It is unambiguous because it is a fixed word:
a general `file.{Environment}.json` convention is not readable, since `market-data.api.json` and
`market-data.Staging.json` have identical shape and the loader would have to guess whether the
middle segment names an environment or part of the topic.

Every other environment uses a **folder**. For a literal "dev" in a topic name write `foo-dev.json`.

## Secrets

`*.local.json` loads last in its directory and belongs in `.gitignore` — put the key in
`database.local.json` next to `database.json`.

- `.gitignore` keeps a secret out of source control, **not out of a container image**. If
  `AppData/**` is copied to build output, exclude the pattern explicitly.
- Production secrets still belong in environment variables or a vault; both override these files.

## Reload

Sources are registered with `reloadOnChange: true`. This is what makes hot reload real elsewhere in
the stack — an `IOptionsMonitor<T>` consumer only ever sees a change if the underlying source
reloads, so `cultures.json` edits reach `Zonit.Extensions.Cultures` because of this flag.

Disable with `o.ReloadOnChange = false` where file watching is unreliable or expensive (network
mounts, some container filesystems). Prefer `DOTNET_USE_POLLING_FILE_WATCHER=1` there over losing
reload altogether.

## Options

| Member | Default | Behaviour |
| --- | --- | --- |
| `SettingsPath` | `"AppData/Settings"` | Relative to the content root. A path resolving outside it throws at startup — the files are read through `ContentRootFileProvider`, which cannot serve above its own root, so the alternative is silently loading nothing. |
| `ReloadOnChange` | `true` | Whether editing a file reloads configuration in the running process. |
| `CreateIfMissing` | `true` | Creates the directory **in Development only**, best-effort. An empty directory changes nothing at runtime, and a read-only content root in production would otherwise fail the start before logging exists — `IOException` and `UnauthorizedAccessException` are swallowed. |

## Traps

- **Calling after `Build()` does nothing useful.** The host has already read configuration.
- **A file that is not valid JSON fails the start.** Sources are optional (a missing file is fine),
  but a malformed present file throws — which is the correct trade, silently ignoring a settings
  file you did write is worse.
- **Section collisions are silent.** Two files declaring `"Culture"` merge key by key, last file
  alphabetically winning per key. One topic per file avoids the question.
- **`SupportedCultures` and other pre-populated arrays replace rather than append** — that is a
  `Zonit.Extensions.Cultures` behaviour, see `.zonit/extensions/cultures/cultures.md`.
