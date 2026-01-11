using Diacritics.Extensions;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a URL-friendly slug generated from text.
/// </summary>
[TypeConverter(typeof(ValueObjectTypeConverter<UrlSlug>))]
[JsonConverter(typeof(UrlSlugJsonConverter))]
public readonly struct UrlSlug : IEquatable<UrlSlug>, IComparable<UrlSlug>, IParsable<UrlSlug>
{
    /// <summary>
    /// Empty UrlSlug (default value for optional scenarios).
    /// </summary>
    public static readonly UrlSlug Empty = default;

    /// <summary>
    /// The slug value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Indicates whether the slug has a value.
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Creates a new slug based on the specified text.
    /// </summary>
    /// <param name="value">Text to transform into a slug.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public UrlSlug(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        string result = value.Trim().RemoveDiacritics();
        result = UrlSlugRegexes.NonAlphanumericRegex().Replace(result, "");
        result = UrlSlugRegexes.WhitespaceRegex().Replace(result, "-");
        result = UrlSlugRegexes.MultipleHyphensRegex().Replace(result, "-"); // Remove excessive hyphens
        Value = result.ToLowerInvariant().Trim('-');
    }

    /// <summary>
    /// Creates a new unique slug based on the specified text, considering existing slugs.
    /// </summary>
    /// <param name="value">Text to transform into a slug.</param>
    /// <param name="getExistingUrls">Function returning a list of existing slugs.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="getExistingUrls"/> is null.</exception>
    public UrlSlug(string value, Func<string, List<string>> getExistingUrls)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        ArgumentNullException.ThrowIfNull(getExistingUrls, nameof(getExistingUrls));

        string baseSlug = CreateSlug(value);
        Value = EnsureUniqueSlug(baseSlug, getExistingUrls);
    }

    /// <summary>
    /// Creates a basic slug from text.
    /// </summary>
    private static string CreateSlug(string value)
    {
        string result = value.Trim().RemoveDiacritics();
        result = UrlSlugRegexes.NonAlphanumericRegex().Replace(result, "");
        result = UrlSlugRegexes.WhitespaceRegex().Replace(result, "-");
        result = UrlSlugRegexes.MultipleHyphensRegex().Replace(result, "-");
        return result.ToLowerInvariant().Trim('-');
    }

    /// <summary>
    /// Ensures uniqueness of the slug in the context of existing slugs.
    /// </summary>
    private static string EnsureUniqueSlug(string baseSlug, Func<string, List<string>> getExistingUrls)
    {
        var existingUrls = getExistingUrls(baseSlug);

        if (existingUrls.Count == 0 || !existingUrls.Contains(baseSlug))
        {
            return baseSlug;
        }

        int suffix = 1;
        string uniqueSlug;

        do
        {
            uniqueSlug = $"{baseSlug}-{suffix}";
            suffix++;
        } while (existingUrls.Contains(uniqueSlug));

        return uniqueSlug;
    }

    /// <summary>
    /// Converts UrlSlug to string.
    /// </summary>
    public static implicit operator string(UrlSlug slug) => slug.Value ?? string.Empty;

    /// <summary>
    /// Converts string to UrlSlug. Returns Empty for null/whitespace.
    /// </summary>
    public static implicit operator UrlSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return new UrlSlug(value);
    }

    /// <inheritdoc />
    public bool Equals(UrlSlug other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UrlSlug other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    /// <summary>
    /// Compares two slugs for equality.
    /// </summary>
    public static bool operator ==(UrlSlug left, UrlSlug right) => left.Equals(right);

    /// <summary>
    /// Compares two slugs for inequality.
    /// </summary>
    public static bool operator !=(UrlSlug left, UrlSlug right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(UrlSlug other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares two slugs for less than.
    /// </summary>
    public static bool operator <(UrlSlug left, UrlSlug right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two slugs for less than or equal.
    /// </summary>
    public static bool operator <=(UrlSlug left, UrlSlug right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two slugs for greater than.
    /// </summary>
    public static bool operator >(UrlSlug left, UrlSlug right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two slugs for greater than or equal.
    /// </summary>
    public static bool operator >=(UrlSlug left, UrlSlug right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Creates a slug from the specified text.
    /// </summary>
    public static UrlSlug Create(string value) => new(value);

    /// <summary>
    /// Tries to create a slug from the specified text.
    /// </summary>
    /// <param name="value">Text to transform into a slug.</param>
    /// <param name="slug">Created slug or default if value is invalid.</param>
    /// <returns>True if slug was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out UrlSlug slug)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            slug = default;
            return false;
        }

        slug = new UrlSlug(value);
        return true;
    }

    /// <summary>
    /// Parses a string to a UrlSlug.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed UrlSlug.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static UrlSlug Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as UrlSlug.");
    }

    /// <summary>
    /// Tries to parse a string to a UrlSlug.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed UrlSlug or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UrlSlug result)
        => TryCreate(s, out result);
}

/// <summary>
/// Helper class containing compiled regex patterns for UrlSlug generation.
/// </summary>
internal static partial class UrlSlugRegexes
{
    [GeneratedRegex("[^A-Za-z0-9 -]+", RegexOptions.Compiled)]
    internal static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    internal static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-{2,}", RegexOptions.Compiled)]
    internal static partial Regex MultipleHyphensRegex();
}
