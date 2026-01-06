using System.ComponentModel;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a description for content (articles, products, categories, etc.).
/// Optimized for SEO with a maximum length of 160 characters.
/// </summary>
[TypeConverter(typeof(ValueObjectTypeConverter<Description>))]
public readonly struct Description : IEquatable<Description>
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
    /// The description value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the length of the description.
    /// </summary>
    public int Length => Value.Length;

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
    /// Converts string to Description.
    /// </summary>
    public static implicit operator Description(string value) => new(value);

    /// <summary>
    /// Converts Description to string.
    /// </summary>
    public static implicit operator string(Description description) => description.Value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(Description other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Description other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

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
    public override string ToString() => Value;

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
}
