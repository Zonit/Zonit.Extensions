using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a culture in language format (e.g., "en-US", "pl-PL").
/// Note: default(Culture) has Value = null. Use Culture.Default for "en-US".
/// </summary>
[TypeConverter(typeof(ValueObjectTypeConverter<Culture>))]
[JsonConverter(typeof(CultureJsonConverter))]
public readonly struct Culture : IEquatable<Culture>, IComparable<Culture>, IParsable<Culture>
{
    /// <summary>
    /// Default culture (en-US). Use this when you need a valid default culture.
    /// Note: default(Culture) is different - it has Value = null.
    /// </summary>
    public static readonly Culture Default = new("en-US");

    /// <summary>
    /// Empty culture (default value for optional scenarios). Same as default(Culture).
    /// </summary>
    public static readonly Culture Empty = default;

    /// <summary>
    /// The culture value in language format (e.g., "en-US").
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Indicates whether the culture has a value.
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Gets the value or returns "en-US" if empty.
    /// Use this when you need a guaranteed valid culture code.
    /// </summary>
    public string ValueOrDefault => HasValue ? Value : "en-US";

    /// <summary>
    /// Creates a new culture based on the specified language code.
    /// </summary>
    /// <param name="value">Culture code in language format (e.g., "en-US", "pl-PL").</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="CultureNotFoundException">Thrown when <paramref name="value"/> is not a valid culture code.</exception>
    public Culture(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        try
        {
            var cultureInfo = new CultureInfo(value);
            Value = cultureInfo.Name;
        }
        catch (CultureNotFoundException ex)
        {
            throw new CultureNotFoundException($"Culture '{value}' is not a valid culture code.", ex);
        }
    }

    /// <summary>
    /// Creates a culture from a CultureInfo object.
    /// </summary>
    /// <param name="cultureInfo">CultureInfo object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultureInfo"/> is null.</exception>
    public Culture(CultureInfo cultureInfo)
    {
        ArgumentNullException.ThrowIfNull(cultureInfo, nameof(cultureInfo));
        Value = cultureInfo.Name;
    }

    /// <summary>
    /// Converts culture to a CultureInfo object. Returns null if empty, or CultureInfo for "en-US" as fallback.
    /// </summary>
    public CultureInfo? ToCultureInfo() => HasValue ? new CultureInfo(Value) : null;

    /// <summary>
    /// Converts culture to a CultureInfo object. Returns "en-US" CultureInfo if empty.
    /// </summary>
    public CultureInfo ToCultureInfoOrDefault() => new CultureInfo(ValueOrDefault);


    /// <summary>
    /// Gets the two-letter language code (e.g., "en" for "en-US").
    /// </summary>
    public string? LanguageCode => ToCultureInfo()?.TwoLetterISOLanguageName;

    /// <summary>
    /// Gets the display name of the culture in its native language.
    /// </summary>
    public string? NativeName => ToCultureInfo()?.NativeName;

    /// <summary>
    /// Gets the display name of the culture in English.
    /// </summary>
    public string? EnglishName => ToCultureInfo()?.EnglishName;

    /// <summary>
    /// Converts Culture to string.
    /// </summary>
    public static implicit operator string(Culture culture) => culture.Value ?? string.Empty;

    /// <summary>
    /// Converts string to Culture. Returns Empty for null/whitespace or invalid culture codes.
    /// </summary>
    public static implicit operator Culture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return TryCreate(value, out var culture) ? culture : Empty;
    }

    /// <summary>
    /// Converts CultureInfo to Culture.
    /// </summary>
    public static implicit operator Culture(CultureInfo cultureInfo) => new(cultureInfo);

    /// <summary>
    /// Converts Culture to CultureInfo.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the culture has no value.</exception>
    public static implicit operator CultureInfo(Culture culture) => 
        culture.ToCultureInfo() ?? throw new InvalidOperationException("Cannot convert empty Culture to CultureInfo.");

    /// <inheritdoc />
    public bool Equals(Culture other)
    {
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Culture other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Compares two cultures for equality.
    /// </summary>
    public static bool operator ==(Culture left, Culture right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two cultures for inequality.
    /// </summary>
    public static bool operator !=(Culture left, Culture right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Culture other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compares two cultures for less than.
    /// </summary>
    public static bool operator <(Culture left, Culture right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two cultures for less than or equal.
    /// </summary>
    public static bool operator <=(Culture left, Culture right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two cultures for greater than.
    /// </summary>
    public static bool operator >(Culture left, Culture right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two cultures for greater than or equal.
    /// </summary>
    public static bool operator >=(Culture left, Culture right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Creates a culture from the specified language code.
    /// </summary>
    public static Culture Create(string value) => new(value);

    /// <summary>
    /// Tries to create a culture from the specified language code.
    /// </summary>
    /// <param name="value">Culture code.</param>
    /// <param name="culture">Created culture or default if value is invalid.</param>
    /// <returns>True if culture was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out Culture culture)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            culture = default;
            return false;
        }

        try
        {
            culture = new Culture(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = default;
            return false;
        }
    }

    /// <summary>
    /// Parses a string to a Culture.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Culture.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Culture Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Culture.");
    }

    /// <summary>
    /// Tries to parse a string to a Culture.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Culture or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Culture result)
        => TryCreate(s, out result);
}
