# Schedule ValueObject

Immutable value object representing a schedule rule with compact binary storage (16 bytes).
Works like cron but with strong typing - null fields mean "any value" (wildcard).

## Features

| Feature | Description |
|---------|-------------|
| **Binary Storage** | Compact 16-byte format for database storage |
| **Two Modes** | Interval (every X time) or Calendar (cron-like) |
| **Nullable Fields** | null = wildcard (any value) |
| **Factory Methods** | `EveryMinutes(5)`, `EveryDay(15, 0)`, etc. |
| **GetNextOccurrence** | Calculate next execution time |
| **JsonConverter** | Native System.Text.Json serialization |
| **TypeConverter** | ASP.NET model binding support |

## Quick Start

```csharp
// Interval mode: every 5 minutes
var schedule = Schedule.EveryMinutes(5);

// Calendar mode: daily at 15:00
var daily = Schedule.EveryDay(15, 0);

// Get next occurrence
DateTimeOffset? next = schedule.GetNextOccurrence(DateTimeOffset.Now);

// Binary storage (16 bytes)
byte[] bytes = schedule.ToBytes();
Schedule restored = Schedule.FromBytes(bytes);
```

## Two Modes

### 1. Interval Mode

Use when you want to repeat every N seconds/minutes/hours/days:

```csharp
// Every 30 seconds
Schedule.EverySeconds(30)

// Every 5 minutes
Schedule.EveryMinutes(5)

// Every 2 hours
Schedule.EveryHours(2)

// Every 1 day
Schedule.EveryDays(1)

// Custom TimeSpan
Schedule.Every(TimeSpan.FromMinutes(90))

// With max executions
Schedule.EveryMinutes(5).WithMaxExecutions(100)
```

### 2. Calendar Mode (Cron-like)

Use when you want to execute at specific times. **Null = any value (wildcard)**.

```csharp
// Every minute at second 0
new Schedule { Second = 0 }

// Every hour at minute 0
new Schedule { Minute = 0, Second = 0 }

// Every day at 15:00:00
new Schedule { Hour = 15, Minute = 0, Second = 0 }
// Or use factory:
Schedule.EveryDay(15, 0)

// Every Monday at 9:00
new Schedule { DayOfWeek = DayOfWeek.Monday, Hour = 9, Minute = 0, Second = 0 }
// Or use factory:
Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)

// 5th of every month at 3:00
new Schedule { DayOfMonth = 5, Hour = 3, Minute = 0, Second = 0 }
// Or use factory:
Schedule.EveryMonth(5, 3, 0)

// Last day of month at midnight
new Schedule { DayOfMonth = -1, Hour = 0, Minute = 0, Second = 0 }

// January 1st at midnight (yearly)
new Schedule { Month = 1, DayOfMonth = 1, Hour = 0, Minute = 0, Second = 0 }
// Or use factory:
Schedule.EveryYear(1, 1, 0, 0)
```

## Multiple Schedules

For multiple execution times (e.g., 8:00 and 18:00), use an array:

```csharp
// Daily at 8:00 and 18:00
Schedule[] morningAndEvening = [
    Schedule.EveryDay(8, 0),
    Schedule.EveryDay(18, 0)
];

// Monday and Friday at 9:00
Schedule[] mondayAndFriday = [
    Schedule.EveryWeek(DayOfWeek.Monday, 9, 0),
    Schedule.EveryWeek(DayOfWeek.Friday, 9, 0)
];

// Weekdays at 9:00
Schedule[] weekdays = [
    Schedule.EveryWeek(DayOfWeek.Monday, 9, 0),
    Schedule.EveryWeek(DayOfWeek.Tuesday, 9, 0),
    Schedule.EveryWeek(DayOfWeek.Wednesday, 9, 0),
    Schedule.EveryWeek(DayOfWeek.Thursday, 9, 0),
    Schedule.EveryWeek(DayOfWeek.Friday, 9, 0)
];
```

## Comparison with Cron

| Cron Expression | Schedule Equivalent |
|-----------------|---------------------|
| `* * * * *` | `new Schedule { Second = 0 }` |
| `*/5 * * * *` | `Schedule.EveryMinutes(5)` |
| `0 15 * * *` | `Schedule.EveryDay(15, 0)` |
| `0 9 * * 1` | `Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)` |
| `0 0 1 * *` | `Schedule.EveryMonth(1, 0, 0)` |
| `0 0 L * *` | `new Schedule { DayOfMonth = -1, Hour = 0, Minute = 0, Second = 0 }` |
| `0 0 1 1 *` | `Schedule.EveryYear(1, 1, 0, 0)` |

## Binary Storage Format

Schedule uses a compact 16-byte binary format optimized for database storage:

