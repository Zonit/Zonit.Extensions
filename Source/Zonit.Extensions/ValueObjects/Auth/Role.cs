using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a role name (e.g. <c>"admin"</c>, <c>"editor"</c>, <c>"super-admin"</c>).
/// </summary>
/// <remarks>
/// <para>Counterpart of <see cref="Permission"/>. Differences:</para>
/// <list type="bullet">
///   <item>Role is a single token: <c>"admin"</c>, no dots, no wildcards.</item>
///   <item>Permissions express <i>what</i> (actions / resources), roles express
///         <i>who</i> (named groups of users).</item>
///   <item>An identity carries both — roles are typically projected into a set of
///         permissions at the boundary (policy / authorization layer).</item>
/// </list>
/// <para>Format: <c>[a-z0-9_-]+</c>, length 1..64, normalized to lower-case.</para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Role>))]
[JsonConverter(typeof(RoleJsonConverter))]
public readonly struct Role : IEquatable<Role>, IComparable<Role>, IParsable<Role>, ISpanParsable<Role>
{
    public const int MaxLength = 64;
    public const int MinLength = 1;

    public static readonly Role Empty = default;

    private static readonly Regex Pattern = new(
        @"^[a-z0-9][a-z0-9_\-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string? _value;

    public string Value => _value ?? string.Empty;

    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    public Role(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            throw new ArgumentException($"Role cannot exceed {MaxLength} characters.", nameof(value));

        if (!Pattern.IsMatch(normalized))
            throw new ArgumentException(
                "Role must consist of [a-z0-9_-] characters starting with a letter or digit (e.g. 'admin', 'super-editor').",
                nameof(value));

        _value = normalized;
    }

    public static implicit operator string(Role r) => r.Value;

    public static implicit operator Role(string? value)
        => string.IsNullOrWhiteSpace(value) ? Empty : new Role(value);

    public bool Equals(Role other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Role r && Equals(r);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public int CompareTo(Role other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public static bool operator ==(Role a, Role b) => a.Equals(b);
    public static bool operator !=(Role a, Role b) => !a.Equals(b);
    public static bool operator <(Role a, Role b) => a.CompareTo(b) < 0;
    public static bool operator <=(Role a, Role b) => a.CompareTo(b) <= 0;
    public static bool operator >(Role a, Role b) => a.CompareTo(b) > 0;
    public static bool operator >=(Role a, Role b) => a.CompareTo(b) >= 0;

    public override string ToString() => Value;

    public static Role Create(string value) => new(value);

    public static bool TryCreate(string? value, out Role role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = Empty;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !Pattern.IsMatch(normalized))
        {
            role = Empty;
            return false;
        }

        role = new Role(normalized);
        return true;
    }

    public static Role Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Role.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Role result)
        => TryCreate(s, out result);

    public static Role Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException("Cannot parse as Role.");

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Role result)
        => TryCreate(s.ToString(), out result);
}

/// <summary>JSON converter for <see cref="Role"/> – serializes as plain string.</summary>
public sealed class RoleJsonConverter : JsonConverter<Role>
{
    public override Role Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Role.Empty;
        var s = reader.GetString();
        return string.IsNullOrWhiteSpace(s) ? Role.Empty : new Role(s);
    }

    public override void Write(Utf8JsonWriter writer, Role value, JsonSerializerOptions options)
    {
        if (!value.HasValue) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value);
    }
}
