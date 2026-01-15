using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a valid URL address.
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
[TypeConverter(typeof(ValueObjectTypeConverter<Url>))]
[JsonConverter(typeof(UrlJsonConverter))]
public readonly struct Url : IEquatable<Url>, IComparable<Url>, IParsable<Url>, ISpanParsable<Url>
{
    /// <summary>
    /// Empty URL instance. Equivalent to default(Url).
    /// </summary>
    public static readonly Url Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The URL value. Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Url).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the URL has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the Uri object representing this URL.
    /// </summary>
    public Uri? Uri => HasValue ? new Uri(Value) : null;

    /// <summary>
    /// Gets the URL scheme (http, https, ftp, etc.).
    /// </summary>
    public string? Scheme => Uri?.Scheme;

    /// <summary>
    /// Gets the URL host.
    /// </summary>
    public string? Host => Uri?.Host;

    /// <summary>
    /// Gets the URL port.
    /// </summary>
    public int Port => Uri?.Port ?? 0;

    /// <summary>
    /// Gets the URL path.
    /// </summary>
    public string? Path => Uri?.AbsolutePath;

    /// <summary>
    /// Gets the URL query string.
    /// </summary>
    public string? Query => Uri?.Query;

    /// <summary>
    /// Checks if the URL uses HTTPS.
    /// </summary>
    public bool IsHttps => Uri?.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Checks if the URL is absolute.
    /// </summary>
    public bool IsAbsolute => Uri?.IsAbsoluteUri ?? false;

    /// <summary>
    /// Creates a new URL based on the specified address.
    /// </summary>
    /// <param name="value">URL address.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="UriFormatException">Thrown when <paramref name="value"/> is not a valid URL.</exception>
    public Url(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"'{trimmedValue}' is not a valid absolute URL.");
        }

        _value = uri.AbsoluteUri;
    }

    /// <summary>
    /// Creates a new URL with option to accept relative addresses.
    /// </summary>
    /// <param name="value">URL address.</param>
    /// <param name="allowRelative">Whether to accept relative URLs.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="UriFormatException">Thrown when <paramref name="value"/> is not a valid URL.</exception>
    public Url(string value, bool allowRelative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();
        var uriKind = allowRelative ? UriKind.RelativeOrAbsolute : UriKind.Absolute;

        if (!Uri.TryCreate(trimmedValue, uriKind, out var uri))
        {
            throw new UriFormatException($"'{trimmedValue}' is not a valid URL.");
        }

        _value = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
    }

    /// <summary>
    /// Creates a URL from a Uri object.
    /// </summary>
    /// <param name="uri">Uri object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    public Url(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        _value = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
    }

    /// <summary>
    /// Combines the current URL with a relative path.
    /// </summary>
    /// <param name="relativePath">Relative path to combine.</param>
    /// <returns>New URL that is the combination.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL has no value.</exception>
    public Url Combine(string relativePath)
    {
        if (Uri is null)
            throw new InvalidOperationException("Cannot combine paths on an empty URL.");
        
        var combinedUri = new Uri(Uri, relativePath);
        return new Url(combinedUri);
    }

    /// <summary>
    /// Adds or updates a query string parameter.
    /// </summary>
    /// <param name="key">Parameter key.</param>
    /// <param name="value">Parameter value.</param>
    /// <returns>New URL with the added parameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL has no value.</exception>
    public Url WithQueryParameter(string key, string value)
    {
        if (Uri is null)
            throw new InvalidOperationException("Cannot add query parameter to an empty URL.");
        
        var builder = new UriBuilder(Uri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query[key] = value;
        builder.Query = query.ToString();
        return new Url(builder.Uri);
    }

    /// <summary>
    /// Removes a parameter from the query string.
    /// </summary>
    /// <param name="key">Parameter key to remove.</param>
    /// <returns>New URL without the parameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL has no value.</exception>
    public Url WithoutQueryParameter(string key)
    {
        if (Uri is null)
            throw new InvalidOperationException("Cannot remove query parameter from an empty URL.");
        
        var builder = new UriBuilder(Uri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query.Remove(key);
        builder.Query = query.ToString();
        return new Url(builder.Uri);
    }

    /// <summary>
    /// Converts Url to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Url url) => url.Value;

    /// <summary>
    /// Converts string to Url. Returns Empty for null/whitespace or invalid URLs.
    /// </summary>
    public static implicit operator Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return TryCreate(value, out var url) ? url : Empty;
    }

    /// <summary>
    /// Converts Uri to Url.
    /// </summary>
    public static implicit operator Url(Uri uri) => new(uri);

    /// <summary>
    /// Converts Url to Uri.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the URL has no value.</exception>
    public static implicit operator Uri(Url url) => 
        url.Uri ?? throw new InvalidOperationException("Cannot convert empty Url to Uri.");

    /// <inheritdoc />
    public bool Equals(Url other)
    {
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Url other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// Compares two URLs for equality.
    /// </summary>
    public static bool operator ==(Url left, Url right) => left.Equals(right);

    /// <summary>
    /// Compares two URLs for inequality.
    /// </summary>
    public static bool operator !=(Url left, Url right) => !left.Equals(right);

    /// <inheritdoc />
    public int CompareTo(Url other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compares two URLs for less than.
    /// </summary>
    public static bool operator <(Url left, Url right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two URLs for less than or equal.
    /// </summary>
    public static bool operator <=(Url left, Url right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two URLs for greater than.
    /// </summary>
    public static bool operator >(Url left, Url right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two URLs for greater than or equal.
    /// </summary>
    public static bool operator >=(Url left, Url right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Creates a URL from the specified address.
    /// </summary>
    public static Url Create(string value) => new(value);

    /// <summary>
    /// Creates a URL from the specified address with option to accept relative addresses.
    /// </summary>
    public static Url Create(string value, bool allowRelative) => new(value, allowRelative);

    /// <summary>
    /// Tries to create a URL from the specified address.
    /// </summary>
    /// <param name="value">URL address.</param>
    /// <param name="url">Created URL or default if value is invalid.</param>
    /// <returns>True if URL was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out Url url)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            url = Empty;
            return false;
        }

        try
        {
            url = new Url(value);
            return true;
        }
        catch (UriFormatException)
        {
            url = Empty;
            return false;
        }
    }

    /// <summary>
    /// Tries to create a URL from the specified address with option to accept relative addresses.
    /// </summary>
    /// <param name="value">URL address.</param>
    /// <param name="allowRelative">Whether to accept relative URLs.</param>
    /// <param name="url">Created URL or default if value is invalid.</param>
    /// <returns>True if URL was created, false otherwise.</returns>
    public static bool TryCreate(string? value, bool allowRelative, out Url url)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            url = Empty;
            return false;
        }

        try
        {
            url = new Url(value, allowRelative);
            return true;
        }
        catch (UriFormatException)
        {
            url = Empty;
            return false;
        }
    }

    /// <summary>
    /// Parses a string to a Url.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Url.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Url Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Url.");
    }

    /// <summary>
    /// Tries to parse a string to a Url.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Url or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Url result)
        => TryCreate(s, out result);

    /// <summary>
    /// Parses a span of characters to a Url.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Url.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Url Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException("Cannot parse as Url.");
    }

    /// <summary>
    /// Tries to parse a span of characters to a Url.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Url or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Url result)
        => TryCreate(s.ToString(), out result);
}
