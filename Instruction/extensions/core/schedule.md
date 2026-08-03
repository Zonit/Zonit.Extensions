# Schedule

`Zonit.Extensions.Schedule` is a `readonly record struct` describing **one** recurrence rule. For two
firing times (08:00 and 18:00) you use two `Schedule` values, not one.

Nothing to register — it is a value object like the rest of the package, and follows the general
contract in `.zonit/extensions/core/value-objects.md`.

## Read this first

- **Always construct with a factory or `new Schedule { … }`.** The only public constructor is the
  parameterless one; the field-taking one is private and takes *binary-format* values, not the ones you
  read back from the properties.
- **`null` means wildcard.** `new Schedule { Hour = 15 }` fires at 15:xx:xx — every minute and every
  second of that hour. Add `Minute = 0, Second = 0` unless you mean that.
- **The binary format is a fixed 20 bytes and there is exactly one version.** A blob shorter than
  `Schedule.StorageSize` is **rejected**, not upgraded — `FromBytes` returns `Schedule.Empty` with no
  exception and no log. Widen the column before deploying; see the migration note below.
- **Size the database column from `Schedule.StorageSize`.** It is `static readonly`, not `const`, so it
  is deliberately *not* inlined into your assembly. Never write the literal `20`.

## Two modes

### Interval mode — "every N"

Set `Interval`. Every calendar field is ignored.

```csharp
Schedule.EverySeconds(30);
Schedule.EveryMinutes(5);
Schedule.EveryHours(2);
Schedule.EveryDays(1);
Schedule.Every(TimeSpan.FromMinutes(90));

Schedule.EveryMinutes(5).IsInterval;   // true
Schedule.EveryMinutes(5).ToString();   // "Every 00:05:00"
```

An interval of zero or less is not an interval: `Schedule.EverySeconds(0).IsInterval` is `false` and
`HasValue` is `false`.

### Calendar mode — cron-like, null = any

```csharp
Schedule.EveryMinute();                       // Second=0            -> every minute at :00
Schedule.EveryHour(atMinute: 0);              // Minute=0, Second=0
Schedule.EveryDay(15, 30);                    // Hour=15, Minute=30, Second=0
Schedule.EveryWeek(DayOfWeek.Monday, 9);      // + DayOfWeek
Schedule.EveryMonth(onDay: 5, atHour: 3);     // + DayOfMonth
Schedule.EveryYear(inMonth: 1, onDay: 1);     // + Month

// Or spell it out — the properties are init-only.
var lastDayOfMonth = new Schedule { DayOfMonth = -1, Hour = 0, Minute = 0, Second = 0 };
```

| property | type | range | `null` means |
|---|---|---|---|
| `Interval` | `TimeSpan?` | > 0 | not interval mode |
| `Second` | `int?` | 0-59 | every second |
| `Minute` | `int?` | 0-59 | every minute |
| `Hour` | `int?` | 0-23 | every hour |
| `DayOfMonth` | `int?` | 1-31, or **-1 = last day** | every day |
| `Month` | `int?` | 1-12 | every month |
| `DayOfWeek` | `DayOfWeek?` | Sunday(0)-Saturday(6) | every day |
| `MaxExecutions` | `int?` | ≥ 0 | unlimited (`0` is stored as `null`) |
| `IsNow` | `bool` | — | (not nullable) |

Out-of-range values throw `ArgumentOutOfRangeException` **from the initializer**:

```csharp
var bad = new Schedule { Hour = 25 };   // ArgumentOutOfRangeException
```

### One-shot: `Schedule.Now()`

Fires once, immediately, when the schedule is started (typically at application startup), then never
again — `GetNextOccurrence` returns `null` for a Now-only schedule.

```csharp
Schedule.Now().IsNow;                                  // true
Schedule.Now().GetNextOccurrence(DateTimeOffset.UtcNow);   // null
```

Combine it with a recurring rule to get "once now, then on the cadence":

```csharp
// Two separate schedules — the usual shape:
Schedule[] both = [Schedule.Now(), Schedule.EveryWeek(DayOfWeek.Friday, 22)];

// Or fold the flag into one value:
var oneValue = Schedule.EveryDay(15) with { IsNow = true };
oneValue.ToString();   // "Now, Hour=15, Minute=0, Second=0"
```

