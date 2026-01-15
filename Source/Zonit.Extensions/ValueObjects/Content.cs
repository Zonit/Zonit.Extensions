using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents textual content (HTML, markdown, plain text, etc.) without length restrictions.
/// </summary>
/// <remarks>
/// This is a DDD value object designed for:
/// <list type="bullet">
///   <item>Entity Framework Core (value object mapping)</item>
///   <item>Blazor form validation (via TypeConverter)</item>
///   <item>JSON serialization (via JsonConverter)</item>
///   <item>Model binding in ASP.NET Core</item>
/// </list>
/// Unlike <see cref="Title"/> and <see cref="Description"/>, Content has no length limit
/// and preserves all whitespace and special characters (including HTML).
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Content>))]
[JsonConverter(typeof(ContentJsonConverter))]
public readonly struct Content : IEquatable<Content>, IComparable<Content>, IParsable<Content>, ISpanParsable<Content>
{
    /// <summary>
    /// Empty content instance. Equivalent to default(Content).
    /// </summary>
    public static readonly Content Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The content value. Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Content).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the content has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the length of the content in characters.
    /// </summary>
    public int Length => _value?.Length ?? 0;

    /// <summary>
    /// Creates new content with the specified value.
    /// </summary>
    /// <param name="value">Content text. Will be trimmed at start and end only.</param>
    /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
    public Content(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        _value = value.Trim();
    }

    /// <summary>
    /// Converts Content to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Content content) => content.Value;

    /// <summary>
    /// Converts string to Content.
    /// </summary>
    /// <param name="value">String value to convert.</param>
    /// <returns><see cref="Empty"/> for null/whitespace, otherwise a new Content instance.</returns>
    public static implicit operator Content(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return new Content(value);
    }

    /// <inheritdoc />
    public bool Equals(Content other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Content other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Compares two contents for equality.
    /// </summary>
    public static bool operator ==(Content left, Content right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two contents for inequality.
    /// </summary>
    public static bool operator !=(Content left, Content right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Content other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares two contents for less than.
    /// </summary>
    public static bool operator <(Content left, Content right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two contents for less than or equal.
    /// </summary>
    public static bool operator <=(Content left, Content right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two contents for greater than.
    /// </summary>
    public static bool operator >(Content left, Content right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two contents for greater than or equal.
    /// </summary>
    public static bool operator >=(Content left, Content right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Creates new content from the specified value.
    /// </summary>
    /// <param name="value">Content text.</param>
    /// <returns>A new Content instance.</returns>
    /// <exception cref="ArgumentException">Thrown when value is invalid.</exception>
    public static Content Create(string value) => new(value);

    /// <summary>
    /// Tries to create content from the specified value without throwing exceptions.
    /// </summary>
    /// <param name="value">Content text.</param>
    /// <param name="content">Created content or <see cref="Empty"/> if value is invalid.</param>
    /// <returns>True if content was created successfully, false otherwise.</returns>
    public static bool TryCreate(string? value, out Content content)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            content = Empty;
            return false;
        }

        content = new Content(value);
        return true;
    }

    /// <summary>
    /// Parses a string to Content.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Content.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Content Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Content.");
    }

    /// <summary>
    /// Tries to parse a string to Content.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Content or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Content result)
        => TryCreate(s, out result);

    /// <summary>
    /// Parses a span of characters to Content.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Content.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Content Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException("Cannot parse as Content.");
    }

    /// <summary>
    /// Tries to parse a span of characters to Content.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Content or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Content result)
        => TryCreate(s.ToString(), out result);
}
