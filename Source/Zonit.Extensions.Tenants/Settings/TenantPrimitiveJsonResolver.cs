using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Source-generated-equivalent metadata for the scalar types a setting model's properties can
/// have. Always last in the resolver chain.
/// </summary>
/// <remarks>
/// <para><b>Why it is hand-written here rather than generated.</b> A
/// <see cref="JsonTypeInfo"/> built through <see cref="JsonMetadataServices"/> resolves each of its
/// properties' type infos through the same options, so emitting metadata for a model is not enough
/// — <c>string</c>, <c>int</c>, <c>bool</c> and friends need entries too, or configuring the model
/// throws. System.Text.Json's own generator emits one file per such type into every context, which
/// is why a generated context folder is full of <c>Context.Boolean.g.cs</c>. That set is closed and
/// identical in every assembly, so it lives here once instead of being copied into everyone's
/// generated output. Only <b>enums</b> stay per-assembly, because
/// <see cref="JsonMetadataServices.GetEnumConverter{T}"/> needs the closed generic.</para>
///
/// <para>Everything below is a <see cref="JsonMetadataServices"/> built-in converter — no
/// reflection, no <c>MakeGenericType</c>, nothing the trimmer cannot follow.</para>
/// </remarks>
internal sealed class TenantPrimitiveJsonResolver : IJsonTypeInfoResolver
{
    internal static readonly TenantPrimitiveJsonResolver Default = new();

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Reference types and non-nullable value types.
        if (type == typeof(string)) return JsonMetadataServices.CreateValueInfo<string>(options, JsonMetadataServices.StringConverter);
        if (type == typeof(Uri)) return JsonMetadataServices.CreateValueInfo<Uri>(options, JsonMetadataServices.UriConverter);
        if (type == typeof(Version)) return JsonMetadataServices.CreateValueInfo<Version>(options, JsonMetadataServices.VersionConverter);

        if (type == typeof(bool)) return JsonMetadataServices.CreateValueInfo<bool>(options, JsonMetadataServices.BooleanConverter);
        if (type == typeof(byte)) return JsonMetadataServices.CreateValueInfo<byte>(options, JsonMetadataServices.ByteConverter);
        if (type == typeof(sbyte)) return JsonMetadataServices.CreateValueInfo<sbyte>(options, JsonMetadataServices.SByteConverter);
        if (type == typeof(short)) return JsonMetadataServices.CreateValueInfo<short>(options, JsonMetadataServices.Int16Converter);
        if (type == typeof(ushort)) return JsonMetadataServices.CreateValueInfo<ushort>(options, JsonMetadataServices.UInt16Converter);
        if (type == typeof(int)) return JsonMetadataServices.CreateValueInfo<int>(options, JsonMetadataServices.Int32Converter);
        if (type == typeof(uint)) return JsonMetadataServices.CreateValueInfo<uint>(options, JsonMetadataServices.UInt32Converter);
        if (type == typeof(long)) return JsonMetadataServices.CreateValueInfo<long>(options, JsonMetadataServices.Int64Converter);
        if (type == typeof(ulong)) return JsonMetadataServices.CreateValueInfo<ulong>(options, JsonMetadataServices.UInt64Converter);
        if (type == typeof(float)) return JsonMetadataServices.CreateValueInfo<float>(options, JsonMetadataServices.SingleConverter);
        if (type == typeof(double)) return JsonMetadataServices.CreateValueInfo<double>(options, JsonMetadataServices.DoubleConverter);
        if (type == typeof(decimal)) return JsonMetadataServices.CreateValueInfo<decimal>(options, JsonMetadataServices.DecimalConverter);
        if (type == typeof(char)) return JsonMetadataServices.CreateValueInfo<char>(options, JsonMetadataServices.CharConverter);
        if (type == typeof(Guid)) return JsonMetadataServices.CreateValueInfo<Guid>(options, JsonMetadataServices.GuidConverter);
        if (type == typeof(DateTime)) return JsonMetadataServices.CreateValueInfo<DateTime>(options, JsonMetadataServices.DateTimeConverter);
        if (type == typeof(DateTimeOffset)) return JsonMetadataServices.CreateValueInfo<DateTimeOffset>(options, JsonMetadataServices.DateTimeOffsetConverter);
        if (type == typeof(DateOnly)) return JsonMetadataServices.CreateValueInfo<DateOnly>(options, JsonMetadataServices.DateOnlyConverter);
        if (type == typeof(TimeOnly)) return JsonMetadataServices.CreateValueInfo<TimeOnly>(options, JsonMetadataServices.TimeOnlyConverter);
        if (type == typeof(TimeSpan)) return JsonMetadataServices.CreateValueInfo<TimeSpan>(options, JsonMetadataServices.TimeSpanConverter);

        // Nullable<T>. The underlying info has to be built first — GetNullableConverter takes it,
        // not the options — which is also why enums cannot be handled generically here.
        if (type == typeof(bool?)) return Nullable<bool>(options, JsonMetadataServices.BooleanConverter);
        if (type == typeof(byte?)) return Nullable<byte>(options, JsonMetadataServices.ByteConverter);
        if (type == typeof(sbyte?)) return Nullable<sbyte>(options, JsonMetadataServices.SByteConverter);
        if (type == typeof(short?)) return Nullable<short>(options, JsonMetadataServices.Int16Converter);
        if (type == typeof(ushort?)) return Nullable<ushort>(options, JsonMetadataServices.UInt16Converter);
        if (type == typeof(int?)) return Nullable<int>(options, JsonMetadataServices.Int32Converter);
        if (type == typeof(uint?)) return Nullable<uint>(options, JsonMetadataServices.UInt32Converter);
        if (type == typeof(long?)) return Nullable<long>(options, JsonMetadataServices.Int64Converter);
        if (type == typeof(ulong?)) return Nullable<ulong>(options, JsonMetadataServices.UInt64Converter);
        if (type == typeof(float?)) return Nullable<float>(options, JsonMetadataServices.SingleConverter);
        if (type == typeof(double?)) return Nullable<double>(options, JsonMetadataServices.DoubleConverter);
        if (type == typeof(decimal?)) return Nullable<decimal>(options, JsonMetadataServices.DecimalConverter);
        if (type == typeof(char?)) return Nullable<char>(options, JsonMetadataServices.CharConverter);
        if (type == typeof(Guid?)) return Nullable<Guid>(options, JsonMetadataServices.GuidConverter);
        if (type == typeof(DateTime?)) return Nullable<DateTime>(options, JsonMetadataServices.DateTimeConverter);
        if (type == typeof(DateTimeOffset?)) return Nullable<DateTimeOffset>(options, JsonMetadataServices.DateTimeOffsetConverter);
        if (type == typeof(DateOnly?)) return Nullable<DateOnly>(options, JsonMetadataServices.DateOnlyConverter);
        if (type == typeof(TimeOnly?)) return Nullable<TimeOnly>(options, JsonMetadataServices.TimeOnlyConverter);
        if (type == typeof(TimeSpan?)) return Nullable<TimeSpan>(options, JsonMetadataServices.TimeSpanConverter);

        return null;
    }

    private static JsonTypeInfo<T?> Nullable<T>(JsonSerializerOptions options, System.Text.Json.Serialization.JsonConverter<T> underlying)
        where T : struct
        => JsonMetadataServices.CreateValueInfo<T?>(
            options,
            JsonMetadataServices.GetNullableConverter(JsonMetadataServices.CreateValueInfo<T>(options, underlying)));
}
