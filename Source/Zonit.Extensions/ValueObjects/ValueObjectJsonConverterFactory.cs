using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// JSON converter factory for value objects.
/// Automatically handles serialization/deserialization of Title, Description, Content, Url, UrlSlug, Culture, Price, Asset.
/// </summary>
public sealed class ValueObjectJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(Title) ||
               typeToConvert == typeof(Description) ||
               typeToConvert == typeof(Content) ||
               typeToConvert == typeof(Url) ||
               typeToConvert == typeof(UrlSlug) ||
               typeToConvert == typeof(Culture) ||
               typeToConvert == typeof(Price) ||
               typeToConvert == typeof(Asset);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(Title))
            return new TitleJsonConverter();
        if (typeToConvert == typeof(Description))
            return new DescriptionJsonConverter();
        if (typeToConvert == typeof(Content))
            return new ContentJsonConverter();
        if (typeToConvert == typeof(Url))
            return new UrlJsonConverter();
        if (typeToConvert == typeof(UrlSlug))
            return new UrlSlugJsonConverter();
        if (typeToConvert == typeof(Culture))
            return new CultureJsonConverter();
        if (typeToConvert == typeof(Price))
            return new PriceJsonConverter();
        if (typeToConvert == typeof(Asset))
            return new AssetJsonConverter();

        throw new NotSupportedException($"Cannot create converter for {typeToConvert}");
    }
}

/// <summary>
/// JSON converter for Title value object.
/// </summary>
public sealed class TitleJsonConverter : JsonConverter<Title>
{
    /// <inheritdoc />
    public override Title Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Title.TryCreate(value, out var title) ? title : Title.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Title value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// JSON converter for Description value object.
/// </summary>
public sealed class DescriptionJsonConverter : JsonConverter<Description>
{
    /// <inheritdoc />
    public override Description Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Description.TryCreate(value, out var description) ? description : Description.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Description value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// JSON converter for Url value object.
/// </summary>
public sealed class UrlJsonConverter : JsonConverter<Url>
{
    /// <inheritdoc />
    public override Url Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Url.TryCreate(value, out var url) ? url : Url.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Url value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// JSON converter for UrlSlug value object.
/// </summary>
public sealed class UrlSlugJsonConverter : JsonConverter<UrlSlug>
{
    /// <inheritdoc />
    public override UrlSlug Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return UrlSlug.TryCreate(value, out var slug) ? slug : UrlSlug.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, UrlSlug value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// JSON converter for Culture value object.
/// </summary>
public sealed class CultureJsonConverter : JsonConverter<Culture>
{
    /// <inheritdoc />
    public override Culture Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Culture.TryCreate(value, out var culture) ? culture : Culture.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Culture value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// JSON converter for Content value object.
/// </summary>
public sealed class ContentJsonConverter : JsonConverter<Content>
{
    /// <inheritdoc />
    public override Content Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Content.TryCreate(value, out var content) ? content : Content.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Content value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
