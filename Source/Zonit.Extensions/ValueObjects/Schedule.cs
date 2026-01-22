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
/// Binary Storage: Schedule uses compact 16-byte binary format for database storage.
/// Use ToBytes() and FromBytes() for serialization.
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
    /// Binary storage format version.
    /// </summary>
    private const byte StorageVersion = 1;

    /// <summary>
    /// Binary storage size in bytes (fixed 16 bytes).
    /// </summary>
    public const int StorageSize = 16;

    /// <summary>
    /// Empty schedule instance (never triggers).
    /// </summary>
    public static readonly Schedule Empty = default;

    // Sentinel values for nullable fields in binary format
    private const sbyte NullSentinel = -128;
    private const long NullIntervalTicks = -1;

    #endregion

    #region Fields (private backing)

    private readonly long _intervalTicks;
    private readonly sbyte _second;
    private readonly sbyte _minute;
    private readonly sbyte _hour;
    private readonly sbyte _dayOfMonth;
    private readonly sbyte _month;
    private readonly sbyte _dayOfWeek;
    private readonly int _maxExecutions;

    #endregion

    #region Properties

    /// <summary>
    /// Interval between executions. If set, calendar fields are ignored.
    /// Use for "every N seconds/minutes/hours" schedules.
    /// </summary>
    public TimeSpan? Interval
    {
        get => _intervalTicks == NullIntervalTicks ? null : TimeSpan.FromTicks(_intervalTicks);
        init => _intervalTicks = value?.Ticks ?? NullIntervalTicks;
    }

    /// <summary>
    /// Second of minute (0-59). Null = every second.
    /// </summary>
    public int? Second
    {
        get => _second == NullSentinel ? null : _second;
        init => _second = value.HasValue ? ValidateRange(value.Value, 0, 59, nameof(Second)) : NullSentinel;
    }

    /// <summary>
    /// Minute of hour (0-59). Null = every minute.
    /// </summary>
    public int? Minute
    {
        get => _minute == NullSentinel ? null : _minute;
        init => _minute = value.HasValue ? ValidateRange(value.Value, 0, 59, nameof(Minute)) : NullSentinel;
    }

    /// <summary>
    /// Hour of day (0-23). Null = every hour.
    /// </summary>
    public int? Hour
    {
        get => _hour == NullSentinel ? null : _hour;
        init => _hour = value.HasValue ? ValidateRange(value.Value, 0, 23, nameof(Hour)) : NullSentinel;
    }

    /// <summary>
    /// Day of month (1-31, or -1 for last day). Null = every day.
    /// </summary>
    public int? DayOfMonth
    {
        get => _dayOfMonth == NullSentinel ? null : _dayOfMonth;
        init => _dayOfMonth = value.HasValue ? ValidateDayOfMonth(value.Value) : NullSentinel;
    }

    /// <summary>
    /// Month of year (1-12). Null = every month.
    /// </summary>
    public int? Month
    {
        get => _month == NullSentinel ? null : _month;
        init => _month = value.HasValue ? ValidateRange(value.Value, 1, 12, nameof(Month)) : NullSentinel;
    }

    /// <summary>
    /// Day of week (Sunday = 0, Saturday = 6). Null = every day.
    /// </summary>
    public DayOfWeek? DayOfWeek
    {
        get => _dayOfWeek == NullSentinel ? null : (DayOfWeek)_dayOfWeek;
        init => _dayOfWeek = value.HasValue ? (sbyte)value.Value : NullSentinel;
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
    /// Indicates whether this is an interval-based schedule (vs calendar-based).
    /// </summary>
    public bool IsInterval => _intervalTicks != NullIntervalTicks && _intervalTicks > 0;

    /// <summary>
    /// Indicates whether this schedule has any meaningful configuration.
    /// </summary>
    public bool HasValue => IsInterval || HasCalendarFields;

    /// <summary>
    /// Indicates whether any calendar field is set.
    /// </summary>
    private bool HasCalendarFields =>
        _second != NullSentinel ||
        _minute != NullSentinel ||
        _hour != NullSentinel ||
        _dayOfMonth != NullSentinel ||
        _month != NullSentinel ||
        _dayOfWeek != NullSentinel;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new schedule with default values (all null = every second).
    /// </summary>
    public Schedule()
    {
        _intervalTicks = NullIntervalTicks;
        _second = NullSentinel;
        _minute = NullSentinel;
        _hour = NullSentinel;
        _dayOfMonth = NullSentinel;
        _month = NullSentinel;
        _dayOfWeek = NullSentinel;
        _maxExecutions = 0;
    }

    /// <summary>
    /// Internal constructor for binary deserialization.
    /// </summary>
    private Schedule(
        long intervalTicks,
        sbyte second,
        sbyte minute,
        sbyte hour,
        sbyte dayOfMonth,
        sbyte month,
        sbyte dayOfWeek,
        int maxExecutions)
    {
        _intervalTicks = intervalTicks;
        _second = second;
        _minute = minute;
        _hour = hour;
        _dayOfMonth = dayOfMonth;
        _month = month;
        _dayOfWeek = dayOfWeek;
        _maxExecutions = maxExecutions;
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
    /// Serializes schedule to compact binary format (16 bytes).
    /// </summary>
    /// <remarks>
    /// <para><strong>Binary Format V1 (16 bytes):</strong></para>
    /// <code>
    /// [1 byte]  Version (1)
    /// [8 bytes] IntervalTicks (Int64, -1 = null)
    /// [1 byte]  Second (-128 = null, 0-59)
    /// [1 byte]  Minute (-128 = null, 0-59)
    /// [1 byte]  Hour (-128 = null, 0-23)
    /// [1 byte]  DayOfMonth (-128 = null, -1 = last, 1-31)
    /// [1 byte]  Month (-128 = null, 1-12)
    /// [1 byte]  DayOfWeek (-128 = null, 0-6)
    /// [1 byte]  Reserved (for future use)
    /// </code>
    /// <para>Note: MaxExecutions stored separately if needed.</para>
    /// </remarks>
    public byte[] ToBytes()
    {
        var result = new byte[StorageSize];
        WriteToSpan(result.AsSpan());
        return result;
    }

    /// <summary>
    /// Writes schedule to a span (must be at least 16 bytes).
    /// </summary>
    public void WriteToSpan(Span<byte> destination)
    {
        if (destination.Length < StorageSize)
            throw new ArgumentException($"Destination must be at least {StorageSize} bytes.", nameof(destination));

        destination[0] = StorageVersion;
        BinaryPrimitives.WriteInt64LittleEndian(destination[1..], _intervalTicks);
        destination[9] = (byte)_second;
        destination[10] = (byte)_minute;
        destination[11] = (byte)_hour;
        destination[12] = (byte)_dayOfMonth;
        destination[13] = (byte)_month;
        destination[14] = (byte)_dayOfWeek;
        destination[15] = 0; // Reserved
    }

    /// <summary>
    /// Deserializes schedule from binary format.
    /// </summary>
    public static Schedule FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < StorageSize)
            return Empty;

        var version = bytes[0];
        if (version != StorageVersion)
            return Empty; // Unknown version

        var intervalTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes[1..]);
        var second = (sbyte)bytes[9];
        var minute = (sbyte)bytes[10];
        var hour = (sbyte)bytes[11];
        var dayOfMonth = (sbyte)bytes[12];
        var month = (sbyte)bytes[13];
        var dayOfWeek = (sbyte)bytes[14];
        // bytes[15] reserved

        return new Schedule(intervalTicks, second, minute, hour, dayOfMonth, month, dayOfWeek, 0);
    }

    /// <summary>
    /// Deserializes schedule from byte array.
    /// </summary>
    public static Schedule FromBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < StorageSize)
            return Empty;
        return FromBytes(bytes.AsSpan());
    }

    #endregion

    #region Static Factory Methods - Interval

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
    public Schedule WithMaxExecutions(int count) =>
        new(_intervalTicks, _second, _minute, _hour, _dayOfMonth, _month, _dayOfWeek, count);

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
        _maxExecutions == other._maxExecutions;

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(_intervalTicks, _second, _minute, _hour, _dayOfMonth, _month, _dayOfWeek);

    #endregion

    #region ToString

    /// <inheritdoc />
    public override string ToString()
    {
        if (!HasValue)
            return "(empty)";

        if (IsInterval)
            return $"Every {Interval!.Value}";

        var parts = new List<string>();

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
