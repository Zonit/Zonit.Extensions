using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Converters;

/// <summary>
/// Type converter for Schedule value object.
/// Supports conversion from/to byte[] for model binding and storage.
/// </summary>
public sealed class ScheduleTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(byte[]) ||
        sourceType == typeof(string) ||
        base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(byte[]) ||
        destinationType == typeof(string) ||
        base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value switch
        {
            byte[] bytes => Schedule.FromBytes(bytes),
            string str when Schedule.TryParse(str, out var schedule) => schedule,
            string str when string.IsNullOrWhiteSpace(str) => Schedule.Empty,
            _ => base.ConvertFrom(context, culture, value)
        };
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Schedule schedule)
        {
            if (destinationType == typeof(byte[]))
                return schedule.ToBytes();
            if (destinationType == typeof(string))
                return schedule.ToString();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>
/// JSON converter for Schedule value object.
/// Supports both compact binary (Base64) and verbose object format.
/// </summary>
public sealed class ScheduleJsonConverter : JsonConverter<Schedule>
{
    /// <inheritdoc />
    public override Schedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Schedule.Empty;

        // Handle Base64 string (compact binary format)
        if (reader.TokenType == JsonTokenType.String)
        {
            var base64 = reader.GetString();
            if (string.IsNullOrEmpty(base64))
                return Schedule.Empty;

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return Schedule.FromBytes(bytes);
            }
            catch
            {
                // Try as cron-like string
                if (Schedule.TryParse(base64, out var schedule))
                    return schedule;
                return Schedule.Empty;
            }
        }

        // Handle object format
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected null, string, or object for Schedule.");

        TimeSpan? interval = null;
        int? second = null;
        int? minute = null;
        int? hour = null;
        int? dayOfMonth = null;
        int? month = null;
        DayOfWeek? dayOfWeek = null;
        int? maxExecutions = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "interval":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        if (TimeSpan.TryParse(reader.GetString(), out var ts))
                            interval = ts;
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        interval = TimeSpan.FromTicks(reader.GetInt64());
                    }
                    break;
                case "second":
                    if (reader.TokenType == JsonTokenType.Number)
                        second = reader.GetInt32();
                    break;
                case "minute":
                    if (reader.TokenType == JsonTokenType.Number)
                        minute = reader.GetInt32();
                    break;
                case "hour":
                    if (reader.TokenType == JsonTokenType.Number)
                        hour = reader.GetInt32();
                    break;
                case "dayofmonth":
                case "day":
                    if (reader.TokenType == JsonTokenType.Number)
                        dayOfMonth = reader.GetInt32();
                    break;
                case "month":
                    if (reader.TokenType == JsonTokenType.Number)
                        month = reader.GetInt32();
                    break;
                case "dayofweek":
                case "weekday":
                    if (reader.TokenType == JsonTokenType.Number)
                        dayOfWeek = (DayOfWeek)reader.GetInt32();
                    else if (reader.TokenType == JsonTokenType.String && 
                             Enum.TryParse<DayOfWeek>(reader.GetString(), true, out var dow))
                        dayOfWeek = dow;
                    break;
                case "maxexecutions":
                case "max":
                    if (reader.TokenType == JsonTokenType.Number)
                        maxExecutions = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new Schedule
        {
            Interval = interval,
            Second = second,
            Minute = minute,
            Hour = hour,
            DayOfMonth = dayOfMonth,
            Month = month,
            DayOfWeek = dayOfWeek,
            MaxExecutions = maxExecutions
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Schedule value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        // Write as compact Base64 by default for efficiency
        // Can be changed to verbose object format if needed
        writer.WriteBase64StringValue(value.ToBytes());
    }

    /// <summary>
    /// Writes schedule as verbose JSON object (for debugging/APIs).
    /// </summary>
    public static void WriteVerbose(Utf8JsonWriter writer, Schedule value)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        if (value.IsInterval)
        {
            writer.WriteString("interval", value.Interval!.Value.ToString());
        }
        else
        {
            if (value.Second.HasValue)
                writer.WriteNumber("second", value.Second.Value);
            if (value.Minute.HasValue)
                writer.WriteNumber("minute", value.Minute.Value);
            if (value.Hour.HasValue)
                writer.WriteNumber("hour", value.Hour.Value);
            if (value.DayOfMonth.HasValue)
                writer.WriteNumber("dayOfMonth", value.DayOfMonth.Value);
            if (value.Month.HasValue)
                writer.WriteNumber("month", value.Month.Value);
            if (value.DayOfWeek.HasValue)
                writer.WriteString("dayOfWeek", value.DayOfWeek.Value.ToString());
        }

        if (value.MaxExecutions.HasValue)
            writer.WriteNumber("maxExecutions", value.MaxExecutions.Value);

        writer.WriteEndObject();
    }
}
