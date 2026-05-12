using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;
using Zonit.Extensions.Text;

namespace Zonit.Extensions;

/// <summary>
/// Represents a title for content (articles, products, categories, etc.).
/// Maximum length of 60 characters (based on typical display constraints).
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
[TypeConverter(typeof(ValueObjectTypeConverter<Title>))]
[JsonConverter(typeof(TitleJsonConverter))]
public readonly struct Title : IEquatable<Title>, IComparable<Title>, IParsable<Title>, ISpanParsable<Title>
{
    /// <summary>
    /// Maximum allowed length for a title.
    /// </summary>
    public const int MaxLength = 60;

    /// <summary>
    /// Minimum required length for a valid title.
    /// </summary>
    public const int MinLength = 1;

    /// <summary>
    /// Empty title instance. Equivalent to default(Title).
    /// </summary>
    public static readonly Title Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The title value. Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Title).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the title has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the length of the title in text elements (graphemes), correctly handling Unicode.
    /// </summary>
    public int Length => string.IsNullOrEmpty(_value) ? 0 : new StringInfo(_value).LengthInTextElements;

    /// <summary>
    /// Creates a new title with the specified value.
    /// </summary>
    /// <param name="value">Title text. Will be trimmed and have whitespace normalized.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when value is null, empty, whitespace only, or exceeds <see cref="MaxLength"/> characters.
    /// </exception>
    public Title(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var normalizedValue = Normalize(value);

        var graphemeLength = new StringInfo(normalizedValue).LengthInTextElements;

        if (graphemeLength < MinLength)
            throw new ArgumentException($"Title must be at least {MinLength} character long.", nameof(value));

        if (graphemeLength > MaxLength)
            throw new ArgumentException($"Title cannot exceed {MaxLength} characters. Current length: {graphemeLength}.", nameof(value));

        _value = normalizedValue;
    }

    /// <summary>
    /// Normalizes the input string: trims and collapses multiple whitespace characters into single spaces.
    /// </summary>
    private static string Normalize(string value)
        => value.Trim().NormalizeWhitespace();

    /// <summary>
    /// Converts Title to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Title title) => title.Value;

    /// <summary>
    /// Converts string to Title.
    /// </summary>
    /// <param name="value">String value to convert.</param>
    /// <returns><see cref="Empty"/> for null/whitespace, otherwise a new Title instance.</returns>
    /// <exception cref="ArgumentException">Thrown when value exceeds <see cref="MaxLength"/> characters.</exception>
    public static implicit operator Title(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return new Title(value);
    }

    /// <inheritdoc />
    public bool Equals(Title other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Title other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Compares two titles for equality.
    /// </summary>
    public static bool operator ==(Title left, Title right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two titles for inequality.
    /// </summary>
    public static bool operator !=(Title left, Title right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Title other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares two titles for less than.
    /// </summary>
    public static bool operator <(Title left, Title right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two titles for less than or equal.
    /// </summary>
    public static bool operator <=(Title left, Title right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two titles for greater than.
    /// </summary>
    public static bool operator >(Title left, Title right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two titles for greater than or equal.
    /// </summary>
    public static bool operator >=(Title left, Title right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Creates a new title from the specified value.
    /// </summary>
    /// <param name="value">Title text.</param>
    /// <returns>A new Title instance.</returns>
    /// <exception cref="ArgumentException">Thrown when value is invalid.</exception>
    public static Title Create(string value) => new(value);

    /// <summary>
    /// Tries to create a title from the specified value without throwing exceptions.
    /// </summary>
    /// <param name="value">Title text.</param>
    /// <param name="title">Created title or <see cref="Empty"/> if value is invalid.</param>
    /// <returns>True if title was created successfully, false otherwise.</returns>
    public static bool TryCreate(string? value, out Title title)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            title = Empty;
            return false;
        }

        var normalizedValue = Normalize(value);
        var graphemeLength = new StringInfo(normalizedValue).LengthInTextElements;

        if (graphemeLength < MinLength || graphemeLength > MaxLength)
        {
            title = Empty;
            return false;
        }

        title = new Title(normalizedValue);
        return true;
    }

    /// <summary>
    /// Parses a string to a Title.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Title.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Title Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Title. Must be between {MinLength} and {MaxLength} characters.");
    }

    /// <summary>
    /// Tries to parse a string to a Title.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Title or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Title result)
        => TryCreate(s, out result);

    /// <summary>
    /// Parses a span of characters to a Title.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Title.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Title Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse as Title. Must be between {MinLength} and {MaxLength} characters.");
    }

    /// <summary>
    /// Tries to parse a span of characters to a Title.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Title or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Title result)
        => TryCreate(s.ToString(), out result);
}
