using Diacritics.Extensions;
using System.Text.RegularExpressions;

namespace Zonit.Extensions;

/// <summary>
/// Reprezentuje przyjazny dla URL slug wygenerowany z tekstu.
/// </summary>
public sealed partial class UrlSlug : IEquatable<UrlSlug>
{
    /// <summary>
    /// Wartość sluga.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Tworzy nowy slug na podstawie podanego tekstu.
    /// </summary>
    /// <param name="value">Tekst do przekształcenia na slug.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="value"/> jest null.</exception>
    public UrlSlug(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        string result = value.Trim().RemoveDiacritics();
        result = NonAlphanumericRegex().Replace(result, "");
        result = WhitespaceRegex().Replace(result, "-");
        result = MultipleHyphensRegex().Replace(result, "-"); // usunięcie nadmiernych myślników
        Value = result.ToLowerInvariant().Trim('-');
    }

    /// <summary>
    /// Tworzy nowy unikalny slug na podstawie podanego tekstu, uwzględniając istniejące slugi.
    /// </summary>
    /// <param name="value">Tekst do przekształcenia na slug.</param>
    /// <param name="getExistingUrls">Funkcja zwracająca listę istniejących slugów.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="value"/> lub <paramref name="getExistingUrls"/> jest null.</exception>
    public UrlSlug(string value, Func<string, List<string>> getExistingUrls)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        ArgumentNullException.ThrowIfNull(getExistingUrls, nameof(getExistingUrls));

        string baseSlug = CreateSlug(value);
        Value = EnsureUniqueSlug(baseSlug, getExistingUrls);
    }

    /// <summary>
    /// Tworzy podstawowy slug z tekstu.
    /// </summary>
    private static string CreateSlug(string value)
    {
        string result = value.Trim().RemoveDiacritics();
        result = NonAlphanumericRegex().Replace(result, "");
        result = WhitespaceRegex().Replace(result, "-");
        result = MultipleHyphensRegex().Replace(result, "-");
        return result.ToLowerInvariant().Trim('-');
    }

    /// <summary>
    /// Zapewnia unikalność sluga w kontekście istniejących slugów.
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
    /// Konwertuje string na obiekt UrlSlug.
    /// </summary>
    public static implicit operator UrlSlug(string value) => new(value);

    /// <summary>
    /// Konwertuje UrlSlug na string.
    /// </summary>
    public static implicit operator string(UrlSlug slug) => slug?.Value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(UrlSlug? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UrlSlug other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Porównuje dwa slugi.
    /// </summary>
    public static bool operator ==(UrlSlug? left, UrlSlug? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Porównuje dwa slugi.
    /// </summary>
    public static bool operator !=(UrlSlug? left, UrlSlug? right) => !(left == right);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Tworzy slug z podanego tekstu.
    /// </summary>
    public static UrlSlug Create(string value) => new(value);

    [GeneratedRegex("[^A-Za-z0-9 -]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphensRegex();
}