### Limiting executions

```csharp
var capped = Schedule.EveryMinutes(5).WithMaxExecutions(3);
capped.MaxExecutions;   // 3
```

`WithMaxExecutions` uses `with`, so it preserves everything else correctly. `MaxExecutions` **does**
round-trip through both the binary format and JSON — it occupies bytes 16-19.

## Empty and default

`Schedule.Empty`, `new Schedule()` and `default(Schedule)` are the same value and mean "never fires":

```csharp
Schedule.Empty == default(Schedule);   // true
Schedule.Empty.HasValue;               // false
Schedule.Empty.ToString();             // "(empty)"
new Schedule().Second;                 // null
```

This holds for array elements (`new Schedule[10]`), unassigned fields, and EF-materialised `NULL`
columns — all of them read back as all-wildcards with `HasValue == false`, never as "midnight on day 0".

`HasValue` is `true` when the schedule is an interval, has any calendar field set, or is `IsNow`.

## Computing the next run

```csharp
DateTimeOffset? next = Schedule.EveryDay(15, 30)
    .GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
```

- Second argument is the time zone for calendar evaluation; `null` means `TimeZoneInfo.Local`. Pass one
  explicitly on a server — the local zone of a container is rarely what a business rule means.
- Interval mode simply returns `after + Interval`; it does not align to wall-clock boundaries.
- Calendar mode scans forward second by second and gives up after 4 years, returning `null`. A rule that
  can never match (e.g. `Month = 2, DayOfMonth = 30`) therefore returns `null` rather than hanging.
- `HasValue == false` returns `null`.

## Binary format: fixed 20 bytes

```
offset  size  field
     0     1  format tag (always 1)
     1     8  IntervalTicks   Int64 little-endian, -1 = null
     9     1  Second          -128 = null, else 0-59
    10     1  Minute          -128 = null, else 0-59
    11     1  Hour            -128 = null, else 0-23
    12     1  DayOfMonth      -128 = null, -1 = last, else 1-31
    13     1  Month           -128 = null, else 1-12
    14     1  DayOfWeek       -128 = null, else 0-6
    15     1  Flags           bit 0 = IsNow
    16     4  MaxExecutions   Int32 little-endian, 0 = unlimited
```

```csharp
byte[] blob = schedule.ToBytes();               // always Schedule.StorageSize bytes
Schedule back = Schedule.FromBytes(blob);
back == schedule;                               // true

Span<byte> buffer = stackalloc byte[Schedule.StorageSize];
schedule.WriteToSpan(buffer);                   // ArgumentException if shorter than StorageSize
```

`FromBytes` validates **length and tag before reading any field**, so a malformed blob is never
partially reinterpreted into a schedule that fires at the wrong time:

```csharp
Schedule.FromBytes((byte[]?)null).HasValue;      // false — Empty
Schedule.FromBytes(new byte[16]).HasValue;       // false — too short
Schedule.FromBytes(new byte[20]).HasValue;       // false — tag byte is 0, not 1
```

### EF Core

```csharp
modelBuilder.Entity<Job>()
    .Property(j => j.Schedule)
    .HasConversion(v => v.ToBytes(), v => Schedule.FromBytes(v))
    .HasMaxLength(Schedule.StorageSize);          // BINARY(20) / VARBINARY(20)
```

Size the column from `Schedule.StorageSize`, never from a literal. It is `static readonly` on purpose: a
public `const` would be baked into your assembly at *your* compile time, so a downstream binary built
against an older package would keep using the stale number and fail at runtime with
`ArgumentException: Destination must be at least 20 bytes` — with nothing at build time to warn anyone.

**Migrating from a 16-byte column:** widen it to `BINARY(20)` / `VARBINARY(20)` **before** deploying.
The first 16 bytes are unchanged and the tag byte is still `1`, so a row that gets right-padded with
zeros on widening (which is what SQL Server does to a `BINARY` column) reads back correctly with
`MaxExecutions == null` — which is what those rows always meant. What does *not* work is a value that
is still physically 16 bytes long when it reaches `FromBytes`: that is rejected as `Schedule.Empty`,
silently. So if the storage layer can hand you a short array (`VARBINARY`, a blob store, a JSON column
holding old base64), rewrite those rows from their source definition rather than relying on padding.
Callers of `WriteToSpan` must enlarge their buffer to `Schedule.StorageSize`.

