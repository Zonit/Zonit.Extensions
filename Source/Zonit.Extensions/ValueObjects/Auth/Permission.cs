using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a permission identifier (e.g. <c>"affiliate.read"</c>, <c>"orders.*"</c>, <c>"admin"</c>).
/// </summary>
/// <remarks>
/// <para>This is a DDD value object designed for:</para>
/// <list type="bullet">
///   <item>Authorization checks (<c>IPermissionChecker.Has(Permission)</c>)</item>
///   <item>Navigation/UI guard rails (<c>NavGroup.Permission</c>)</item>
///   <item>Entity Framework Core (value object mapping)</item>
///   <item>JSON serialization / Model binding</item>
/// </list>
/// <para>Format: dot-separated tokens of <c>[a-z0-9_-]</c> with optional wildcard <c>*</c>
/// at any token (e.g. <c>"orders.*.read"</c>). Case-insensitive, normalized to lower.</para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Permission>))]
[JsonConverter(typeof(PermissionJsonConverter))]
public readonly partial struct Permission : IEquatable<Permission>, IComparable<Permission>, IParsable<Permission>, ISpanParsable<Permission>
{
    /// <summary>Maximum total length.</summary>
    public const int MaxLength = 200;

    /// <summary>Minimum length (a single token like <c>"a"</c>).</summary>
    public const int MinLength = 1;

    /// <summary>Wildcard token that matches any single segment.</summary>
    public const string Wildcard = "*";

    /// <summary>Empty Permission (no constraint).</summary>
    public static readonly Permission Empty = default;

    private readonly string? _value;

    /// <summary>Permission value. Never null.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>True if Permission has a non-empty value.</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>Tokens of the permission (split by dot).</summary>
    public IReadOnlyList<string> Tokens =>
        HasValue ? Value.Split('.') : Array.Empty<string>();

    /// <summary>True if this permission contains a wildcard token.</summary>
    public bool HasWildcard => HasValue && Value.Contains('*');

    // Source-generated regex — AOT-safe, faster than `new Regex(..., Compiled)`.
    //
    // Each token is EITHER a literal `[a-z0-9_-]+` OR a bare wildcard `*`. We deliberately
    // disallow mixed forms like `"a*b"` so the wildcard semantics stay consistent: every
    // `*` always means "match this whole token" and there is no ambiguity with prefix /
    // suffix matching (audit AUDIT_2026_05 §3.4).
    [GeneratedRegex(@"^([a-z0-9_\-]+|\*)(\.([a-z0-9_\-]+|\*))*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// Creates a new Permission. Trims and lowercases the value.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when value is invalid.</exception>
    public Permission(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            throw new ArgumentException($"Permission cannot exceed {MaxLength} characters.", nameof(value));

        if (!Pattern().IsMatch(normalized))
            throw new ArgumentException(
                "Permission must consist of dot-separated tokens of [a-z0-9_-*] characters (e.g. 'orders.read', 'admin.*').",
                nameof(value));

        _value = normalized;
    }

    /// <summary>
    /// Checks whether <paramref name="other"/> is granted by this permission, expanding wildcards.
    /// E.g. <c>"orders.*"</c>.Implies(<c>"orders.read"</c>) → true.
    /// </summary>
    /// <remarks>
    /// <b>Wildcard semantics</b> (industry-standard, matches AWS IAM / K8s RBAC / Casbin default):
    /// a trailing <c>*</c> matches zero or more sub-tokens. Therefore
    /// <c>"admin.*".Implies("admin")</c> returns <see langword="true"/>. See audit AUDIT_2026_05 §1.11.
    /// <para><b>Performance</b>: the implementation walks both values via <see cref="ReadOnlySpan{T}"/>
    /// token enumeration — no <c>string.Split</c> allocations, no intermediate arrays. This matters
    /// because <see cref="Implies"/> sits on the hot path of every <c>[Authorize&lt;RequirePermission&gt;]</c>
    /// check (called O(granted-permissions) times per request).</para>
    /// </remarks>
    public bool Implies(Permission other)
    {
        if (!HasValue) return false;
        if (!other.HasValue) return true; // empty permission is universally allowed
        if (Equals(other)) return true;

        var leftValue = Value.AsSpan();
        var rightValue = other.Value.AsSpan();

        var leftSplit = leftValue.Split('.');
        var rightSplit = rightValue.Split('.');

        bool leftHas = leftSplit.MoveNext();
        bool rightHas = rightSplit.MoveNext();

        while (leftHas || rightHas)
        {
            // Wildcard at this position matches anything (including the right side running out
            // — that is the "trailing *" permissive semantics documented above).
            if (leftHas)
            {
                var lSlice = leftValue[leftSplit.Current];
                if (lSlice.Length == 1 && lSlice[0] == '*')
                {
                    leftHas = leftSplit.MoveNext();
                    rightHas = rightSplit.MoveNext();
                    continue;
                }
            }

            if (!leftHas) return false;   // left ran out → not implies
            if (!rightHas) return false;  // right ran out → left is more specific
            if (!leftValue[leftSplit.Current].SequenceEqual(rightValue[rightSplit.Current]))
                return false;

            leftHas = leftSplit.MoveNext();
            rightHas = rightSplit.MoveNext();
        }

        return true;
    }

    public static implicit operator string(Permission p) => p.Value;

    public static implicit operator Permission(string? value)
        => string.IsNullOrWhiteSpace(value) ? Empty : new Permission(value);

    public bool Equals(Permission other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Permission p && Equals(p);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public int CompareTo(Permission other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public static bool operator ==(Permission a, Permission b) => a.Equals(b);
    public static bool operator !=(Permission a, Permission b) => !a.Equals(b);
    public static bool operator <(Permission a, Permission b) => a.CompareTo(b) < 0;
    public static bool operator <=(Permission a, Permission b) => a.CompareTo(b) <= 0;
    public static bool operator >(Permission a, Permission b) => a.CompareTo(b) > 0;
    public static bool operator >=(Permission a, Permission b) => a.CompareTo(b) >= 0;

    public override string ToString() => Value;

    public static Permission Create(string value) => new(value);

    public static bool TryCreate(string? value, out Permission permission)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            permission = Empty;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !Pattern().IsMatch(normalized))
        {
            permission = Empty;
            return false;
        }

        permission = new Permission(normalized);
        return true;
    }

    public static Permission Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Permission.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Permission result)
        => TryCreate(s, out result);

    public static Permission Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException("Cannot parse as Permission.");

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Permission result)
        => TryCreate(s.ToString(), out result);
}

/// <summary>
/// JSON converter for <see cref="Permission"/> – serializes as plain string.
/// </summary>
public sealed class PermissionJsonConverter : JsonConverter<Permission>
{
    public override Permission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Permission.Empty;
        var s = reader.GetString();
        return string.IsNullOrWhiteSpace(s) ? Permission.Empty : new Permission(s);
    }

    public override void Write(Utf8JsonWriter writer, Permission value, JsonSerializerOptions options)
    {
        if (!value.HasValue) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value);
    }
}
