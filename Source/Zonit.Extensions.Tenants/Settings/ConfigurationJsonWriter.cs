using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Renders an <see cref="IConfigurationSection"/> into the JSON a setting model deserialises from,
/// so an <c>appsettings.json</c> block and a persisted <c>Tenant.Variables</c> blob travel the
/// exact same code path.
/// </summary>
/// <remarks>
/// <para><b>Why not <c>IConfiguration.Bind</c>.</b> The built-in binder is reflection-based, and
/// the configuration binding <i>source generator</i> that would make it AOT-safe works by
/// intercepting call sites where the bound type is statically known. Our call site lives in this
/// library behind an open generic, and a generator cannot see another generator's output anyway —
/// so <c>Bind</c> would hand back an AOT hole plus a second, subtly different set of conversion
/// rules to keep in sync with JSON. Going through JSON instead means one contract: the same
/// camelCase-out / any-case-in matching, the same converters, the same
/// <see cref="Setting{T}.Hydrate"/> override if a setting needs one.</para>
///
/// <para><b>Configuration is stringly-typed; JSON is not.</b> Every value out of
/// <see cref="IConfiguration"/> is a <see cref="string"/>, including <c>"true"</c> and <c>"42"</c>.
/// Guessing token types from the text is what makes naive converters mangle
/// <c>"title": "2026"</c> into a number, so the expected CLR type is taken from
/// <see cref="JsonTypeInfo.Properties"/> instead — metadata the source generator already emitted,
/// with no reflection and no trim warnings. When a subtree has no metadata (an unregistered
/// nested model on a non-AOT host) it degrades to the text heuristic rather than failing.</para>
///
/// <para><b>Shape detection</b> follows configuration's own conventions: a section with no
/// children is a value, a section whose children are <c>"0"</c>, <c>"1"</c>, <c>"2"</c>… is an
/// array (that is how JSON arrays and <c>Key:0=…</c> environment variables both arrive), anything
/// else is an object.</para>
/// </remarks>
internal static class ConfigurationJsonWriter
{
    /// <summary>
    /// Renders <paramref name="section"/> as a JSON document shaped for <paramref name="typeInfo"/>.
    /// </summary>
    /// <param name="section">The configuration subtree holding one setting's values.</param>
    /// <param name="typeInfo">
    /// Metadata for the setting's model, or <see langword="null"/> when none is registered — in
    /// which case scalar types are inferred from their text.
    /// </param>
    public static string Write(IConfigurationSection section, JsonTypeInfo? typeInfo)
    {
        var buffer = new ArrayBufferWriter<byte>(256);

        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteSection(writer, section, typeInfo?.Type, typeInfo?.Options);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteSection(
        Utf8JsonWriter writer,
        IConfigurationSection section,
        Type? expected,
        JsonSerializerOptions? options)
    {
        var children = section.GetChildren().ToArray();

        if (children.Length == 0)
        {
            WriteScalar(writer, section.Value, expected);
            return;
        }

        if (IsArray(children))
        {
            WriteArray(writer, children, expected, options);
            return;
        }

        WriteObject(writer, children, expected, options);
    }

    private static void WriteObject(
        Utf8JsonWriter writer,
        IConfigurationSection[] children,
        Type? expected,
        JsonSerializerOptions? options)
    {
        // Property metadata for this level, when the model type is registered. Absent metadata is
        // not an error — every child then falls back to the text heuristic.
        var properties = expected is not null && options is not null
            && options.TryGetTypeInfo(expected, out var info)
            ? info.Properties
            : null;

        writer.WriteStartObject();

        foreach (var child in children)
        {
            // The configuration key is written verbatim. Casing does not have to be reconciled
            // here because the reader matches property names case-insensitively — which is what
            // lets appsettings.json use the natural "Title" while blobs use "title".
            writer.WritePropertyName(child.Key);

            Type? childType = null;
            if (properties is not null)
            {
                foreach (var property in properties)
                {
                    if (string.Equals(property.Name, child.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        childType = property.PropertyType;
                        break;
                    }
                }
            }

            WriteSection(writer, child, childType, options);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(
        Utf8JsonWriter writer,
        IConfigurationSection[] children,
        Type? expected,
        JsonSerializerOptions? options)
    {
        var elementType = ElementTypeOf(expected);

        writer.WriteStartArray();

        // Ordered by numeric index rather than by the provider's ordering, which is lexicographic
        // and would put "10" before "2".
        foreach (var child in children.OrderBy(c => int.Parse(c.Key, System.Globalization.CultureInfo.InvariantCulture)))
            WriteSection(writer, child, elementType, options);

        writer.WriteEndArray();
    }

    /// <summary>Consecutive integer keys from zero — configuration's array convention.</summary>
    private static bool IsArray(IConfigurationSection[] children)
    {
        var seen = new bool[children.Length];

        foreach (var child in children)
        {
            if (!int.TryParse(child.Key, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var index))
                return false;

            if (index < 0 || index >= children.Length || seen[index])
                return false;

            seen[index] = true;
        }

        return children.Length > 0;
    }

    private static Type? ElementTypeOf(Type? collection)
    {
        if (collection is null) return null;
        if (collection.IsArray) return collection.GetElementType();
        if (collection.IsGenericType)
        {
            var arguments = collection.GetGenericArguments();
            if (arguments.Length == 1) return arguments[0];
        }
        return null;
    }

    /// <summary>
    /// Writes one configuration value as the JSON token its target type expects.
    /// </summary>
    /// <remarks>
    /// Anything not explicitly handled is written as a JSON string, which is correct for
    /// <see cref="string"/> and for every type System.Text.Json reads out of one —
    /// <see cref="Guid"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>,
    /// <see cref="TimeSpan"/>, <see cref="Uri"/>, <see cref="char"/>. Getting it wrong is not
    /// silent: a mismatch surfaces as a <see cref="JsonException"/>, which the caller logs and
    /// raises on <c>OnSettingHydrationFailed</c>.
    /// </remarks>
    private static void WriteScalar(Utf8JsonWriter writer, string? value, Type? expected)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var type = expected is null ? null : Nullable.GetUnderlyingType(expected) ?? expected;

        if (type is null)
        {
            WriteInferred(writer, value);
            return;
        }

        if (type == typeof(string))
        {
            writer.WriteStringValue(value);
            return;
        }

        if (type == typeof(bool))
        {
            if (bool.TryParse(value, out var flag)) writer.WriteBooleanValue(flag);
            else writer.WriteStringValue(value);
            return;
        }

        if (type.IsEnum)
        {
            WriteEnum(writer, value, type);
            return;
        }

        if (IsNumeric(type))
        {
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                writer.WriteNumberValue(number);
                return;
            }

            // Not a number after all — write it as text so the failure is a JsonException that
            // names the property, rather than malformed JSON that names nothing.
            writer.WriteStringValue(value);
            return;
        }

        writer.WriteStringValue(value);
    }

    /// <summary>
    /// Enums are persisted as numbers (no <c>JsonStringEnumConverter</c> is in play), but nobody
    /// writes <c>"Severity": 2</c> in an appsettings file by choice. Both spellings are accepted
    /// here and normalised to the number the reader expects.
    /// </summary>
    private static void WriteEnum(Utf8JsonWriter writer, string value, Type enumType)
    {
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var numeric))
        {
            writer.WriteNumberValue(numeric);
            return;
        }

        if (Enum.TryParse(enumType, value, ignoreCase: true, out var parsed) && parsed is IConvertible convertible)
        {
            writer.WriteNumberValue(convertible.ToInt64(System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        // Unknown member name. Written as a string so it fails as a JsonException naming this
        // property instead of silently landing on the zero member.
        writer.WriteStringValue(value);
    }

    /// <summary>
    /// Last-resort typing for a subtree with no metadata. Only reachable on a host that also has
    /// no metadata for the model itself, i.e. one already hydrating reflectively.
    /// </summary>
    private static void WriteInferred(Utf8JsonWriter writer, string value)
    {
        if (bool.TryParse(value, out var flag))
        {
            writer.WriteBooleanValue(flag);
            return;
        }

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            writer.WriteNumberValue(number);
            return;
        }

        writer.WriteStringValue(value);
    }

    private static bool IsNumeric(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte)
        || type == typeof(double) || type == typeof(float) || type == typeof(decimal);
}