## JSON

`ScheduleJsonConverter` writes **base64 of the 20 bytes**, and `null` for an empty schedule:

```csharp
JsonSerializer.Serialize(Schedule.EveryMinutes(5).WithMaxExecutions(3));
// "AQBe0LIAAAAAgICAgICAAAMAAAA="

JsonSerializer.Serialize(Schedule.Empty);   // null
```

Reads accept three shapes:

```csharp
JsonSerializer.Deserialize<Schedule>("\"AQBe0LIAAAAAgICAgICAAAMAAAA=\"");   // base64 blob
JsonSerializer.Deserialize<Schedule>("\"0 0 15 * * *\"");                   // cron-like string
JsonSerializer.Deserialize<Schedule>("""{"hour":15,"minute":0,"second":0,"maxExecutions":7}""");
```

Object-form keys are case-insensitive and accept aliases: `dayOfMonth`/`day`, `dayOfWeek`/`weekday`,
`maxExecutions`/`max`. `interval` takes a `TimeSpan` string or a tick count. The object form does **not**
read `isNow` — a `IsNow` schedule survives only through the base64 form.

`ScheduleJsonConverter.WriteVerbose(writer, schedule)` is a public static helper if you want the readable
object shape on an API surface; it is not wired into the default converter.

## Cron string parsing

```csharp
Schedule.TryParse("0 0 15 * * *", out var s);   // 6 fields: second minute hour day month dayOfWeek
Schedule.TryParse("0 15 * * *", out var s2);    // 5 fields: minute hour day month dayOfWeek (second = 0)
s.ToString();                                   // "Hour=15, Minute=0, Second=0"
```

Only `*` and a plain integer are understood per field. **Step (`*/5`), ranges (`1-5`) and lists (`1,3,5`)
are not supported** and make `TryParse` return `false`:

```csharp
Schedule.TryParse("*/5 * * * *", out _);   // false — use Schedule.EveryMinutes(5)
```

`ScheduleTypeConverter` (used by model binding and configuration) converts from `byte[]` and from a cron
string, and converts to `byte[]` or to `ToString()`. Note the asymmetry: `ConvertTo(string)` produces the
human-readable form (`"Hour=15, Minute=0, Second=0"`), which `ConvertFrom` cannot read back. Round-trip
through `byte[]`.

## Comparison with cron

| cron | Schedule |
|---|---|
| `* * * * *` | `Schedule.EveryMinute()` |
| `*/5 * * * *` | `Schedule.EveryMinutes(5)` (interval mode — not the same alignment) |
| `0 15 * * *` | `Schedule.EveryDay(15, 0)` |
| `0 9 * * 1` | `Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)` |
| `0 0 1 * *` | `Schedule.EveryMonth(1, 0, 0)` |
| `0 0 L * *` | `new Schedule { DayOfMonth = -1, Hour = 0, Minute = 0, Second = 0 }` |
| `0 0 1 1 *` | `Schedule.EveryYear(1, 1, 0, 0)` |

`*/5` and `EveryMinutes(5)` are not equivalent: cron fires at :00, :05, :10 …; interval mode fires five
minutes after whatever the previous run was. Use `new Schedule { Minute = 0, Second = 0 }`-style calendar
rules when alignment to the clock matters.

## Known limitations

- There is no explicit reader for the older 16-byte layout. The bytes happen to line up when the storage
  layer zero-pads to 20, but anything that hands `FromBytes` a genuinely short array deserializes to
  `Schedule.Empty` silently, with no exception and no log. Audit any persisted column before upgrading.
- `Schedule.GetHashCode()` folds in `MaxExecutions` and the flags byte, so hash values differ from
  earlier releases. Fine for in-memory dictionaries; never persist a `Schedule` hash code.
- Calendar evaluation advances one second at a time in the general case, so `GetNextOccurrence` on a rule
  that only matches once a year does real work (bounded at 4 years of candidates). Cache the result
  rather than recomputing it in a tight loop.
- `TryParse`'s signature is `TryParse(string?, out Schedule)` — two parameters, no `IFormatProvider`.
  `Schedule` does not implement `IParsable<Schedule>`.
