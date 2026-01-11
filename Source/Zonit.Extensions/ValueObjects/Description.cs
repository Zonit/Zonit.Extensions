using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;
using Zonit.Extensions.Text;

namespace Zonit.Extensions;

/// <summary>
/// Represents a description for content (articles, products, categories, etc.).
/// Maximum length of 160 characters (based on typical display constraints).
/// </summary>
/// <remarks>
/// This is a DDD value object designed for:
/// <list type="bullet">
///   <item>Entity Framework Core (value object mapping)</item>
///   <item>Blazor form validation (via TypeConverter)</item>
///   <item>JSON serialization (via JsonConverter)</item>
///   <item>Model binding in ASP.NET Core</item>
/// </list>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Description>))]
[JsonConverter(typeof(DescriptionJsonConverter))]
public readonly struct Description : IEquatable<Description>, IComparable<Description>, IParsable<Description>
{
    /// <summary>
    /// Maximum allowed length for a description.
    /// </summary>
    public const int MaxLength = 160;

    /// <summary>
    /// Minimum required length for a valid description.
    /// </summary>
    public const int MinLength = 1;

    /// <summary>
    /// Empty description instance. Equivalent to default(Description).
    /// </summary>
    public static readonly Description Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The description value. Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Description).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the description has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the length of the description in text elements (graphemes), correctly handling Unicode.
    /// </summary>
    public int Length => string.IsNullOrEmpty(_value) ? 0 : new StringInfo(_value).LengthInTextElements;

    /// <summary>
    /// Creates a new description with the specified value.
    /// </summary>
    /// <param name="value">Description text. Will be trimmed and have whitespace normalized.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when value is null, empty, whitespace only, or exceeds <see cref="MaxLength"/> characters.
    /// </exception>
    public Description(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var normalizedValue = Normalize(value);

        var graphemeLength = new StringInfo(normalizedValue).LengthInTextElements;

        if (graphemeLength < MinLength)
            throw new ArgumentException($"Description must be at least {MinLength} character long.", nameof(value));

        if (graphemeLength > MaxLength)
            throw new ArgumentException($"Description cannot exceed {MaxLength} characters. Current length: {graphemeLength}.", nameof(value));

        _value = normalizedValue;
    }

    /// <summary>
    /// Normalizes the input string: trims and collapses multiple whitespace characters into single spaces.
    /// </summary>
    private static string Normalize(string value)
        => value.Trim().NormalizeWhitespace();

    /// <summary>
    /// Converts Description to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Description description) => description.Value;

    /// <summary>
    /// Converts string to Description.
    /// </summary>
    /// <param name="value">String value to convert.</param>
    /// <returns><see cref="Empty"/> for null/whitespace, otherwise a new Description instance.</returns>
    /// <exception cref="ArgumentException">Thrown when value exceeds <see cref="MaxLength"/> characters.</exception>
    public static implicit operator Description(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return new Description(value);
    }

    /// <inheritdoc />
    public bool Equals(Description other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Description other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Compares two descriptions for equality.
    /// </summary>
    public static bool operator ==(Description left, Description right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two descriptions for inequality.
    /// </summary>
    public static bool operator !=(Description left, Description right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Description other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares two descriptions for less than.
    /// </summary>
    public static bool operator <(Description left, Description right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two descriptions for less than or equal.
    /// </summary>
    public static bool operator <=(Description left, Description right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two descriptions for greater than.
    /// </summary>
    public static bool operator >(Description left, Description right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two descriptions for greater than or equal.
    /// </summary>
    public static bool operator >=(Description left, Description right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Creates a new description from the specified value.
    /// </summary>
    /// <param name="value">Description text.</param>
    /// <returns>A new Description instance.</returns>
    /// <exception cref="ArgumentException">Thrown when value is invalid.</exception>
    public static Description Create(string value) => new(value);

    /// <summary>
    /// Tries to create a description from the specified value without throwing exceptions.
    /// </summary>
    /// <param name="value">Description text.</param>
    /// <param name="description">Created description or <see cref="Empty"/> if value is invalid.</param>
    /// <returns>True if description was created successfully, false otherwise.</returns>
    public static bool TryCreate(string? value, out Description description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            description = Empty;
            return false;
        }

        var normalizedValue = Normalize(value);
        var graphemeLength = new StringInfo(normalizedValue).LengthInTextElements;

        if (graphemeLength < MinLength || graphemeLength > MaxLength)
        {
            description = Empty;
            return false;
        }

        description = new Description(normalizedValue);
        return true;
    }

    /// <summary>
    /// Parses a string to a Description.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Description.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Description Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Description. Must be between {MinLength} and {MaxLength} characters.");
    }

    /// <summary>
    /// Tries to parse a string to a Description.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Description or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Description result)
        => TryCreate(s, out result);
}