```
[1 byte]  Version (1)
[8 bytes] IntervalTicks (Int64, -1 = null)
[1 byte]  Second (-128 = null, 0-59)
[1 byte]  Minute (-128 = null, 0-59)
[1 byte]  Hour (-128 = null, 0-23)
[1 byte]  DayOfMonth (-128 = null, -1 = last, 1-31)
[1 byte]  Month (-128 = null, 1-12)
[1 byte]  DayOfWeek (-128 = null, 0-6)
[1 byte]  Reserved
```

**Advantages over JSON:**
- 16 bytes vs ~100+ bytes JSON
- No parsing overhead
- Direct binary comparison
- No string allocations

```csharp
// Serialize
byte[] bytes = schedule.ToBytes();

// Deserialize
Schedule schedule = Schedule.FromBytes(bytes);

// Store in database as BINARY(16) or VARBINARY
```

## Entity Framework Core Integration

```csharp
// In DbContext.OnModelCreating
modelBuilder.Entity<MyEntity>()
    .Property(e => e.Schedule)
    .HasConversion(
        v => v.ToBytes(),
        v => Schedule.FromBytes(v)
    )
    .HasColumnType("BINARY(16)");

// For multiple schedules (stored as JSON array of Base64)
modelBuilder.Entity<MyEntity>()
    .Property(e => e.Schedules)
    .HasConversion(
        v => JsonSerializer.Serialize(v.Select(s => Convert.ToBase64String(s.ToBytes())).ToArray(), JsonContext.Default.StringArray),
        v => JsonSerializer.Deserialize(v, JsonContext.Default.StringArray)!.Select(b => Schedule.FromBytes(Convert.FromBase64String(b))).ToArray()
    );
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Interval` | `TimeSpan?` | Interval between executions (interval mode) |
| `Second` | `int?` | Second of minute (0-59), null = every |
| `Minute` | `int?` | Minute of hour (0-59), null = every |
| `Hour` | `int?` | Hour of day (0-23), null = every |
| `DayOfMonth` | `int?` | Day of month (1-31, -1 = last), null = every |
| `Month` | `int?` | Month of year (1-12), null = every |
| `DayOfWeek` | `DayOfWeek?` | Day of week, null = every |
| `MaxExecutions` | `int?` | Max executions, null = unlimited |
| `IsInterval` | `bool` | True if interval mode |
| `HasValue` | `bool` | True if schedule is configured |

## Factory Methods

### Interval Mode

| Method | Description |
|--------|-------------|
| `EverySeconds(int)` | Every N seconds |
| `EveryMinutes(int)` | Every N minutes |
| `EveryHours(int)` | Every N hours |
| `EveryDays(int)` | Every N days |
| `Every(TimeSpan)` | Custom interval |

### Calendar Mode

| Method | Description |
|--------|-------------|
| `EveryMinute()` | Every minute at :00 |
| `EveryHour(int atMinute)` | Every hour at specified minute |
| `EveryDay(int hour, int minute)` | Every day at specified time |
| `EveryWeek(DayOfWeek, int hour, int minute)` | Weekly on specified day |
| `EveryMonth(int day, int hour, int minute)` | Monthly on specified day |
| `EveryYear(int month, int day, int hour, int minute)` | Yearly on specified date |

### Modifiers

| Method | Description |
|--------|-------------|
| `WithMaxExecutions(int)` | Limit number of executions |

## GetNextOccurrence

Calculate when the schedule will next trigger:

```csharp
var schedule = Schedule.EveryDay(15, 0);

// Next occurrence after now
DateTimeOffset? next = schedule.GetNextOccurrence(DateTimeOffset.Now);

// With specific timezone
var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
DateTimeOffset? next = schedule.GetNextOccurrence(DateTimeOffset.Now, tz);

// Check if schedule will ever trigger
if (next.HasValue)
{
    Console.WriteLine($"Next: {next.Value}");
}
```

## JSON Serialization

Schedule serializes to compact Base64 by default:

```csharp
var schedule = Schedule.EveryDay(15, 0);
var json = JsonSerializer.Serialize(schedule);
// "AQAAAP////8AgA8AAIA=" (Base64 of 16 bytes)

// Deserialize
var restored = JsonSerializer.Deserialize<Schedule>(json);
```

For verbose JSON (debugging/APIs), use `ScheduleJsonConverter.WriteVerbose`:

```json
{
  "hour": 15,
  "minute": 0,
  "second": 0
}
```

## When to Use Schedule vs Cron String

| Use Schedule | Use Cron String |
|--------------|-----------------|
| ✅ Type safety at compile time | Simple one-off configuration |
| ✅ Binary storage (16 bytes) | Human-readable config files |
| ✅ No parsing errors | External system integration |
| ✅ IntelliSense support | |
| ✅ Validation at creation | |
