using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a culture in language format (e.g., "en-US", "pl-PL").
/// </summary>
/// <remarks>
/// This is a DDD value object designed for:
/// <list type="bullet">
///   <item>Entity Framework Core (value object mapping)</item>
///   <item>Blazor form validation (via TypeConverter)</item>
///   <item>JSON serialization (via JsonConverter)</item>
///   <item>Model binding in ASP.NET Core</item>
/// </list>
/// Note: <c>default(Culture).Value</c> returns empty string (not null).
/// Use <see cref="Default"/> for a valid "en-US" culture.
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Culture>))]
[JsonConverter(typeof(CultureJsonConverter))]
public readonly struct Culture : IEquatable<Culture>, IComparable<Culture>, IParsable<Culture>, ISpanParsable<Culture>
{
    /// <summary>
    /// Default culture (en-US). Use this when you need a valid default culture.
    /// </summary>
    public static readonly Culture Default = new("en-US");

    /// <summary>
    /// Empty culture instance. Equivalent to default(Culture).
    /// </summary>
    public static readonly Culture Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The culture value in language format (e.g., "en-US"). Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Culture).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the culture has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the value or returns "en-US" if empty.
    /// Use this when you need a guaranteed valid culture code.
    /// </summary>
    public string ValueOrDefault => HasValue ? _value! : "en-US";

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
            _value = cultureInfo.Name;
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
        _value = cultureInfo.Name;
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
    /// Converts Culture to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Culture culture) => culture.Value;

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
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

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
    public override string ToString() => Value;

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
            culture = Empty;
            return false;
        }

        try
        {
            culture = new Culture(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = Empty;
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

    /// <summary>
    /// Parses a span of characters to a Culture.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Culture.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Culture Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException("Cannot parse as Culture.");
    }

    /// <summary>
    /// Tries to parse a span of characters to a Culture.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Culture or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Culture result)
        => TryCreate(s.ToString(), out result);
}
