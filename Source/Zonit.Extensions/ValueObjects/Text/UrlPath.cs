using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents an in-site path component of a URL — the part after the host
/// (path + optional query + optional fragment), without a scheme or host.
/// </summary>
/// <remarks>
/// <para>This is the navigation counterpart to <see cref="Url"/>:</para>
/// <list type="bullet">
///   <item><see cref="Url"/> = absolute address (<c>https://acme.example/orders?id=1</c>).</item>
///   <item><see cref="UrlPath"/> = path within a host (<c>/orders?id=1</c>, <c>orders/42</c>).</item>
///   <item>(future) <c>FilePath</c> = OS filesystem locator. Distinct because of platform
///         separators (<c>\\</c> on Windows), drive letters, UNC, etc.</item>
/// </list>
///
/// <para><b>Normalization.</b> Input is trimmed; absolute-looking inputs (<c>http://...</c>,
/// <c>//cdn.example/...</c>) are rejected — use <see cref="Url"/> for those. A leading
/// <c>/</c> is preserved as part of <see cref="Value"/> if present, so renderers can
/// distinguish "rooted" (<c>/foo</c>) from "relative" (<c>foo</c>) paths.</para>
///
/// <para><b>Why a dedicated VO?</b> Same reasons as the other VOs in this assembly —
/// model binding, EF Core, JSON, validation — plus it documents intent at the
/// type level: a field typed <see cref="UrlPath"/> can never accidentally receive
/// a full external URL.</para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<UrlPath>))]
[JsonConverter(typeof(UrlPathJsonConverter))]
public readonly struct UrlPath : IEquatable<UrlPath>, IComparable<UrlPath>, IParsable<UrlPath>, ISpanParsable<UrlPath>
{
    /// <summary>Empty path. Equivalent to <c>default(UrlPath)</c>.</summary>
    public static readonly UrlPath Empty = default;

    /// <summary>Root path (<c>"/"</c>).</summary>
    public static readonly UrlPath Root = new("/", validated: true);

    /// <summary>Maximum length (matches typical HTTP URI limits).</summary>
    public const int MaxLength = 2048;

    private readonly string? _value;

    /// <summary>Raw value, never null. <see cref="string.Empty"/> for <see cref="Empty"/>.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Whether the path carries a non-empty value.</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>True if the path begins with <c>/</c> (rooted).</summary>
    public bool IsRooted => HasValue && _value![0] == '/';

    // ─── ctors ───────────────────────────────────────────────────────────

    private UrlPath(string value, bool validated)
    {
        _value = value;
    }

    /// <summary>
    /// Constructs a <see cref="UrlPath"/> from a string. Validates and normalizes (trim).
    /// </summary>
    /// <param name="value">A path (e.g. <c>"/orders"</c>, <c>"orders?id=1"</c>) or <c>null</c>.</param>
    /// <exception cref="ArgumentException">When the value is too long or looks absolute.</exception>
    public UrlPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _value = null;
            return;
        }

        if (!TryNormalize(value, out var normalized, out var error))
            throw new ArgumentException(error, nameof(value));

        _value = normalized;
    }

    // ─── factories ──────────────────────────────────────────────────────

    /// <summary>Strict factory; throws on invalid input.</summary>
    public static UrlPath Create(string? value) => new(value);

    /// <summary>Non-throwing factory.</summary>
    public static bool TryCreate(string? value, out UrlPath path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            path = Empty;
            return true;
        }

        if (!TryNormalize(value, out var normalized, out _))
        {
            path = Empty;
            return false;
        }

        path = new UrlPath(normalized, validated: true);
        return true;
    }

    private static bool TryNormalize(string value, [NotNullWhen(true)] out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0) { error = "Path is empty after trim."; return false; }
        if (trimmed.Length > MaxLength) { error = $"Path exceeds {MaxLength} chars."; return false; }

        // Reject schemes (http://, https://, ftp://, mailto:, javascript:, etc.) and
        // protocol-relative ("//cdn/..."). Those are absolute references — caller
        // should use Url instead.
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            error = "Protocol-relative URLs are not paths. Use Url.";
            return false;
        }

        var colon = trimmed.IndexOf(':');
        if (colon > 0)
        {
            var beforeColon = trimmed.AsSpan(0, colon);
            // A real path can legitimately contain ':' (e.g. "/files/v1:foo") but never
            // as the first segment-prefix together with "//". If "://" is present it's
            // unambiguously a scheme.
            if (colon + 2 < trimmed.Length && trimmed[colon + 1] == '/' && trimmed[colon + 2] == '/')
            {
                error = "Absolute URLs are not paths. Use Url.";
                return false;
            }
            // mailto:, javascript:, data:, tel: — single-colon schemes. Reject if the
            // prefix looks like a scheme keyword (letters only).
            bool looksLikeScheme = true;
            foreach (var ch in beforeColon)
            {
                if (!char.IsLetter(ch) && ch != '+' && ch != '-' && ch != '.')
                {
                    looksLikeScheme = false;
                    break;
                }
            }
            if (looksLikeScheme && beforeColon.Length > 0 && trimmed.IndexOf('/') > colon)
            {
                error = $"Value looks like a '{beforeColon}:' URI. Use Url.";
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }

    // ─── parse contracts ────────────────────────────────────────────────

    /// <inheritdoc />
    public static UrlPath Parse(string s, IFormatProvider? provider) => new(s);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UrlPath result)
        => TryCreate(s, out result);

    /// <inheritdoc />
    public static UrlPath Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(s.ToString());

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UrlPath result)
        => TryCreate(s.ToString(), out result);

    // ─── rendering helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns the path with a guaranteed leading <c>/</c>. Useful when rendering
    /// directly into <c>&lt;a href&gt;</c>.
    /// </summary>
    public string ToAbsolutePath()
    {
        if (!HasValue) return "/";
        return IsRooted ? _value! : "/" + _value!;
    }

    /// <summary>
    /// Joins this path with a path base (e.g. <c>HttpContext.Request.PathBase</c>).
    /// Both pieces are joined with exactly one <c>/</c>.
    /// </summary>
    public string ToString(string? pathBase)
    {
        if (string.IsNullOrEmpty(pathBase)) return ToAbsolutePath();
        var trimmedBase = pathBase.TrimEnd('/');
        var trimmedPath = IsRooted ? _value!.AsSpan(1).ToString() : _value ?? string.Empty;
        return trimmedPath.Length == 0
            ? trimmedBase + "/"
            : trimmedBase + "/" + trimmedPath;
    }

    // ─── conversions ────────────────────────────────────────────────────

    /// <summary>String → <see cref="UrlPath"/>; never throws — invalid input becomes <see cref="Empty"/>.</summary>
    public static implicit operator UrlPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;
        return TryCreate(value, out var path) ? path : Empty;
    }

    /// <summary><see cref="UrlPath"/> → string.</summary>
    public static implicit operator string(UrlPath path) => path.Value;

    // ─── equality / compare ─────────────────────────────────────────────

    public bool Equals(UrlPath other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is UrlPath other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public int CompareTo(UrlPath other) => string.CompareOrdinal(Value, other.Value);

    public static bool operator ==(UrlPath left, UrlPath right) => left.Equals(right);
    public static bool operator !=(UrlPath left, UrlPath right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Value;
}
