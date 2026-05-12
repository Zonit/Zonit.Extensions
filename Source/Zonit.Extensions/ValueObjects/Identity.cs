using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Lightweight identity snapshot: an actor (user / service / author / reviewer) the code
/// interacts with. Carries only the <b>must-have</b> fields.
/// </summary>
/// <remarks>
/// <para>Usage sites:</para>
/// <list type="bullet">
///   <item>Domain entities: <c>public Identity Author { get; init; }</c> – several actors per entity
///     with semantically different roles (Author, Reviewer, CreatedBy, ModifiedBy).</item>
///   <item>Application layer: <c>IAuthenticatedProvider.User</c> may return <see cref="Identity"/>.</item>
///   <item>UI: display name / role / permission checks without a round-trip to the backend.</item>
/// </list>
///
/// <para><b>Persistence (EF Core)</b>: store only <see cref="Id"/> (Guid) as the column value.
/// The snapshot (<see cref="Name"/>, <see cref="Roles"/>, <see cref="Permissions"/>) is
/// re-hydrated by <c>Zonit.Extensions.Databases</c> extensions when the consumer opts-in:
/// <code>repo.Extension(x =&gt; x.Author).GetAsync();</code>
/// Without the Extension call, only <see cref="Id"/> is populated and <see cref="HasSnapshot"/>
/// is <c>false</c>. This keeps VOs pure — no hidden I/O, no lazy loading.</para>
///
/// <para><b>Credentials</b> (e-mail, phone) are <i>not</i> part of <see cref="Identity"/> —
/// use <see cref="Credential"/> and a dedicated service. The VO stays small and
/// the attack surface of cached actor data narrow.</para>
///
/// <para><b>Equality</b>: by <see cref="Id"/> only. Two identities with the same <see cref="Id"/>
/// compare equal regardless of snapshot completeness.</para>
///
/// <para><b>AOT-safe</b>: hand-written converters, no dynamic code, no reflection.</para>
/// </remarks>
[TypeConverter(typeof(IdentityTypeConverter))]
[JsonConverter(typeof(IdentityJsonConverter))]
public readonly struct Identity : IEquatable<Identity>, IParsable<Identity>
{
    /// <summary>Empty identity (<c>default(Identity)</c>; <see cref="Id"/> is <see cref="Guid.Empty"/>).</summary>
    public static readonly Identity Empty = default;

    private readonly Guid _id;
    private readonly Title _name;
    private readonly ImmutableArray<Role> _roles;
    private readonly ImmutableArray<Permission> _permissions;

    /// <summary>Stable identifier of the actor.</summary>
    public Guid Id => _id;

    /// <summary>Display name (may be empty if Id-only, pre-hydration).</summary>
    public Title Name => _name;

    /// <summary>Roles the actor belongs to.</summary>
    public ImmutableArray<Role> Roles => _roles.IsDefault ? ImmutableArray<Role>.Empty : _roles;

    /// <summary>Permissions explicitly granted to the actor.</summary>
    public ImmutableArray<Permission> Permissions =>
        _permissions.IsDefault ? ImmutableArray<Permission>.Empty : _permissions;

    /// <summary>True when <see cref="Id"/> is non-empty.</summary>
    public bool HasValue => _id != Guid.Empty;

    /// <summary>
    /// True when the snapshot is hydrated (has <see cref="Name"/> or any roles/permissions).
    /// False for Id-only identities built before <c>IDatabaseExtension</c> materialization.
    /// </summary>
    public bool HasSnapshot =>
        _name.HasValue ||
        (!_roles.IsDefault && _roles.Length > 0) ||
        (!_permissions.IsDefault && _permissions.Length > 0);

    /// <summary>Id-only constructor (no snapshot).</summary>
    public Identity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Identity id must be non-empty.", nameof(id));
        _id = id;
        _name = Title.Empty;
        _roles = ImmutableArray<Role>.Empty;
        _permissions = ImmutableArray<Permission>.Empty;
    }

    /// <summary>Fully hydrated constructor.</summary>
    public Identity(
        Guid id,
        Title name,
        IEnumerable<Role>? roles = null,
        IEnumerable<Permission>? permissions = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Identity id must be non-empty.", nameof(id));
        _id = id;
        _name = name;
        _roles = roles is null ? ImmutableArray<Role>.Empty : [.. roles];
        _permissions = permissions is null ? ImmutableArray<Permission>.Empty : [.. permissions];
    }

    /// <summary>Checks role membership (ordinal).</summary>
    public bool IsInRole(Role role)
    {
        if (!role.HasValue) return false;
        foreach (var r in Roles)
            if (r == role) return true;
        return false;
    }

    /// <summary>
    /// Checks whether this identity has a permission that implies <paramref name="permission"/>
    /// (wildcards respected — see <see cref="Permission.Implies"/>).
    /// </summary>
    public bool HasPermission(Permission permission)
    {
        if (!permission.HasValue) return true;
        foreach (var granted in Permissions)
            if (granted.Implies(permission)) return true;
        return false;
    }

    /// <summary>Ergonomic conversion so entities can persist only the Id.</summary>
    public static implicit operator Guid(Identity identity) => identity._id;

    /// <summary>Ergonomic conversion: <c>Identity i = someGuid;</c>.</summary>
    public static implicit operator Identity(Guid id) => id == Guid.Empty ? Empty : new Identity(id);

    public bool Equals(Identity other) => _id.Equals(other._id);
    public override bool Equals(object? obj) => obj is Identity i && Equals(i);
    public override int GetHashCode() => _id.GetHashCode();

    public static bool operator ==(Identity a, Identity b) => a.Equals(b);
    public static bool operator !=(Identity a, Identity b) => !a.Equals(b);

    public override string ToString() => HasSnapshot ? $"{_name.Value} ({_id})" : _id.ToString();

    /// <summary>Creates an Id-only identity from a Guid string.</summary>
    public static Identity Create(string value) =>
        TryCreate(value, out var i) ? i : throw new FormatException($"Cannot parse '{value}' as Identity.");

    /// <summary>
    /// Tries to parse a string-encoded Guid into an Id-only identity. Composite identity
    /// (with snapshot) is never produced from a single string — only from JSON or constructors.
    /// </summary>
    public static bool TryCreate(string? value, out Identity identity)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            identity = Empty;
            return false;
        }

        identity = new Identity(id);
        return true;
    }

    public static Identity Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Identity.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Identity result)
        => TryCreate(s, out result);
}

