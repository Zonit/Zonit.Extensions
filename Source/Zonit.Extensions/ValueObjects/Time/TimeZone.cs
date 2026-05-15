using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;
using BclTimeZoneInfo = System.TimeZoneInfo;

namespace Zonit.Extensions;

/// <summary>
/// Value object representing a time zone for the <c>Zonit.Extensions</c> stack. Supports
/// two ergonomic ways to express a zone:
/// </summary>
/// <remarks>
/// <para><b>1. Named zone</b> — IANA (e.g. <c>"Europe/Warsaw"</c>, <c>"America/New_York"</c>)
/// or Windows (e.g. <c>"Central European Standard Time"</c>) identifier. The runtime
/// (.NET 6+) accepts both on every platform.</para>
///
/// <para><b>2. Fixed offset</b> — <c>+2</c> / <c>-5</c> hours or a precise <see cref="TimeSpan"/>.
/// Internally rendered as <c>"UTC+HH:MM"</c> / <c>"UTC-HH:MM"</c>. <i>No DST.</i></para>
///
/// <para><b>Examples</b></para>
/// <code>
/// var warsaw   = new TimeZone("Europe/Warsaw");   // IANA — DST-aware
/// var fixedUtc = new TimeZone(0);                 // UTC
/// var offset   = new TimeZone(2);                 // UTC+02:00, no DST
/// TimeZone parsed = "America/New_York";           // implicit from string
/// TimeZone fromHours = 4;                          // implicit from int hours
/// var nowInZone = warsaw.ConvertFromUtc(DateTime.UtcNow);
/// </code>
///
/// <para><b>Conversions.</b> Implicit <see cref="string"/> ⇄ <see cref="TimeZone"/> and
/// <see cref="int"/> → <see cref="TimeZone"/> mirror the way <see cref="Culture"/> works,
/// so existing call sites that take or persist a <see cref="string"/> keep compiling.</para>
///
/// <para><b>AOT / trimming.</b> Hand-rolled serialization, no reflection, no dynamic
/// code; safe to ship with <c>IsTrimmable=true</c> and <c>IsAotCompatible=true</c>.</para>
///
/// <para><b>Equality.</b> Ordinal by the rendered <see cref="Value"/>. Two
/// <see cref="TimeZone"/>s representing the same offset but built differently
/// (e.g. <c>new TimeZone(0)</c> vs <c>new TimeZone("UTC")</c>) are equal — the
/// constructor normalises both to <c>"UTC"</c>.</para>
///
/// <para><b>Naming.</b> Yes, this collides with the long-deprecated <see cref="System.TimeZone"/>
/// (replaced by <see cref="System.TimeZoneInfo"/> in .NET Framework 4.6+). Pick whichever
/// is in scope — when both are referenced, fully qualify
/// (<c>Zonit.Extensions.TimeZone</c>).</para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<TimeZone>))]
[JsonConverter(typeof(TimeZoneJsonConverter))]
public readonly struct TimeZone : IEquatable<TimeZone>, IParsable<TimeZone>
{
    /// <summary>Empty / unset time zone (<c>default(TimeZone)</c>).</summary>
    public static readonly TimeZone Empty = default;

    /// <summary>Coordinated Universal Time. Equivalent to <c>new TimeZone("UTC")</c>.</summary>
    public static readonly TimeZone Utc = new("UTC", TimeSpan.Zero, isFixedOffset: true);

    /// <summary>Local time zone of the current machine. Resolved at access; not cached
    /// across calls in case the host changes its zone at runtime.</summary>
    public static TimeZone Local => new(BclTimeZoneInfo.Local.Id, isFixedOffset: false);

    private readonly string? _value;
    private readonly TimeSpan _offset;
    private readonly bool _isFixedOffset;

    /// <summary>
    /// Canonical string representation. For named zones this is the underlying
    /// <see cref="BclTimeZoneInfo.Id"/>; for fixed-offset zones it is
    /// <c>"UTC"</c>, <c>"UTC+HH:MM"</c> or <c>"UTC-HH:MM"</c>. Never <see langword="null"/>.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>True when this instance carries a meaningful zone.</summary>
    public bool HasValue => !string.IsNullOrEmpty(_value);

    /// <summary>True when the zone is a constant offset from UTC (no daylight saving).</summary>
    public bool IsFixedOffset => _isFixedOffset;

    /// <summary>
    /// Standard UTC offset of this zone. For fixed-offset zones this is exact; for
    /// named zones this is the <i>base</i> offset (DST not applied). Use
    /// <see cref="GetUtcOffset(DateTime)"/> when you need the offset at a specific
    /// moment (DST-aware).
    /// </summary>
    public TimeSpan Offset => _offset;

    /// <summary>Private constructor used by the named-id factory path.</summary>
    private TimeZone(string id, bool isFixedOffset)
    {
        _value = id;
        _isFixedOffset = isFixedOffset;

        // For named zones we resolve the base offset eagerly so that .Offset is cheap.
        // Failures here would have already been caught by the public constructor; this
        // private path is only invoked with values that successfully resolved.
        try
        {
            _offset = BclTimeZoneInfo.FindSystemTimeZoneById(id).BaseUtcOffset;
        }
        catch (TimeZoneNotFoundException)
        {
            _offset = TimeSpan.Zero;
        }
        catch (InvalidTimeZoneException)
        {
            _offset = TimeSpan.Zero;
        }
    }

    /// <summary>Private constructor used by the fixed-offset factory path.</summary>
    private TimeZone(string normalisedId, TimeSpan offset, bool isFixedOffset)
    {
        _value = normalisedId;
        _offset = offset;
        _isFixedOffset = isFixedOffset;
    }

    /// <summary>
    /// Creates a time zone from a textual identifier. Accepts:
    /// <list type="bullet">
    ///   <item>IANA id — <c>"Europe/Warsaw"</c></item>
    ///   <item>Windows id — <c>"Central European Standard Time"</c></item>
    ///   <item>Fixed offset — <c>"UTC"</c>, <c>"UTC+2"</c>, <c>"UTC-05:00"</c>, <c>"+02:30"</c>, <c>"-5"</c></item>
    /// </list>
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null/whitespace
    /// or not a recognisable time-zone identifier.</exception>
    public TimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Time zone value must not be empty.", nameof(value));

        if (!TryCreate(value, out var parsed))
            throw new ArgumentException($"'{value}' is not a recognised time zone identifier or offset.", nameof(value));

        _value = parsed._value;
        _offset = parsed._offset;
        _isFixedOffset = parsed._isFixedOffset;
    }

    /// <summary>
    /// Creates a fixed-offset time zone <i>n</i> hours from UTC. Range
    /// <c>-14 .. +14</c> (inclusive) — matches the IANA spec.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hours"/> is outside <c>[-14, 14]</c>.</exception>
    public TimeZone(int hours) : this(TimeSpan.FromHours(ValidateHours(hours)))
    {
    }

    /// <summary>
    /// Creates a fixed-offset time zone from a <see cref="TimeSpan"/>. The offset must
    /// land on a whole-minute boundary and within <c>-14:00 .. +14:00</c>.
    /// </summary>
    public TimeZone(TimeSpan offset)
    {
        if (offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14))
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be within -14:00..+14:00.");

        if (offset.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Offset must be a whole-minute value.", nameof(offset));

        _offset = offset;
        _isFixedOffset = true;
        _value = RenderFixedOffsetId(offset);
    }

    /// <summary>
    /// Resolves this <see cref="TimeZone"/> into a runtime <see cref="BclTimeZoneInfo"/>.
    /// Named zones consult the host's time-zone database; fixed-offset zones are built
    /// on the fly via <see cref="BclTimeZoneInfo.CreateCustomTimeZone(string, TimeSpan, string, string)"/>.
    /// </summary>
    /// <returns>The resolved <see cref="BclTimeZoneInfo"/>, or <see cref="BclTimeZoneInfo.Utc"/> when this is <see cref="Empty"/>.</returns>
    public BclTimeZoneInfo ToTimeZoneInfo()
    {
        if (!HasValue)
            return BclTimeZoneInfo.Utc;

        if (_isFixedOffset)
        {
            return _offset == TimeSpan.Zero
                ? BclTimeZoneInfo.Utc
                : BclTimeZoneInfo.CreateCustomTimeZone(_value!, _offset, _value!, _value!);
        }

        return BclTimeZoneInfo.FindSystemTimeZoneById(_value!);
    }

    /// <summary>Returns the DST-aware UTC offset of this zone at <paramref name="dateTime"/>.</summary>
    public TimeSpan GetUtcOffset(DateTime dateTime)
        => _isFixedOffset ? _offset : ToTimeZoneInfo().GetUtcOffset(dateTime);

    /// <summary>Converts a UTC <see cref="DateTime"/> into this zone's local time.</summary>
    public DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        if (!HasValue)
            return utcDateTime;

        // Ensure the input is interpreted as UTC; ConvertTimeFromUtc requires Kind=Utc or Unspecified.
        var asUtc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return BclTimeZoneInfo.ConvertTimeFromUtc(asUtc, ToTimeZoneInfo());
    }

    /// <summary>Converts a local <see cref="DateTime"/> in this zone back to UTC.</summary>
    public DateTime ConvertToUtc(DateTime localDateTime)
    {
        if (!HasValue)
            return localDateTime;

        var asUnspecified = localDateTime.Kind == DateTimeKind.Unspecified
            ? localDateTime
            : DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        return BclTimeZoneInfo.ConvertTimeToUtc(asUnspecified, ToTimeZoneInfo());
    }

    // ---- conversions ------------------------------------------------------------------

    /// <summary>Implicit conversion to <see cref="string"/> — returns <see cref="Value"/>.</summary>
    public static implicit operator string(TimeZone tz) => tz.Value;

    /// <summary>
    /// Implicit conversion from <see cref="string"/>. Returns <see cref="Empty"/> when the
    /// input is null / blank / unrecognised — by design, to avoid throwing in DTO / model
    /// binding paths. Use the explicit constructor when you want to fail loudly.
    /// </summary>
    public static implicit operator TimeZone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;
        return TryCreate(value, out var tz) ? tz : Empty;
    }

    /// <summary>Implicit conversion from <see cref="int"/> hours offset (e.g. <c>2</c> → UTC+02:00).</summary>
    public static implicit operator TimeZone(int hours) => new(hours);

    /// <summary>Implicit conversion from <see cref="TimeSpan"/> offset.</summary>
    public static implicit operator TimeZone(TimeSpan offset) => new(offset);

    // ---- equality / parsing -----------------------------------------------------------

    /// <inheritdoc />
    public bool Equals(TimeZone other)
        => string.Equals(_value ?? string.Empty, other._value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TimeZone other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => (_value ?? string.Empty).GetHashCode(StringComparison.OrdinalIgnoreCase);

    public static bool operator ==(TimeZone left, TimeZone right) => left.Equals(right);
    public static bool operator !=(TimeZone left, TimeZone right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Attempts to build a <see cref="TimeZone"/> from <paramref name="value"/>. Returns
    /// <see langword="false"/> (and <see cref="Empty"/>) when the input is not parseable;
    /// never throws. Mirrors <see cref="Culture.TryCreate(string?, out Culture)"/>.
    /// </summary>
    public static bool TryCreate(string? value, out TimeZone timeZone)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            timeZone = Empty;
            return false;
        }

        var trimmed = value.Trim();

        // 1. Bare "UTC" / "GMT" → zero-offset zone.
        if (trimmed.Equals("UTC", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("GMT", StringComparison.OrdinalIgnoreCase))
        {
            timeZone = Utc;
            return true;
        }

        // 2. Fixed offset: "UTC+X[:MM]", "+X[:MM]", "-X[:MM]", "+H", "-H", "0".
        if (TryParseOffset(trimmed, out var offset))
        {
            timeZone = new TimeZone(offset);
            return true;
        }

        // 3. IANA / Windows id — final fallback. Both `Europe/Warsaw` and
        //    `Central European Standard Time` go through the same call; .NET 6+ resolves
        //    both on every platform.
        try
        {
            var info = BclTimeZoneInfo.FindSystemTimeZoneById(trimmed);
            timeZone = new TimeZone(info.Id, isFixedOffset: false);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = Empty;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = Empty;
            return false;
        }
    }

    /// <inheritdoc />
    public static TimeZone Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"'{s}' is not a valid TimeZone.");

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TimeZone result)
        => TryCreate(s, out result);

    // ---- helpers ----------------------------------------------------------------------

    private static int ValidateHours(int hours)
    {
        if (hours < -14 || hours > 14)
            throw new ArgumentOutOfRangeException(nameof(hours), hours, "Time-zone offset hours must be within -14..14.");
        return hours;
    }

    /// <summary>
    /// Parses fixed-offset notations into a <see cref="TimeSpan"/>. Accepted shapes:
    /// <c>UTC</c> already handled above; <c>UTC+H[:MM]</c>, <c>+H[:MM]</c>, <c>-H[:MM]</c>,
    /// bare integer like <c>-5</c> or <c>0</c>.
    /// </summary>
    private static bool TryParseOffset(string text, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;

        // Strip leading "UTC" / "GMT" prefix if present.
        var span = text.AsSpan();
        if (span.StartsWith("UTC", StringComparison.OrdinalIgnoreCase)) span = span[3..];
        else if (span.StartsWith("GMT", StringComparison.OrdinalIgnoreCase)) span = span[3..];
        span = span.Trim();

        if (span.IsEmpty)
        {
            // Only "UTC" or "GMT" was passed — caller already handled that, but be defensive.
            return true;
        }

        // Determine sign.
        var sign = 1;
        if (span[0] == '+') span = span[1..];
        else if (span[0] == '-') { sign = -1; span = span[1..]; }

        if (span.IsEmpty) return false;

        // Two acceptable shapes from here: "H[:MM]" or just "H" (or "HH").
        int colon = span.IndexOf(':');
        int hours;
        int minutes = 0;

        if (colon >= 0)
        {
            if (!int.TryParse(span[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours))
                return false;
            if (!int.TryParse(span[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
                return false;
        }
        else
        {
            if (!int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out hours))
                return false;
        }

        if (hours < 0 || hours > 14 || minutes < 0 || minutes >= 60)
            return false;

        offset = new TimeSpan(sign * hours, sign * minutes, 0);

        // Final range guard (covers -14:00..+14:00 inclusive).
        return offset >= TimeSpan.FromHours(-14) && offset <= TimeSpan.FromHours(14);
    }

    /// <summary>Renders a fixed offset as <c>"UTC"</c>, <c>"UTC+HH:MM"</c> or <c>"UTC-HH:MM"</c>.</summary>
    private static string RenderFixedOffsetId(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";
        var sign = offset.Ticks < 0 ? '-' : '+';
        var abs = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}");
    }
}

/// <summary>
/// AOT-safe JSON converter for <see cref="TimeZone"/>. Reads any string the
/// <see cref="TimeZone.TryCreate(string?, out TimeZone)"/> grammar accepts; writes the
/// canonical <see cref="TimeZone.Value"/>.
/// </summary>
public sealed class TimeZoneJsonConverter : JsonConverter<TimeZone>
{
    /// <inheritdoc />
    public override TimeZone Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return TimeZone.TryCreate(value, out var tz) ? tz : TimeZone.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeZone value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
