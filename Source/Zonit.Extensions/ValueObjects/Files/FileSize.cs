using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Represents a file size with automatic unit conversion (like TimeSpan for time).
/// Provides properties for bytes, KB, MB, GB, TB and formatted display.
/// </summary>
/// <remarks>
/// <para>
/// Design inspired by <see cref="TimeSpan"/> - allows easy conversion between units.
/// </para>
/// <example>
/// <code>
/// FileSize size = FileSize.FromMegabytes(1.5);
/// Console.WriteLine(size.Bytes);        // 1572864
/// Console.WriteLine(size.Kilobytes);    // 1536
/// Console.WriteLine(size.Megabytes);    // 1.5
/// Console.WriteLine(size);              // "1.5 MB"
/// 
/// // Implicit from long (bytes)
/// FileSize size2 = 1024L;               // 1 KB
/// 
/// // Arithmetic
/// var total = size + size2;
/// var doubled = size * 2;
/// </code>
/// </example>
/// </remarks>
[TypeConverter(typeof(FileSizeTypeConverter))]
[JsonConverter(typeof(FileSizeJsonConverter))]
public readonly struct FileSize : IEquatable<FileSize>, IComparable<FileSize>, IParsable<FileSize>, IFormattable
{
    #region Constants

    /// <summary>Bytes in a kilobyte (1024).</summary>
    public const long BytesPerKilobyte = 1024;

    /// <summary>Bytes in a megabyte (1,048,576).</summary>
    public const long BytesPerMegabyte = BytesPerKilobyte * 1024;

    /// <summary>Bytes in a gigabyte (1,073,741,824).</summary>
    public const long BytesPerGigabyte = BytesPerMegabyte * 1024;

    /// <summary>Bytes in a terabyte (1,099,511,627,776).</summary>
    public const long BytesPerTerabyte = BytesPerGigabyte * 1024;

    /// <summary>Zero file size.</summary>
    public static readonly FileSize Zero = new(0);

    /// <summary>Maximum file size (long.MaxValue bytes).</summary>
    public static readonly FileSize MaxValue = new(long.MaxValue);

    #endregion

    #region Common Sizes

    /// <summary>1 KB</summary>
    public static readonly FileSize OneKilobyte = FromKilobytes(1);

    /// <summary>1 MB</summary>
    public static readonly FileSize OneMegabyte = FromMegabytes(1);

    /// <summary>1 GB</summary>
    public static readonly FileSize OneGigabyte = FromGigabytes(1);

    /// <summary>1 TB</summary>
    public static readonly FileSize OneTerabyte = FromTerabytes(1);

    /// <summary>Common limit: 5 MB (email attachments).</summary>
    public static readonly FileSize FiveMegabytes = FromMegabytes(5);

    /// <summary>Common limit: 10 MB (image uploads).</summary>
    public static readonly FileSize TenMegabytes = FromMegabytes(10);

    /// <summary>Common limit: 25 MB (large attachments).</summary>
    public static readonly FileSize TwentyFiveMegabytes = FromMegabytes(25);

    /// <summary>Common limit: 50 MB (documents).</summary>
    public static readonly FileSize FiftyMegabytes = FromMegabytes(50);

    /// <summary>Common limit: 100 MB (large files).</summary>
    public static readonly FileSize HundredMegabytes = FromMegabytes(100);

    /// <summary>Common limit: 500 MB (videos).</summary>
    public static readonly FileSize FiveHundredMegabytes = FromMegabytes(500);

    /// <summary>Common limit: 1 GB.</summary>
    public static readonly FileSize OneGigabyteLimited = FromGigabytes(1);

    /// <summary>Common limit: 2 GB (32-bit limit).</summary>
    public static readonly FileSize TwoGigabytes = FromGigabytes(2);

    /// <summary>Common limit: 4 GB (FAT32 limit).</summary>
    public static readonly FileSize FourGigabytes = FromGigabytes(4);

    #endregion

    private readonly long _bytes;

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long Bytes => _bytes;

    /// <summary>
    /// Total size in kilobytes (fractional).
    /// </summary>
    public double Kilobytes => _bytes / (double)BytesPerKilobyte;

    /// <summary>
    /// Total size in megabytes (fractional).
    /// </summary>
    public double Megabytes => _bytes / (double)BytesPerMegabyte;

    /// <summary>
    /// Total size in gigabytes (fractional).
    /// </summary>
    public double Gigabytes => _bytes / (double)BytesPerGigabyte;

    /// <summary>
    /// Total size in terabytes (fractional).
    /// </summary>
    public double Terabytes => _bytes / (double)BytesPerTerabyte;

    /// <summary>
    /// Indicates whether the size is zero.
    /// </summary>
    public bool IsZero => _bytes == 0;

    /// <summary>
    /// Indicates whether the size has a positive value.
    /// </summary>
    public bool HasValue => _bytes > 0;

    #region Constructors

    /// <summary>
    /// Creates a new FileSize with the specified number of bytes.
    /// </summary>
    /// <param name="bytes">Size in bytes. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bytes is negative.</exception>
    public FileSize(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "File size cannot be negative.");

        _bytes = bytes;
    }

    #endregion

    #region Factory Methods

    /// <summary>Creates FileSize from bytes.</summary>
    public static FileSize FromBytes(long bytes) => new(bytes);

    /// <summary>Creates FileSize from kilobytes.</summary>
    public static FileSize FromKilobytes(double kilobytes)
    {
        if (kilobytes < 0)
            throw new ArgumentOutOfRangeException(nameof(kilobytes), kilobytes, "Size cannot be negative.");

        return new FileSize((long)(kilobytes * BytesPerKilobyte));
    }

    /// <summary>Creates FileSize from megabytes.</summary>
    public static FileSize FromMegabytes(double megabytes)
    {
        if (megabytes < 0)
            throw new ArgumentOutOfRangeException(nameof(megabytes), megabytes, "Size cannot be negative.");

        return new FileSize((long)(megabytes * BytesPerMegabyte));
    }

    /// <summary>Creates FileSize from gigabytes.</summary>
    public static FileSize FromGigabytes(double gigabytes)
    {
        if (gigabytes < 0)
            throw new ArgumentOutOfRangeException(nameof(gigabytes), gigabytes, "Size cannot be negative.");

        return new FileSize((long)(gigabytes * BytesPerGigabyte));
    }

    /// <summary>Creates FileSize from terabytes.</summary>
    public static FileSize FromTerabytes(double terabytes)
    {
        if (terabytes < 0)
            throw new ArgumentOutOfRangeException(nameof(terabytes), terabytes, "Size cannot be negative.");

        return new FileSize((long)(terabytes * BytesPerTerabyte));
    }

    #endregion

    #region Arithmetic Operators

    public static FileSize operator +(FileSize left, FileSize right) =>
        new(left._bytes + right._bytes);

    public static FileSize operator -(FileSize left, FileSize right)
    {
        var result = left._bytes - right._bytes;
        return new(result < 0 ? 0 : result);
    }

    public static FileSize operator *(FileSize size, long multiplier) =>
        new(size._bytes * multiplier);

    public static FileSize operator *(long multiplier, FileSize size) =>
        new(size._bytes * multiplier);

    public static FileSize operator *(FileSize size, double multiplier) =>
        new((long)(size._bytes * multiplier));

    public static FileSize operator /(FileSize size, long divisor) =>
        new(size._bytes / divisor);

    public static double operator /(FileSize left, FileSize right) =>
        (double)left._bytes / right._bytes;

    #endregion

    #region Comparison Operators

    public static bool operator ==(FileSize left, FileSize right) => left._bytes == right._bytes;
    public static bool operator !=(FileSize left, FileSize right) => left._bytes != right._bytes;
    public static bool operator <(FileSize left, FileSize right) => left._bytes < right._bytes;
    public static bool operator <=(FileSize left, FileSize right) => left._bytes <= right._bytes;
    public static bool operator >(FileSize left, FileSize right) => left._bytes > right._bytes;
    public static bool operator >=(FileSize left, FileSize right) => left._bytes >= right._bytes;

    #endregion

    #region Implicit/Explicit Conversions

    /// <summary>
    /// Implicit conversion from long (bytes) to FileSize.
    /// </summary>
    public static implicit operator FileSize(long bytes) => new(bytes);

    /// <summary>
    /// Implicit conversion from int (bytes) to FileSize.
    /// </summary>
    public static implicit operator FileSize(int bytes) => new(bytes);

    /// <summary>
    /// Explicit conversion from FileSize to long (bytes).
    /// </summary>
    public static explicit operator long(FileSize size) => size._bytes;

    #endregion

    #region Equality & Comparison

    public bool Equals(FileSize other) => _bytes == other._bytes;

    public override bool Equals(object? obj) => obj is FileSize other && Equals(other);

    public override int GetHashCode() => _bytes.GetHashCode();

    public int CompareTo(FileSize other) => _bytes.CompareTo(other._bytes);

    #endregion

    #region Formatting

    /// <summary>
    /// Returns human-readable size with automatic unit selection.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Returns formatted size string.
    /// </summary>
    /// <param name="format">
    /// Format specifier:
    /// <list type="bullet">
    ///   <item><c>B</c> - Bytes only (e.g., "1234567 B")</item>
    ///   <item><c>KB</c> - Kilobytes (e.g., "1205.6 KB")</item>
    ///   <item><c>MB</c> - Megabytes (e.g., "1.18 MB")</item>
    ///   <item><c>GB</c> - Gigabytes (e.g., "0.001 GB")</item>
    ///   <item><c>TB</c> - Terabytes</item>
    ///   <item><c>A</c> or <c>null</c> - Auto (best unit)</item>
    /// </list>
    /// </param>
    /// <param name="formatProvider">Format provider (culture).</param>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        formatProvider ??= CultureInfo.CurrentCulture;

        return format?.ToUpperInvariant() switch
        {
            "B" => $"{_bytes:N0} B",
            "KB" => string.Create(formatProvider, $"{Kilobytes:N2} KB"),
            "MB" => string.Create(formatProvider, $"{Megabytes:N2} MB"),
            "GB" => string.Create(formatProvider, $"{Gigabytes:N2} GB"),
            "TB" => string.Create(formatProvider, $"{Terabytes:N2} TB"),
            _ => FormatAuto(formatProvider)
        };
    }

    private string FormatAuto(IFormatProvider formatProvider) => _bytes switch
    {
        < BytesPerKilobyte => $"{_bytes} B",
        < BytesPerMegabyte => string.Create(formatProvider, $"{Kilobytes:N1} KB"),
        < BytesPerGigabyte => string.Create(formatProvider, $"{Megabytes:N1} MB"),
        < BytesPerTerabyte => string.Create(formatProvider, $"{Gigabytes:N2} GB"),
        _ => string.Create(formatProvider, $"{Terabytes:N2} TB")
    };

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a file size string (e.g., "1.5 MB", "1024", "500 KB").
    /// </summary>
    public static FileSize Parse(string s, IFormatProvider? provider = null)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as FileSize.");
    }

    /// <summary>
    /// Tries to parse a file size string.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FileSize result)
    {
        result = Zero;

        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Trim().ToUpperInvariant();

        // Try direct number (bytes)
        if (long.TryParse(s, NumberStyles.Integer, provider, out var bytes))
        {
            if (bytes >= 0)
            {
                result = new FileSize(bytes);
                return true;
            }
            return false;
        }

        // Try with unit suffix
        var units = new[] { ("TB", BytesPerTerabyte), ("GB", BytesPerGigabyte), ("MB", BytesPerMegabyte), ("KB", BytesPerKilobyte), ("B", 1L) };

        foreach (var (unit, multiplier) in units)
        {
            if (s.EndsWith(unit, StringComparison.Ordinal))
            {
                var numberPart = s[..^unit.Length].Trim();
                if (double.TryParse(numberPart, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out var value) && value >= 0)
                {
                    result = new FileSize((long)(value * multiplier));
                    return true;
                }
            }
        }

        return false;
    }

    #endregion
}

#region Converters

/// <summary>
/// TypeConverter for FileSize (ASP.NET model binding, property grid).
/// </summary>
public sealed class FileSizeTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || sourceType == typeof(long) || sourceType == typeof(int) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || destinationType == typeof(long) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value switch
        {
            string s => FileSize.Parse(s, culture),
            long l => new FileSize(l),
            int i => new FileSize(i),
            _ => base.ConvertFrom(context, culture, value)
        };
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is FileSize size)
        {
            if (destinationType == typeof(string))
                return size.ToString(null, culture);
            if (destinationType == typeof(long))
                return size.Bytes;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>
/// JSON converter for FileSize (serializes as bytes number).
/// </summary>
public sealed class FileSizeJsonConverter : JsonConverter<FileSize>
{
    public override FileSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => new FileSize(reader.GetInt64()),
            JsonTokenType.String => FileSize.Parse(reader.GetString()!, null),
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to FileSize.")
        };
    }

    public override void Write(Utf8JsonWriter writer, FileSize value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Bytes);
    }
}

#endregion