/// <summary>
/// JSON converter: an <see cref="Identity"/> is serialized either as a plain Guid string
/// (Id-only) or as an object <c>{ id, name, roles, permissions }</c> (hydrated).
/// Reads support both shapes.
/// </summary>
public sealed class IdentityJsonConverter : JsonConverter<Identity>
{
    public override Identity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Identity.Empty;

            case JsonTokenType.String:
            {
                var s = reader.GetString();
                return string.IsNullOrWhiteSpace(s) ? Identity.Empty : Identity.Create(s);
            }

            case JsonTokenType.StartObject:
            {
                Guid id = Guid.Empty;
                string? name = null;
                List<Role>? roles = null;
                List<Permission>? permissions = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    var prop = reader.GetString();
                    reader.Read();

                    if (string.Equals(prop, "id", StringComparison.OrdinalIgnoreCase))
                        id = reader.GetGuid();
                    else if (string.Equals(prop, "name", StringComparison.OrdinalIgnoreCase))
                        name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    else if (string.Equals(prop, "roles", StringComparison.OrdinalIgnoreCase))
                    {
                        roles = [];
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    var r = reader.GetString();
                                    if (Role.TryCreate(r, out var role)) roles.Add(role);
                                }
                            }
                        }
                    }
                    else if (string.Equals(prop, "permissions", StringComparison.OrdinalIgnoreCase))
                    {
                        permissions = [];
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    var p = reader.GetString();
                                    if (Permission.TryCreate(p, out var perm))
                                        permissions.Add(perm);
                                }
                            }
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (id == Guid.Empty) return Identity.Empty;
                return new Identity(
                    id,
                    name is null ? Title.Empty : new Title(name),
                    roles,
                    permissions);
            }

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for Identity.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Identity value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        if (!value.HasSnapshot)
        {
            // Id-only: emit as plain string to keep payloads minimal.
            writer.WriteStringValue(value.Id);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        if (value.Name.HasValue) writer.WriteString("name", value.Name.Value);

        if (value.Roles.Length > 0)
        {
            writer.WriteStartArray("roles");
            foreach (var r in value.Roles) writer.WriteStringValue(r.Value);
            writer.WriteEndArray();
        }

        if (value.Permissions.Length > 0)
        {
            writer.WriteStartArray("permissions");
            foreach (var p in value.Permissions) writer.WriteStringValue(p.Value);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// <see cref="TypeConverter"/> for model binding / configuration: converts a Guid-formatted
/// string into an Id-only <see cref="Identity"/>. Snapshot hydration is not performed.
/// </summary>
public sealed class IdentityTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Guid g) return g == Guid.Empty ? Identity.Empty : new Identity(g);
        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Identity.Empty;
            if (Identity.TryCreate(s, out var i)) return i;
            throw new FormatException($"Cannot parse '{s}' as Identity.");
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Identity i)
        {
            if (destinationType == typeof(string)) return i.Id.ToString();
            if (destinationType == typeof(Guid)) return i.Id;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
