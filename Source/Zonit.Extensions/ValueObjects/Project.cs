using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Lightweight project snapshot. <b>Note</b>: this VO also covers the "catalog" concept —
/// in our domain <i>a catalog is a project</i>, we intentionally use a single name
/// to avoid duplication.
/// </summary>
/// <remarks>
/// Same identity-snapshot pattern as <see cref="Organization"/>: entities persist
/// only <see cref="Id"/>, the snapshot (<see cref="Name"/>, <see cref="Slug"/>) is
/// rehydrated on demand via <c>Zonit.Extensions.Databases</c>
/// (<c>repo.Extension(x =&gt; x.Project).GetAsync()</c>).
/// Rich data (<c>ProjectModel</c>) lives in <c>Zonit.Extensions.Projects</c>.
/// </remarks>
[TypeConverter(typeof(ProjectTypeConverter))]
[JsonConverter(typeof(ProjectJsonConverter))]
public readonly struct Project : IEquatable<Project>, IParsable<Project>
{
    public static readonly Project Empty = default;

    private readonly Guid _id;
    private readonly Title _name;
    private readonly UrlSlug _slug;

    public Guid Id => _id;
    public Title Name => _name;
    public UrlSlug Slug => _slug;

    public bool HasValue => _id != Guid.Empty;

    public bool HasSnapshot => _name.HasValue || _slug.HasValue;

    public Project(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Project id must be non-empty.", nameof(id));
        _id = id;
        _name = Title.Empty;
        _slug = UrlSlug.Empty;
    }

    public Project(Guid id, Title name, UrlSlug slug = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Project id must be non-empty.", nameof(id));
        _id = id;
        _name = name;
        _slug = slug;
    }

    public static implicit operator Guid(Project p) => p._id;

    public static implicit operator Project(Guid id) =>
        id == Guid.Empty ? Empty : new Project(id);

    public bool Equals(Project other) => _id.Equals(other._id);
    public override bool Equals(object? obj) => obj is Project p && Equals(p);
    public override int GetHashCode() => _id.GetHashCode();

    public static bool operator ==(Project a, Project b) => a.Equals(b);
    public static bool operator !=(Project a, Project b) => !a.Equals(b);

    public override string ToString() => HasSnapshot ? $"{_name.Value} ({_id})" : _id.ToString();

    public static Project Create(string value) =>
        TryCreate(value, out var p) ? p : throw new FormatException($"Cannot parse '{value}' as Project.");

    public static bool TryCreate(string? value, out Project project)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            project = Empty;
            return false;
        }
        project = new Project(id);
        return true;
    }

    public static Project Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var r) ? r : throw new FormatException($"Cannot parse '{s}' as Project.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Project result)
        => TryCreate(s, out result);
}

/// <summary>JSON converter: Guid-string (Id-only) or object <c>{ id, name, slug }</c> (hydrated).</summary>
public sealed class ProjectJsonConverter : JsonConverter<Project>
{
    public override Project Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Project.Empty;

            case JsonTokenType.String:
            {
                var s = reader.GetString();
                return string.IsNullOrWhiteSpace(s) ? Project.Empty : Project.Create(s);
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

                if (id == Guid.Empty) return Project.Empty;
                return new Project(
                    id,
                    name is null ? Title.Empty : new Title(name),
                    UrlSlug.TryCreate(slug, out var s) ? s : UrlSlug.Empty);
            }

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for Project.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Project value, JsonSerializerOptions options)
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

/// <summary><see cref="TypeConverter"/> — string Guid → Id-only <see cref="Project"/>.</summary>
public sealed class ProjectTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Guid g) return g == Guid.Empty ? Project.Empty : new Project(g);
        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Project.Empty;
            if (Project.TryCreate(s, out var p)) return p;
            throw new FormatException($"Cannot parse '{s}' as Project.");
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Project p)
        {
            if (destinationType == typeof(string)) return p.Id.ToString();
            if (destinationType == typeof(Guid)) return p.Id;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
