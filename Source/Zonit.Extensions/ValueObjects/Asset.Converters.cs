using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Type converter for Asset (for model binding, Blazor, etc.).
/// Supports conversion from byte[], Stream, MemoryStream.
/// </summary>
public sealed class AssetTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(byte[]) ||
        sourceType == typeof(Stream) ||
        sourceType == typeof(MemoryStream) ||
        base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(byte[]) ||
        destinationType == typeof(Stream) ||
        destinationType == typeof(MemoryStream) ||
        base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value switch
        {
            byte[] bytes => new Asset(bytes),
            MemoryStream ms => new Asset(ms),
            Stream stream => new Asset(stream),
            _ => base.ConvertFrom(context, culture, value)
        };
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Asset asset)
        {
            if (destinationType == typeof(byte[]))
                return asset.Data;
            if (destinationType == typeof(Stream) || destinationType == typeof(MemoryStream))
                return asset.ToStream();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>
/// JSON converter for Asset value object.
/// Supports both simple Base64 string and full object format.
/// </summary>
public sealed class AssetJsonConverter : JsonConverter<Asset>
{
    /// <inheritdoc />
    public override Asset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Asset.Empty;

        // Handle simple base64 string
        if (reader.TokenType == JsonTokenType.String)
        {
            var base64 = reader.GetString();
            if (string.IsNullOrEmpty(base64))
                return Asset.Empty;

            return Asset.FromBase64(base64);
        }

        // Handle object format
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected null, string, or object.");

        Guid? id = null;
        string? originalName = null;
        string? mimeType = null;
        string? data = null;
        DateTime? createdAt = null;
        string? sha256 = null;
        string? md5 = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "id":
                    if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var parsedId))
                        id = parsedId;
                    break;
                case "name":
                case "originalname":
                    originalName = reader.GetString();
                    break;
                case "mimetype":
                case "mime":
                case "contenttype":
                    mimeType = reader.GetString();
                    break;
                case "data":
                case "content":
                case "bytes":
                    data = reader.GetString();
                    break;
                case "createdat":
                case "created":
                    if (reader.TokenType == JsonTokenType.String && DateTime.TryParse(reader.GetString(), out var parsedDate))
                        createdAt = parsedDate;
                    break;
                case "sha256":
                case "hash":
                    sha256 = reader.GetString();
                    break;
                case "md5":
                    md5 = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (string.IsNullOrEmpty(data))
            return Asset.Empty;

        var bytes = Convert.FromBase64String(data);
        var fileName = !string.IsNullOrWhiteSpace(originalName)
            ? new Asset.FileName(originalName)
            : new Asset.FileName($"{Guid.NewGuid():N}.bin");

        var mime = !string.IsNullOrWhiteSpace(mimeType)
            ? new Asset.MimeType(mimeType)
            : Asset.MimeType.OctetStream;

        // If we have all metadata, use internal constructor (legacy format)
        if (id.HasValue && createdAt.HasValue)
        {
            return new Asset(bytes, fileName, mime, id.Value, createdAt.Value, sha256, md5);
        }

        // Otherwise create new Asset (generates new Id/CreatedAt, detects MimeType from signature)
        return new Asset(bytes, fileName, mime);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Asset value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("originalName", value.OriginalName.Value);
        writer.WriteString("uniqueName", value.UniqueName);
        writer.WriteString("mimeType", value.MediaType.Value);
        writer.WriteString("signature", value.Signature.ToString());
        writer.WriteString("extension", value.Extension);
        writer.WriteNumber("sizeBytes", value.Size.Bytes);
        writer.WriteString("size", value.Size.ToString());
        writer.WriteString("createdAt", value.CreatedAt.ToString("O"));
        writer.WriteString("sha256", value.Sha256);
        writer.WriteString("md5", value.Md5);
        writer.WriteString("category", value.Category.ToString());
        writer.WriteString("data", value.Base64);
        writer.WriteEndObject();
    }
}
