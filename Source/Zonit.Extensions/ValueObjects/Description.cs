using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a description for content (articles, products, categories, etc.).
/// Optimized for SEO with a maximum length of 160 characters.
/// </summary>
[TypeConverter(typeof(ValueObjectTypeConverter<Description>))]
[JsonConverter(typeof(DescriptionJsonConverter))]
public readonly struct Description : IEquatable<Description>, IComparable<Description>, IParsable<Description>
{
    /// <summary>
    /// Maximum length for SEO optimization (Google displays ~160 characters in meta descriptions).
    /// </summary>
    public const int MaxLength = 160;

    /// <summary>
    /// Minimum length for a valid description.
    /// </summary>
    public const int MinLength = 1;

    /// <summary>
    /// Empty description (default value for optional scenarios).
    /// </summary>
    public static readonly Description Empty = default;

    /// <summary>
    /// The description value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Indicates whether the description has a value.
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Gets the length of the description.
    /// </summary>
    public int Length => Value?.Length ?? 0;

    /// <summary>
    /// Indicates whether the description is optimized for SEO (not exceeding recommended length).
    /// </summary>
    public bool IsOptimized => Length <= MaxLength;

    /// <summary>
    /// Creates a new description with the specified value.
    /// </summary>
    /// <param name="value">Description text.</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or exceeds maximum length.</exception>
    public Description(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (trimmedValue.Length < MinLength)
        {
            throw new ArgumentException($"Description must be at least {MinLength} character long.", nameof(value));
        }

        if (trimmedValue.Length > MaxLength)
        {
            throw new ArgumentException($"Description cannot exceed {MaxLength} characters for SEO optimization.", nameof(value));
        }

        Value = trimmedValue;
    }

    /// <summary>
    /// Converts Description to string.
    /// </summary>
    public static implicit operator string(Description description) => description.Value ?? string.Empty;

    /// <summary>
    /// Converts string to Description. Returns Empty for null/whitespace, truncates if too long.
    /// </summary>
    public static implicit operator Description(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            return Truncate(trimmed);

        return new Description(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(Description other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Description other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

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
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Creates a description from the specified value.
    /// </summary>
    public static Description Create(string value) => new(value);

    /// <summary>
    /// Tries to create a description from the specified value.
    /// </summary>
    /// <param name="value">Description text.</param>
    /// <param name="description">Created description or default if value is invalid.</param>
    /// <returns>True if description was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out Description description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            description = default;
            return false;
        }

        var trimmedValue = value.Trim();

        if (trimmedValue.Length < MinLength || trimmedValue.Length > MaxLength)
        {
            description = default;
            return false;
        }

        description = new Description(trimmedValue);
        return true;
    }

    /// <summary>
    /// Truncates the description to the maximum SEO length if necessary.
    /// </summary>
    /// <param name="value">Description text to truncate.</param>
    /// <param name="addEllipsis">Whether to add "..." at the end if truncated.</param>
    /// <returns>Truncated description.</returns>
    public static Description Truncate(string value, bool addEllipsis = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (trimmedValue.Length <= MaxLength)
        {
            return new Description(trimmedValue);
        }

        var truncatedLength = addEllipsis ? MaxLength - 3 : MaxLength;
        var truncated = trimmedValue[..truncatedLength];

        if (addEllipsis)
        {
            truncated += "...";
        }

        return new Description(truncated);
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

        throw new FormatException($"Cannot parse '{s}' as Description.");
    }

    /// <summary>
    /// Tries to parse a string to a Description.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Description or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Description result)
        => TryCreate(s, out result);
}
