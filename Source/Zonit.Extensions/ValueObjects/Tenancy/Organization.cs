using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Lightweight organization (tenant / workspace) snapshot.
/// </summary>
/// <remarks>
/// <para>Same identity-snapshot pattern as the actor VO: Id is the stable key,
/// <see cref="Name"/> and <see cref="Slug"/> are a lightweight snapshot
/// hydrated by <c>Zonit.Extensions.Databases</c> when the consumer opts-in
/// (<c>repo.Extension(x =&gt; x.Organization).GetAsync()</c>).
/// Entities persist only the Id; the snapshot is re-materialized on read.</para>
///
/// <para>For full operational data (billing, plan, members count, settings) use
/// <c>OrganizationModel</c> from <c>Zonit.Extensions.Organizations</c>.</para>
/// </remarks>
[TypeConverter(typeof(OrganizationTypeConverter))]
[JsonConverter(typeof(OrganizationJsonConverter))]
public readonly struct Organization : IEquatable<Organization>, IParsable<Organization>
{
    public static readonly Organization Empty = default;

    private readonly Guid _id;
    private readonly Title _name;
    private readonly UrlSlug _slug;

    public Guid Id => _id;
    public Title Name => _name;
    public UrlSlug Slug => _slug;

    public bool HasValue => _id != Guid.Empty;

    public bool HasSnapshot => _name.HasValue || _slug.HasValue;

    public Organization(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Organization id must be non-empty.", nameof(id));
        _id = id;
        _name = Title.Empty;
        _slug = UrlSlug.Empty;
    }

    public Organization(Guid id, Title name, UrlSlug slug = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Organization id must be non-empty.", nameof(id));
        _id = id;
        _name = name;
        _slug = slug;
    }

    public static implicit operator Guid(Organization o) => o._id;

    public static implicit operator Organization(Guid id) =>
        id == Guid.Empty ? Empty : new Organization(id);

    public bool Equals(Organization other) => _id.Equals(other._id);
    public override bool Equals(object? obj) => obj is Organization o && Equals(o);
    public override int GetHashCode() => _id.GetHashCode();

    public static bool operator ==(Organization a, Organization b) => a.Equals(b);
    public static bool operator !=(Organization a, Organization b) => !a.Equals(b);

    public override string ToString() => HasSnapshot ? $"{_name.Value} ({_id})" : _id.ToString();

    public static Organization Create(string value) =>
        TryCreate(value, out var o) ? o : throw new FormatException($"Cannot parse '{value}' as Organization.");

    public static bool TryCreate(string? value, out Organization organization)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            organization = Empty;
            return false;
        }
        organization = new Organization(id);
        return true;
    }

    public static Organization Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Organization.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Organization result)
        => TryCreate(s, out result);
}

/// <summary>JSON converter: Guid-string (Id-only) or object <c>{ id, name, slug }</c> (hydrated).</summary>
public sealed class OrganizationJsonConverter : JsonConverter<Organization>
{
    public override Organization Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Organization.Empty;

            case JsonTokenType.String:
            {
                var s = reader.GetString();
                return string.IsNullOrWhiteSpace(s) ? Organization.Empty : Organization.Create(s);
            }

            case JsonTokenType.StartObject:
            {
                Guid id = Guid.Empty;
                string? name = null;
                string? slug = null;

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
                    else if (string.Equals(prop, "slug", StringComparison.OrdinalIgnoreCase))
                        slug = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    else
                        reader.Skip();
                }

                if (id == Guid.Empty) return Organization.Empty;
                return new Organization(
                    id,
                    name is null ? Title.Empty : new Title(name),
                    UrlSlug.TryCreate(slug, out var s) ? s : UrlSlug.Empty);
            }

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for Organization.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Organization value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        if (!value.HasSnapshot)
        {
            writer.WriteStringValue(value.Id);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        if (value.Name.HasValue) writer.WriteString("name", value.Name.Value);
        if (value.Slug.HasValue) writer.WriteString("slug", value.Slug.Value);
        writer.WriteEndObject();
    }
}

/// <summary><see cref="TypeConverter"/> — string Guid → Id-only <see cref="Organization"/>.</summary>
public sealed class OrganizationTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Guid g) return g == Guid.Empty ? Organization.Empty : new Organization(g);
        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Organization.Empty;
            if (Organization.TryCreate(s, out var o)) return o;
            throw new FormatException($"Cannot parse '{s}' as Organization.");
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Organization o)
        {
            if (destinationType == typeof(string)) return o.Id.ToString();
            if (destinationType == typeof(Guid)) return o.Id;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
