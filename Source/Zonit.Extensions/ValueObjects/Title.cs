using System.ComponentModel;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a title for content (articles, products, categories, etc.).
/// Optimized for SEO with a maximum length of 60 characters.
/// </summary>
[TypeConverter(typeof(ValueObjectTypeConverter<Title>))]
public readonly struct Title : IEquatable<Title>
{
    /// <summary>
    /// Maximum length for SEO optimization (Google displays ~60 characters in search results).
    /// </summary>
    public const int MaxLength = 60;

    /// <summary>
    /// Minimum length for a valid title.
    /// </summary>
    public const int MinLength = 1;

    /// <summary>
    /// The title value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the length of the title.
    /// </summary>
    public int Length => Value.Length;

    /// <summary>
    /// Indicates whether the title is optimized for SEO (not exceeding recommended length).
    /// </summary>
    public bool IsOptimized => Length <= MaxLength;

    /// <summary>
    /// Creates a new title with the specified value.
    /// </summary>
    /// <param name="value">Title text.</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or exceeds maximum length.</exception>
    public Title(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (trimmedValue.Length < MinLength)
        {
            throw new ArgumentException($"Title must be at least {MinLength} character long.", nameof(value));
        }

        if (trimmedValue.Length > MaxLength)
        {
            throw new ArgumentException($"Title cannot exceed {MaxLength} characters for SEO optimization.", nameof(value));
        }

        Value = trimmedValue;
    }

    /// <summary>
    /// Converts string to Title.
    /// </summary>
    public static implicit operator Title(string value) => new(value);

    /// <summary>
    /// Converts Title to string.
    /// </summary>
    public static implicit operator string(Title title) => title.Value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(Title other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Title other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

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
    public override string ToString() => Value;

    /// <summary>
    /// Creates a title from the specified value.
    /// </summary>
    public static Title Create(string value) => new(value);

    /// <summary>
    /// Tries to create a title from the specified value.
    /// </summary>
    /// <param name="value">Title text.</param>
    /// <param name="title">Created title or default if value is invalid.</param>
    /// <returns>True if title was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out Title title)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            title = default;
            return false;
        }

        var trimmedValue = value.Trim();

        if (trimmedValue.Length < MinLength || trimmedValue.Length > MaxLength)
        {
            title = default;
            return false;
        }

        title = new Title(trimmedValue);
        return true;
    }

    /// <summary>
    /// Truncates the title to the maximum SEO length if necessary.
    /// </summary>
    /// <param name="value">Title text to truncate.</param>
    /// <param name="addEllipsis">Whether to add "..." at the end if truncated.</param>
    /// <returns>Truncated title.</returns>
    public static Title Truncate(string value, bool addEllipsis = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (trimmedValue.Length <= MaxLength)
        {
            return new Title(trimmedValue);
        }

        var truncatedLength = addEllipsis ? MaxLength - 3 : MaxLength;
        var truncated = trimmedValue[..truncatedLength];

        if (addEllipsis)
        {
            truncated += "...";
        }

        return new Title(truncated);
    }
}
