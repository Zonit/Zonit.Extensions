using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a single schedule rule as an immutable value object.
/// Works like cron: null/default = any value (wildcard), set value = specific time.
/// Use a list/array for multiple schedules (e.g., 8:00 and 18:00 = two Schedule instances).
/// </summary>
/// <remarks>
/// Two Modes:
/// - Interval Mode: Set Interval to repeat every N time. Calendar fields are ignored.
/// - Calendar Mode: Set calendar fields (Second, Minute, Hour, etc.). Null = wildcard (any value).
/// 
/// Binary Storage: Schedule uses a compact 20-byte binary format for database storage
/// (see <see cref="StorageSize"/>). Use ToBytes() and FromBytes() for serialization.
/// 
/// Calendar Mode Examples:
/// - new Schedule { Second = 0 } - Every minute at :00
/// - new Schedule { Minute = 0, Second = 0 } - Every hour at :00:00
/// - new Schedule { Hour = 15, Minute = 0, Second = 0 } - Daily at 15:00:00
/// - new Schedule { DayOfWeek = DayOfWeek.Monday, Hour = 9 } - Every Monday at 9:xx
/// </remarks>
[TypeConverter(typeof(ScheduleTypeConverter))]
[JsonConverter(typeof(ScheduleJsonConverter))]
public readonly record struct Schedule : IEquatable<Schedule>
{
    #region Constants

    /// <summary>
    /// Format tag written as the first byte by <see cref="ToBytes"/> and required by
    /// <see cref="FromBytes(ReadOnlySpan{byte})"/>.
    /// </summary>
    /// <remarks>
    /// This is a shape guard, not a compatibility mechanism. There is exactly one layout and
    /// no reader for any other, so a blob that does not start with this byte is rejected rather
    /// than reinterpreted. Should the layout ever change, bump this and the change is a hard,
    /// visible break — which is the honest outcome for a fixed-width binary column.
    /// </remarks>
    private const byte StorageFormatTag = 1;

    /// <summary>
    /// Binary storage size in bytes (fixed 20 bytes) written by <see cref="ToBytes"/>.
    /// Use this value to size a database column, e.g. <c>BINARY(20)</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately <c>static readonly</c> rather than <c>const</c>. A public <c>const</c> is
    /// inlined into consuming assemblies at THEIR compile time, so a downstream binary built
    /// against an older package would keep using the stale number until recompiled — and this is
    /// precisely the value consumers are told to size a database column with, so the failure
    /// would surface as a truncated column or an <see cref="ArgumentException"/> from
    /// <see cref="WriteToSpan"/> at runtime, with nothing at build time to warn anyone.
    /// </remarks>
    public static readonly int StorageSize = 20;

    /// <summary>
    /// Empty schedule instance (never triggers). Identical to <c>new Schedule()</c>
    /// and to <c>default(Schedule)</c> - see the Fields region for why that holds.
    /// </summary>
    public static readonly Schedule Empty = new();

    // Sentinel values for nullable fields in the *binary* format (what ToBytes writes).
    private const sbyte NullSentinel = -128;
    private const long NullIntervalTicks = -1;

    #endregion

    #region Fields (private backing)

    // Storage encoding: every field below holds its binary-format value XOR-ed with that
    // field's null sentinel, so "no value" is the all-zero bit pattern.
    //
    // WHY: C# skips the parameterless struct constructor for `default(T)`. Storing the raw
    // sentinels (-128 / -1) directly meant default(Schedule), `new Schedule[n]` elements,
    // unassigned fields and EF-materialised NULL columns all read back as 0/Sunday with
    // HasValue == true - a schedule that claims to be configured for midnight on day 0 of
    // month 0, which no date can ever match. XOR-ing with the sentinel is an involution, so
    // encode and decode are the same operation and the zero pattern means "all wildcards".
    private readonly long _intervalTicks;
    private readonly sbyte _second;
    private readonly sbyte _minute;
    private readonly sbyte _hour;
    private readonly sbyte _dayOfMonth;
    private readonly sbyte _month;
    private readonly sbyte _dayOfWeek;
    private readonly int _maxExecutions;
    private readonly byte _flags;

    // Flag bits packed into _flags (byte 15 of binary layout). 0 is a valid "no flags" state,
    // so this field needs no sentinel encoding.
    private const byte FlagIsNow = 0x01;

    /// <summary>Converts between the binary-format value and the stored value (self-inverse).</summary>
    private static sbyte Xor(sbyte value) => (sbyte)(value ^ NullSentinel);

    /// <summary>Converts between the binary-format tick count and the stored one (self-inverse).</summary>
    private static long XorTicks(long value) => value ^ NullIntervalTicks;

    /// <summary>Decodes a stored calendar field to its nullable public value.</summary>
    private static int? Decode(sbyte stored)
    {
        var value = Xor(stored);
        return value == NullSentinel ? null : value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Interval between executions. If set, calendar fields are ignored.
    /// Use for "every N seconds/minutes/hours" schedules.
    /// </summary>
    public TimeSpan? Interval
    {
        get
        {
            var ticks = XorTicks(_intervalTicks);
            return ticks == NullIntervalTicks ? null : TimeSpan.FromTicks(ticks);
        }
        init => _intervalTicks = XorTicks(value?.Ticks ?? NullIntervalTicks);
    }

    /// <summary>
    /// Second of minute (0-59). Null = every second.
    /// </summary>
    public int? Second
    {
        get => Decode(_second);
        init => _second = Xor(value.HasValue ? ValidateRange(value.Value, 0, 59, nameof(Second)) : NullSentinel);
    }

    /// <summary>
    /// Minute of hour (0-59). Null = every minute.
    /// </summary>
    public int? Minute
    {
        get => Decode(_minute);
        init => _minute = Xor(value.HasValue ? ValidateRange(value.Value, 0, 59, nameof(Minute)) : NullSentinel);
    }

    /// <summary>
    /// Hour of day (0-23). Null = every hour.
    /// </summary>
    public int? Hour
    {
        get => Decode(_hour);
        init => _hour = Xor(value.HasValue ? ValidateRange(value.Value, 0, 23, nameof(Hour)) : NullSentinel);
    }

    /// <summary>
    /// Day of month (1-31, or -1 for last day). Null = every day.
    /// </summary>
    public int? DayOfMonth
    {
        get => Decode(_dayOfMonth);
        init => _dayOfMonth = Xor(value.HasValue ? ValidateDayOfMonth(value.Value) : NullSentinel);
    }

    /// <summary>
    /// Month of year (1-12). Null = every month.
    /// </summary>
    public int? Month
    {
        get => Decode(_month);
        init => _month = Xor(value.HasValue ? ValidateRange(value.Value, 1, 12, nameof(Month)) : NullSentinel);
    }

    /// <summary>
    /// Day of week (Sunday = 0, Saturday = 6). Null = every day.
    /// </summary>
    public DayOfWeek? DayOfWeek
    {
        get => Decode(_dayOfWeek) is { } value ? (DayOfWeek)value : null;
        init => _dayOfWeek = Xor(value.HasValue ? (sbyte)value.Value : NullSentinel);
    }

    /// <summary>
    /// Maximum number of executions. Null/0 = unlimited.
    /// </summary>
    public int? MaxExecutions
    {
        get => _maxExecutions == 0 ? null : _maxExecutions;
        init => _maxExecutions = value ?? 0;
    }

    /// <summary>
    /// When <c>true</c>, this schedule fires exactly once - immediately (now), as soon as the
    /// schedule is started (e.g. at application startup). Combine with calendar/interval
    /// schedules to run once immediately plus on a recurring cadence.
    /// </summary>
    public bool IsNow
    {
        get => (_flags & FlagIsNow) != 0;
        init => _flags = value ? (byte)(_flags | FlagIsNow) : (byte)(_flags & ~FlagIsNow);
    }

    /// <summary>
    /// Indicates whether this is an interval-based schedule (vs calendar-based).
    /// </summary>
    public bool IsInterval => XorTicks(_intervalTicks) > 0;

    /// <summary>
    /// Indicates whether this schedule has any meaningful configuration.
    /// </summary>
    public bool HasValue => IsInterval || HasCalendarFields || IsNow;

    /// <summary>
    /// Indicates whether any calendar field is set. A stored 0 is the encoded null sentinel,
    /// so "any field set" is simply "any stored byte non-zero".
    /// </summary>
    private bool HasCalendarFields =>
        _second != 0 ||
        _minute != 0 ||
        _hour != 0 ||
        _dayOfMonth != 0 ||
        _month != 0 ||
        _dayOfWeek != 0;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new schedule with default values (all null = every second).
    /// </summary>
    public Schedule()
    {
        // All-zero *is* the encoded "every field is a wildcard" state (see Fields region).
        // Spelling it out keeps new Schedule() and default(Schedule) provably identical, which
        // is the invariant that Schedule.Empty, array elements and EF NULL columns depend on.
        _intervalTicks = 0;
        _second = 0;
        _minute = 0;
        _hour = 0;
        _dayOfMonth = 0;
        _month = 0;
        _dayOfWeek = 0;
        _maxExecutions = 0;
        _flags = 0;
    }

    /// <summary>
    /// Internal constructor for binary deserialization. Takes values in the *binary format*
    /// encoding (-128 / -1 mean null) and stores them XOR-ed; see the Fields region.
    /// </summary>
    private Schedule(
        long intervalTicks,
        sbyte second,
        sbyte minute,
        sbyte hour,
        sbyte dayOfMonth,
        sbyte month,
        sbyte dayOfWeek,
        int maxExecutions,
        byte flags)
    {
        _intervalTicks = XorTicks(intervalTicks);
        _second = Xor(second);
        _minute = Xor(minute);
        _hour = Xor(hour);
        _dayOfMonth = Xor(dayOfMonth);
        _month = Xor(month);
        _dayOfWeek = Xor(dayOfWeek);
        _maxExecutions = maxExecutions;
        _flags = flags;
    }

    #endregion

    #region Validation

    private static sbyte ValidateRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}.");
        return (sbyte)value;
    }

    private static sbyte ValidateDayOfMonth(int value)
    {
        if (value == -1 || (value >= 1 && value <= 31))
            return (sbyte)value;
        throw new ArgumentOutOfRangeException(nameof(DayOfMonth), value, "Value must be 1-31 or -1 (last day).");
    }

    #endregion

    #region Binary Storage

    /// <summary>
    /// Serializes schedule to compact binary format (20 bytes).
    /// </summary>
    /// <remarks>
    /// <para><strong>Binary format (20 bytes, fixed):</strong></para>
    /// <code>
    /// [1 byte]  Format tag (1)
    /// [8 bytes] IntervalTicks (Int64, -1 = null)
    /// [1 byte]  Second (-128 = null, 0-59)
    /// [1 byte]  Minute (-128 = null, 0-59)
    /// [1 byte]  Hour (-128 = null, 0-23)
    /// [1 byte]  DayOfMonth (-128 = null, -1 = last, 1-31)
    /// [1 byte]  Month (-128 = null, 1-12)
    /// [1 byte]  DayOfWeek (-128 = null, 0-6)
    /// [1 byte]  Flags (bit 0 = IsNow)
    /// [4 bytes] MaxExecutions (Int32, 0 = unlimited)
    /// </code>
    /// <para>Store as <c>BINARY(20)</c> / <c>byte[20]</c>, sizing the column from
    /// <see cref="StorageSize"/> rather than a literal. Anything shorter, or a blob whose first
    /// byte is not the format tag, is rejected by <see cref="FromBytes(ReadOnlySpan{byte})"/> and
    /// yields <see cref="Empty"/> — a malformed blob is never partially reinterpreted.</para>
    /// </remarks>
    public byte[] ToBytes()
    {
        var result = new byte[StorageSize];
        WriteToSpan(result.AsSpan());
        return result;
    }

    /// <summary>
    /// Writes schedule to a span (must be at least <see cref="StorageSize"/> bytes).
    /// </summary>
    public void WriteToSpan(Span<byte> destination)
    {
        if (destination.Length < StorageSize)
            throw new ArgumentException($"Destination must be at least {StorageSize} bytes.", nameof(destination));

        destination[0] = StorageFormatTag;
        BinaryPrimitives.WriteInt64LittleEndian(destination[1..], XorTicks(_intervalTicks));
        destination[9] = (byte)Xor(_second);
        destination[10] = (byte)Xor(_minute);
        destination[11] = (byte)Xor(_hour);
        destination[12] = (byte)Xor(_dayOfMonth);
        destination[13] = (byte)Xor(_month);
        destination[14] = (byte)Xor(_dayOfWeek);
        destination[15] = _flags;
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], _maxExecutions);
    }

    /// <summary>
    /// Deserializes a schedule from the 20-byte binary format. Returns <see cref="Empty"/> for
    /// anything that is not a well-formed blob — wrong length, or a first byte that is not the
    /// format tag.
    /// </summary>
    public static Schedule FromBytes(ReadOnlySpan<byte> bytes)
    {
        // Length and tag are both checked before any field is read: a short or foreign blob must
        // never be partially reinterpreted into a schedule that silently fires at the wrong time.
        if (bytes.Length < StorageSize || bytes[0] != StorageFormatTag)
            return Empty;

        var intervalTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes[1..]);
        var second = (sbyte)bytes[9];
        var minute = (sbyte)bytes[10];
        var hour = (sbyte)bytes[11];
        var dayOfMonth = (sbyte)bytes[12];
        var month = (sbyte)bytes[13];
        var dayOfWeek = (sbyte)bytes[14];
        var flags = bytes[15];
        var maxExecutions = BinaryPrimitives.ReadInt32LittleEndian(bytes[16..]);

        return new Schedule(intervalTicks, second, minute, hour, dayOfMonth, month, dayOfWeek, maxExecutions, flags);
    }

    /// <summary>
    /// Deserializes a schedule from a byte array. Returns <see cref="Empty"/> for <see langword="null"/>
    /// or a malformed blob.
    /// </summary>
    public static Schedule FromBytes(byte[]? bytes)
        => bytes is null ? Empty : FromBytes(bytes.AsSpan());

    #endregion

    #region Static Factory Methods - Interval

    /// <summary>
    /// Creates a one-shot schedule that fires immediately (now) when the schedule starts
    /// (e.g., at application startup) and never again. Combine with other schedules
    /// to run once immediately plus on a recurring cadence.
    /// </summary>
    /// <example>
    /// <code>
    /// // Run once immediately, then every Friday at 22:00
    /// services.AddSchedule&lt;SyncHandler&gt;(
    ///     Schedule.Now(),
    ///     Schedule.EveryWeek(DayOfWeek.Friday, 22));
    /// </code>
    /// </example>
    public static Schedule Now() => new() { IsNow = true };

    /// <summary>
    /// Creates an interval schedule that executes every N seconds.
    /// </summary>
    public static Schedule EverySeconds(int seconds) =>
        new() { Interval = TimeSpan.FromSeconds(seconds) };

    /// <summary>
    /// Creates an interval schedule that executes every N minutes.
    /// </summary>
    public static Schedule EveryMinutes(int minutes) =>
        new() { Interval = TimeSpan.FromMinutes(minutes) };

    /// <summary>
    /// Creates an interval schedule that executes every N hours.
    /// </summary>
    public static Schedule EveryHours(int hours) =>
        new() { Interval = TimeSpan.FromHours(hours) };

    /// <summary>
    /// Creates an interval schedule that executes every N days.
    /// </summary>
    public static Schedule EveryDays(int days) =>
        new() { Interval = TimeSpan.FromDays(days) };

    /// <summary>
    /// Creates an interval schedule from a TimeSpan.
    /// </summary>
    public static Schedule Every(TimeSpan interval) =>
        new() { Interval = interval };

    #endregion

    #region Static Factory Methods - Calendar

    /// <summary>
    /// Creates a schedule that executes every minute at second 0.
    /// </summary>
    public static Schedule EveryMinute() =>
        new() { Second = 0 };

    /// <summary>
    /// Creates a schedule that executes every hour at the specified minute.
    /// </summary>
    public static Schedule EveryHour(int atMinute = 0) =>
        new() { Minute = atMinute, Second = 0 };

    /// <summary>
    /// Creates a schedule that executes every day at the specified time.
    /// </summary>
    public static Schedule EveryDay(int atHour, int atMinute = 0) =>
        new() { Hour = atHour, Minute = atMinute, Second = 0 };

    /// <summary>
    /// Creates a schedule that executes on the specified day of week at the specified time.
    /// </summary>
    public static Schedule EveryWeek(DayOfWeek day, int atHour = 0, int atMinute = 0) =>
        new() { DayOfWeek = day, Hour = atHour, Minute = atMinute, Second = 0 };

    /// <summary>
    /// Creates a schedule that executes on the specified day of month at the specified time.
    /// </summary>
    /// <param name="onDay">Day of month (1-31, or -1 for last day).</param>
    /// <param name="atHour">Hour (0-23).</param>
    /// <param name="atMinute">Minute (0-59).</param>
    public static Schedule EveryMonth(int onDay, int atHour = 0, int atMinute = 0) =>
        new() { DayOfMonth = onDay, Hour = atHour, Minute = atMinute, Second = 0 };

    /// <summary>
    /// Creates a schedule that executes on the specified month and day at the specified time.
    /// </summary>
    public static Schedule EveryYear(int inMonth, int onDay, int atHour = 0, int atMinute = 0) =>
        new() { Month = inMonth, DayOfMonth = onDay, Hour = atHour, Minute = atMinute, Second = 0 };

    #endregion

    #region WithMaxExecutions

    /// <summary>
    /// Returns a new schedule with the specified maximum execution count.
    /// </summary>
    /// <remarks>
    /// Uses <c>with</c> rather than the private constructor because that constructor takes
    /// binary-format values and re-encodes them; copying the already-encoded fields is the
    /// only correct way to preserve them.
    /// </remarks>
    public Schedule WithMaxExecutions(int count) =>
        this with { MaxExecutions = count };

    #endregion

    #region GetNextOccurrence

    /// <summary>
    /// Calculates the next occurrence after the specified time.
    /// </summary>
    /// <param name="after">The time after which to find the next occurrence.</param>
    /// <param name="timeZone">Time zone for calendar calculations (null = Local).</param>
    /// <returns>Next occurrence, or null if no valid occurrence exists.</returns>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after, TimeZoneInfo? timeZone = null)
    {
        if (!HasValue)
            return null;

        // Now-only (one-shot) schedules fire once at Start() and never recur.
        if (IsNow && !IsInterval && !HasCalendarFields)
            return null;

        timeZone ??= TimeZoneInfo.Local;

        if (IsInterval)
        {
            // For interval-based schedules, add interval to the reference time
            var interval = Interval!.Value;
            if (interval <= TimeSpan.Zero)
                return null;

            return after.Add(interval);
        }

        // Calendar-based schedule
        return GetNextCalendarOccurrence(after, timeZone);
    }

    private DateTimeOffset? GetNextCalendarOccurrence(DateTimeOffset after, TimeZoneInfo timeZone)
    {
        // Convert to local time in the target timezone
        var local = TimeZoneInfo.ConvertTime(after, timeZone);

        // Start from the next second
        var candidate = local.AddSeconds(1);
        candidate = new DateTimeOffset(
            candidate.Year, candidate.Month, candidate.Day,
            candidate.Hour, candidate.Minute, candidate.Second, 0,
            candidate.Offset);

        // Limit search to avoid infinite loops (max 4 years for yearly schedules)
        var maxDate = local.AddYears(4);

        while (candidate < maxDate)
        {
            if (MatchesCalendar(candidate))
            {
                return candidate;
            }

            // Advance to next potential match
            candidate = AdvanceToNextPotentialMatch(candidate);
        }

        return null;
    }

    private bool MatchesCalendar(DateTimeOffset dt)
    {
        // Check month
        if (Month.HasValue && dt.Month != Month.Value)
            return false;

        // Check day of month
        if (DayOfMonth.HasValue)
        {
            var targetDay = DayOfMonth.Value;
            if (targetDay == -1)
            {
                // Last day of month
                var lastDay = DateTime.DaysInMonth(dt.Year, dt.Month);
                if (dt.Day != lastDay)
                    return false;
            }
            else if (dt.Day != targetDay)
            {
                return false;
            }
        }

        // Check day of week
        if (DayOfWeek.HasValue && dt.DayOfWeek != DayOfWeek.Value)
            return false;

        // Check hour
        if (Hour.HasValue && dt.Hour != Hour.Value)
            return false;

        // Check minute
        if (Minute.HasValue && dt.Minute != Minute.Value)
            return false;

        // Check second
        if (Second.HasValue && dt.Second != Second.Value)
            return false;

        return true;
    }

    private DateTimeOffset AdvanceToNextPotentialMatch(DateTimeOffset dt)
    {
        // Smart advancement based on which fields are set
        // This is a simplified version - for production, more optimization can be added

        if (Second.HasValue && dt.Second != Second.Value)
        {
            // Advance to the target second in the current or next minute
            var targetSecond = Second.Value;
            if (dt.Second < targetSecond)
            {
                return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, targetSecond, 0, dt.Offset);
            }
            else
            {
                // Need next minute
                return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, 0, dt.Offset).AddMinutes(1)
                    .AddSeconds(targetSecond);
            }
        }

        // Advance by one second as fallback
        return dt.AddSeconds(1);
    }

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(Schedule other) =>
        _intervalTicks == other._intervalTicks &&
        _second == other._second &&
        _minute == other._minute &&
        _hour == other._hour &&
        _dayOfMonth == other._dayOfMonth &&
        _month == other._month &&
        _dayOfWeek == other._dayOfWeek &&
        _maxExecutions == other._maxExecutions &&
        _flags == other._flags;

    /// <inheritdoc />
    public override int GetHashCode() =>
        // _maxExecutions is part of Equals, so it belongs here too - HashCode.Combine tops out
        // at eight arguments, hence the nested combine for the two trailing fields.
        HashCode.Combine(_intervalTicks, _second, _minute, _hour, _dayOfMonth, _month, _dayOfWeek,
            HashCode.Combine(_maxExecutions, _flags));

    #endregion

    #region ToString

    /// <inheritdoc />
    public override string ToString()
    {
        if (!HasValue)
            return "(empty)";

        if (IsNow && !IsInterval && !HasCalendarFields)
            return "Now (fire once)";

        if (IsInterval)
            return $"Every {Interval!.Value}";

        var parts = new List<string>();

        if (IsNow)
            parts.Add("Now");

        if (Month.HasValue)
            parts.Add($"Month={Month.Value}");
        if (DayOfMonth.HasValue)
            parts.Add(DayOfMonth.Value == -1 ? "Day=Last" : $"Day={DayOfMonth.Value}");
        if (DayOfWeek.HasValue)
            parts.Add($"DayOfWeek={DayOfWeek.Value}");
        if (Hour.HasValue)
            parts.Add($"Hour={Hour.Value}");
        if (Minute.HasValue)
            parts.Add($"Minute={Minute.Value}");
        if (Second.HasValue)
            parts.Add($"Second={Second.Value}");

        if (MaxExecutions.HasValue)
            parts.Add($"Max={MaxExecutions.Value}");

        return parts.Count > 0 ? string.Join(", ", parts) : "Every second";
    }

    #endregion

    #region TryParse

    /// <summary>
    /// Tries to parse a schedule from a cron-like string.
    /// Format: "second minute hour dayOfMonth month dayOfWeek" (* = any)
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out Schedule result)
    {
        result = Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts.Length > 6)
            return false;

        try
        {
            // Support 5-field (minute hour day month dayOfWeek) or 6-field (second minute hour day month dayOfWeek)
            int offset = parts.Length == 6 ? 0 : -1;

            int? second = offset >= 0 ? ParseField(parts[0], 0, 59) : 0;
            int? minute = ParseField(parts[1 + offset], 0, 59);
            int? hour = ParseField(parts[2 + offset], 0, 23);
            int? dayOfMonth = ParseField(parts[3 + offset], 1, 31);
            int? month = ParseField(parts[4 + offset], 1, 12);
            int? dayOfWeek = ParseField(parts[5 + offset], 0, 6);

            result = new Schedule
            {
                Second = second,
                Minute = minute,
                Hour = hour,
                DayOfMonth = dayOfMonth,
                Month = month,
                DayOfWeek = dayOfWeek.HasValue ? (DayOfWeek)dayOfWeek.Value : null
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int? ParseField(string field, int min, int max)
    {
        if (field == "*")
            return null;

        if (int.TryParse(field, out var value) && value >= min && value <= max)
            return value;

        throw new FormatException($"Invalid field value: {field}");
    }

    #endregion
}
