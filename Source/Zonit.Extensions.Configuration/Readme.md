# Zonit.Extensions.Configuration

One configuration file per concern, instead of one `appsettings.json` in which the Serilog section
buries everything else. Host-agnostic: the entry point extends `IHostApplicationBuilder`, so web,
worker and console hosts use the same call. Depends only on
`Microsoft.Extensions.Configuration.Json` and `Microsoft.Extensions.Hosting.Abstractions` — both of
which an ASP.NET Core host already has from the shared framework — and on **no other Zonit package**.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Configuration.svg)](https://www.nuget.org/packages/Zonit.Extensions.Configuration/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Configuration.svg)](https://www.nuget.org/packages/Zonit.Extensions.Configuration/)

```bash
dotnet add package Zonit.Extensions.Configuration
```

## Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddAppData();   // first line — Kestrel and logging read configuration during Build()
```

There is a second overload on `IServiceCollection`, for callers that never see the host builder:

```csharp
builder.Services.AddAppData();
```

It works because the host registers its `ConfigurationManager` as the `IConfiguration` service and
that type implements `IConfigurationBuilder` too, so the source list is reachable from the
collection alone. Both overloads share one idempotency marker, and both must run before `Build()` —
reaching the manager is not the same as beating Kestrel and the logging providers to it.

**Using Zonit.Extensions.Website?** `AddWebsite()` already calls this, from either receiver, and the
package arrives transitively — there is nothing to add. Opt out with `o.UseAppData = false`, or call
`builder.AddAppData(o => …)` first to keep the loader with your own settings.

## Layout

```
AppData/Settings/
  database.json            connection string
  kestrel.json             endpoints and certificate
  cultures.json            default culture and the supported list
  serilog.json             sinks and filters
  tenants.json             site identity: name, description, brand colours

  kestrel.dev.json         Development only — reserved ".dev.json" suffix
  database.local.json      machine-local secrets, gitignored

  Staging/                 folder name = ASPNETCORE_ENVIRONMENT
    kestrel.json
```

Each file is a normal configuration document — the section names are the same ones you would write
in `appsettings.json`:

```json
{
  "Culture": {
    "DefaultCulture": "pl-pl",
    "SupportedCultures": [ "en-us", "pl-pl" ]
  }
}
```

Files are **merged**, not chosen: `database.json` and `kestrel.json` both contribute, so keep one
topic per file. Two files claiming the same section makes the winner depend on their names.

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

The files are inserted **directly after the last `appsettings*.json` source**, not appended.
Appending would place them above the environment variables — which is how a container and a CI
pipeline configure the app — and silently win over them. Hosts with no `appsettings.json` at all
work the same way; the anchor then falls back to the front of the list.

## Two ways to vary by environment

`.dev.json` is a **reserved suffix** meaning "Development only". It is short and unambiguous
precisely because it is a fixed word. A general `file.{Environment}.json` convention could not be
read reliably — `market-data.api.json` and `market-data.Staging.json` have the same shape, so the
loader would have to guess whether the middle segment names an environment or part of the topic.

Every other environment therefore uses a **folder**, where no guessing is possible. If you need a
literal "dev" in a topic name, write `foo-dev.json`.

## Secrets

`*.local.json` loads last within its directory and is meant to sit in `.gitignore`: put a key in
`database.local.json` next to `database.json` and it never reaches the repository.

Two things this does **not** do for you:

- **Exclude the pattern from the build output.** `.gitignore` keeps a secret out of source control,
  not out of a container image. Add an explicit exclusion if `AppData/**` is copied to output.
- **Replace a vault.** Production secrets belong in environment variables or a secret store, both
  of which override these files anyway.

## Reload

Sources are registered with `reloadOnChange: true`, so editing a file applies in the running
process — that is the switch that makes `IOptionsMonitor<T>` consumers, such as
`Zonit.Extensions.Cultures`, actually see the change. Turn it off with `o.ReloadOnChange = false`
where file watching is unreliable or costly (network-mounted volumes, some container filesystems);
`DOTNET_USE_POLLING_FILE_WATCHER=1` is usually a better answer than giving up reload entirely.

## Options

| Member | Default | Behaviour |
| --- | --- | --- |
| `SettingsPath` | `"AppData/Settings"` | Relative to the content root. A path resolving outside it throws at startup rather than loading nothing quietly. |
| `ReloadOnChange` | `true` | Whether editing a file reloads configuration in the running process. |
| `CreateIfMissing` | `true` | Creates the directory **in Development only**, best-effort. An empty directory changes nothing at runtime, so a read-only content root in production is not worth failing a start over — `IOException` and `UnauthorizedAccessException` are swallowed. |

## Notes

- **Call before `Build()`.** Kestrel and the logging providers read configuration while the host is
  being built.
- **A missing settings directory is a valid state** — the host runs on whatever other sources it has.
- **Idempotent.** Repeat calls are ignored, so a host may call `AddAppData()` explicitly even when
  something else already did. The first call's options are the ones that apply.
- Trim- and AOT-safe: no reflection, no dynamic code. Binding your own options still goes through
  the configuration binding source generator as usual.

## License

MIT.
