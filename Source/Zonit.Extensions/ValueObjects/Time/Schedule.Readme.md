# Schedule

Documentation for `Schedule` now lives in one place:
[`Instruction/extensions/core/schedule.md`](../../../../Instruction/extensions/core/schedule.md)
(installed into a consumer's repo as `.zonit/extensions/core/schedule.md`).

This file previously described a 16-byte binary format. **That is out of date.** The current format is a
fixed **20 bytes** with `MaxExecutions` in bytes 16-19 and an `IsNow` flag in byte 15, and there is no
reader for anything shorter — `Schedule.FromBytes` returns `Schedule.Empty` for a blob below
`Schedule.StorageSize`, silently.

Size a column from `Schedule.StorageSize` (a `static readonly int`, deliberately not a `const`), never
from a literal:

```csharp
modelBuilder.Entity<Job>()
    .Property(j => j.Schedule)
    .HasConversion(v => v.ToBytes(), v => Schedule.FromBytes(v))
    .HasMaxLength(Schedule.StorageSize);   // BINARY(20) / VARBINARY(20)
```

See `schedule.md` for the two modes, the factory list, the byte layout, the JSON shapes, the cron-string
subset that `TryParse` understands, and the migration note for existing `BINARY(16)` columns.
