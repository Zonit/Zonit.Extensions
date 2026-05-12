using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a user-supplied credential. Recognized kinds:
/// <list type="bullet">
///   <item><b>Id</b> — GUID-formatted identifier (machine-issued).</item>
///   <item><b>Email</b> — <c>"jan@example.com"</c>.</item>
///   <item><b>Phone</b> — <c>"+48600700800"</c> (E.164-like).</item>
///   <item><b>Username</b> — <c>"jkowalski"</c> (alphanumeric).</item>
/// </list>
/// The <see cref="Kind"/> is auto-detected from the value itself —
/// the user types once, the system figures out what they mean.
/// </summary>
/// <remarks>
/// <para>Used at the identity boundary: login forms, invitations, password recovery.
/// Not part of the public identity snapshot — credentials are always loaded on demand by
/// dedicated services, never cached alongside identity data.</para>
/// <para>Persistence: store the raw <see cref="Value"/> (normalized lower-case).
/// <see cref="Kind"/> is derived and need not be persisted — it is re-computed on load.</para>
/// <para>AOT-safe: no dynamic code, hand-written <see cref="JsonConverter{T}"/>.</para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Credential>))]
[JsonConverter(typeof(CredentialJsonConverter))]
public readonly struct Credential : IEquatable<Credential>, IComparable<Credential>, IParsable<Credential>, ISpanParsable<Credential>
{
    /// <summary>Max total length (accommodates long e-mails).</summary>
    public const int MaxLength = 254;

    /// <summary>Min length (e.g. 3-char username).</summary>
    public const int MinLength = 3;

    /// <summary>Empty credential (<c>default(Credential)</c>).</summary>
    public static readonly Credential Empty = default;

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // E.164-friendly: optional leading '+', then 7-15 digits (spaces/dashes allowed in input, stripped on detection).
    private static readonly Regex PhonePattern = new(
        @"^\+?[0-9]{7,15}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UsernamePattern = new(
        @"^[a-z0-9][a-z0-9._\-]{2,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string? _value;
    private readonly CredentialKind _kind;
    private readonly Guid _id;

    /// <summary>Normalized credential value. Never null.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Auto-detected credential kind.</summary>
    public CredentialKind Kind => _kind;

    /// <summary>True when a non-empty credential is present.</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>Shortcut for <c>Kind == CredentialKind.Email</c>.</summary>
    public bool IsEmail => _kind == CredentialKind.Email;

    /// <summary>Shortcut for <c>Kind == CredentialKind.Phone</c>.</summary>
    public bool IsPhone => _kind == CredentialKind.Phone;

    /// <summary>Shortcut for <c>Kind == CredentialKind.Username</c>.</summary>
    public bool IsUsername => _kind == CredentialKind.Username;

    /// <summary>Shortcut for <c>Kind == CredentialKind.Id</c>.</summary>
    public bool IsId => _kind == CredentialKind.Id;

    /// <summary>
    /// Parsed Guid when <see cref="Kind"/> is <see cref="CredentialKind.Id"/>;
    /// <see cref="Guid.Empty"/> otherwise.
    /// </summary>
    public Guid Id => _id;

    /// <summary>
    /// Creates a credential from raw input. Trims, lower-cases, and auto-detects
    /// <see cref="Kind"/>. Phone numbers have spaces, dashes, and parentheses stripped.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when input cannot be classified
    /// or is out of length bounds.</exception>
    public Credential(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var (normalized, kind, id) = Normalize(value);

        if (kind == CredentialKind.Unknown)
            throw new ArgumentException(
                $"Value '{value}' is not a valid id, email, phone number, or username.",
                nameof(value));

        if (normalized.Length < MinLength)
            throw new ArgumentException($"Credential must be at least {MinLength} characters.", nameof(value));

        if (normalized.Length > MaxLength)
            throw new ArgumentException($"Credential cannot exceed {MaxLength} characters.", nameof(value));

        _value = normalized;
        _kind = kind;
        _id = id;
    }

    /// <summary>
    /// Creates a credential from a Guid identifier (machine-issued).
    /// <see cref="Kind"/> will be <see cref="CredentialKind.Id"/>.
    /// </summary>
    public Credential(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Credential id must be non-empty.", nameof(id));
        _value = id.ToString("D");
        _kind = CredentialKind.Id;
        _id = id;
    }

    private Credential(string normalizedValue, CredentialKind kind, Guid id = default)
    {
        _value = normalizedValue;
        _kind = kind;
        _id = id;
    }

    private static (string Value, CredentialKind Kind, Guid Id) Normalize(string raw)
    {
        var trimmed = raw.Trim();

        // 1. Guid first (most specific format).
        if (Guid.TryParse(trimmed, out var guid) && guid != Guid.Empty)
            return (guid.ToString("D"), CredentialKind.Id, guid);

        // 2. Phone: strip formatting characters, then validate.
        var digitsOnly = StripPhoneFormatting(trimmed);
        if (PhonePattern.IsMatch(digitsOnly))
            return (digitsOnly, CredentialKind.Phone, Guid.Empty);

        var lower = trimmed.ToLowerInvariant();

        // 3. Email.
        if (EmailPattern.IsMatch(lower))
            return (lower, CredentialKind.Email, Guid.Empty);

        // 4. Username (last resort).
        if (UsernamePattern.IsMatch(lower))
            return (lower, CredentialKind.Username, Guid.Empty);

        return (lower, CredentialKind.Unknown, Guid.Empty);
    }

    private static string StripPhoneFormatting(string input)
    {
        Span<char> buffer = stackalloc char[input.Length];
        int len = 0;
        foreach (var c in input)
        {
            if (c == ' ' || c == '-' || c == '(' || c == ')') continue;
            buffer[len++] = c;
        }
        return new string(buffer[..len]);
    }

    public static implicit operator string(Credential c) => c.Value;

    public static implicit operator Credential(string? value)
        => string.IsNullOrWhiteSpace(value) ? Empty : new Credential(value);

    public bool Equals(Credential other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is Credential c && Equals(c);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public int CompareTo(Credential other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public static bool operator ==(Credential a, Credential b) => a.Equals(b);
    public static bool operator !=(Credential a, Credential b) => !a.Equals(b);
    public static bool operator <(Credential a, Credential b) => a.CompareTo(b) < 0;
    public static bool operator <=(Credential a, Credential b) => a.CompareTo(b) <= 0;
    public static bool operator >(Credential a, Credential b) => a.CompareTo(b) > 0;
    public static bool operator >=(Credential a, Credential b) => a.CompareTo(b) >= 0;

    public override string ToString() => Value;

    public static Credential Create(string value) => new(value);

    public static bool TryCreate(string? value, out Credential credential)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            credential = Empty;
            return false;
        }

        var (normalized, kind, id) = Normalize(value);
        if (kind == CredentialKind.Unknown || normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            credential = Empty;
            return false;
        }

        credential = new Credential(normalized, kind, id);
        return true;
    }

    public static Credential Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Credential.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Credential result)
        => TryCreate(s, out result);

    public static Credential Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException("Cannot parse as Credential.");

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Credential result)
        => TryCreate(s.ToString(), out result);
}

/// <summary>Credential kind auto-detected from the input value.</summary>
public enum CredentialKind
{
    /// <summary>Not a recognized credential format.</summary>
    Unknown = 0,
    /// <summary>Local user name (alphanumeric, 3-64 chars).</summary>
    Username = 1,
    /// <summary>E-mail address.</summary>
    Email = 2,
    /// <summary>Phone number (E.164-like, digits + optional leading '+').</summary>
    Phone = 3,
    /// <summary>Machine-issued GUID identifier.</summary>
    Id = 4,
}

/// <summary>JSON converter for <see cref="Credential"/> – serializes as plain string.</summary>
public sealed class CredentialJsonConverter : JsonConverter<Credential>
{
    public override Credential Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Credential.Empty;
        var s = reader.GetString();
        return string.IsNullOrWhiteSpace(s) ? Credential.Empty : new Credential(s);
    }

    public override void Write(Utf8JsonWriter writer, Credential value, JsonSerializerOptions options)
    {
        if (!value.HasValue) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value);
    }
}
