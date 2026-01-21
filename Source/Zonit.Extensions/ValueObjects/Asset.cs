using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a file asset as an immutable value object with embedded metadata.
/// Contains original file data plus metadata (name, type, hash, timestamps) for complete file representation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Features:</strong>
/// <list type="bullet">
///   <item>Unique ID (GUID) - always available for safe storage</item>
///   <item>Original name preserved + unique name for filesystem</item>
///   <item>SHA256 and MD5 hashes for integrity and deduplication</item>
///   <item>Creation timestamp embedded</item>
///   <item>FileSize VO for easy unit conversion (KB, MB, GB)</item>
///   <item>Implicit conversions from/to Stream, byte[], Base64</item>
///   <item>EF Core compatible (stores as byte[] with header)</item>
/// </list>
/// </para>
/// 
/// <para>
/// <strong>Design:</strong>
/// Asset is a self-contained "capsule" with all metadata. Once created, all validation
/// is complete - no need to recalculate hash, detect MIME type, etc.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Multiple ways to create
/// Asset asset = fileBytes;                              // From byte[]
/// Asset asset = memoryStream;                           // From Stream
/// Asset asset = new Asset(bytes);                       // Auto GUID name
/// Asset asset = new Asset(bytes, "document.pdf");       // With name
/// 
/// // Properties
/// Console.WriteLine(asset.Id);             // "7a3b9c4d-1234-5678-90ab-cdef12345678"
/// Console.WriteLine(asset.OriginalName);   // "document.pdf"
/// Console.WriteLine(asset.UniqueName);     // "7a3b9c4d-1234-5678-90ab-cdef12345678.pdf"
/// Console.WriteLine(asset.Size);           // "1.5 MB" (FileSize VO)
/// Console.WriteLine(asset.Size.Megabytes); // 1.5
/// Console.WriteLine(asset.CreatedAt);      // 2026-01-21 12:34:56
/// 
/// // Implicit conversion to Stream
/// Stream stream = asset;
/// await stream.CopyToAsync(destination);
/// </code>
/// </example>
[TypeConverter(typeof(AssetTypeConverter))]
[JsonConverter(typeof(AssetJsonConverter))]
public readonly partial struct Asset : IEquatable<Asset>
{
    /// <summary>
    /// Maximum file size allowed (100 MB by default).
    /// Use <see cref="AssetValidationOptions"/> for custom limits.
    /// </summary>
    public static readonly FileSize MaxSize = FileSize.HundredMegabytes;

    /// <summary>
    /// Empty asset instance.
    /// </summary>
    public static readonly Asset Empty = default;

    private readonly byte[]? _data;
    private readonly string? _sha256;
    private readonly string? _md5;

    #region Core Properties

    /// <summary>
    /// Unique identifier for this asset. Always generated, never changes.
    /// Use for safe filesystem storage, database keys, deduplication.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Original file name (or GUID-based if not provided).
    /// </summary>
    public FileName OriginalName { get; }

    /// <summary>
    /// Unique filename: {Id}{Extension}. Safe for filesystem storage.
    /// </summary>
    public string UniqueName => HasValue ? $"{Id}{Extension}" : string.Empty;

    /// <summary>
    /// MIME type of the file.
    /// </summary>
    public MimeType ContentType { get; }

    /// <summary>
    /// File extension with dot (e.g., ".pdf"). Derived from ContentType.
    /// </summary>
    public string Extension => ContentType.Extension;

    /// <summary>
    /// File size as FileSize VO (with KB/MB/GB conversion).
    /// </summary>
    public FileSize Size { get; }

    /// <summary>
    /// UTC timestamp when this asset was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// SHA256 hash of the file data (Base64 encoded).
    /// Lazy computed on first access. Use for integrity verification.
    /// </summary>
    public string Sha256 => _sha256 ?? (HasValue ? ComputeSha256() : string.Empty);

    /// <summary>
    /// MD5 hash of the file data (Base64 encoded).
    /// Lazy computed on first access. Use for legacy systems, ETags.
    /// </summary>
    public string Md5 => _md5 ?? (HasValue ? ComputeMd5() : string.Empty);

    /// <summary>
    /// SHA256 hash alias for backward compatibility.
    /// </summary>
    public string Hash => Sha256;

    /// <summary>
    /// File content as bytes. Never null - returns empty array for default.
    /// </summary>
    public byte[] Data => _data ?? [];

    /// <summary>
    /// Indicates whether this asset has data.
    /// </summary>
    public bool HasValue => _data is { Length: > 0 };

    #endregion

    #region File Type Properties

    /// <summary>Checks if this is an image file.</summary>
    public bool IsImage => ContentType.IsImage;

    /// <summary>Checks if this is a video file.</summary>
    public bool IsVideo => ContentType.IsVideo;

    /// <summary>Checks if this is an audio file.</summary>
    public bool IsAudio => ContentType.IsAudio;

    /// <summary>Checks if this is a document (PDF, DOC, etc.).</summary>
    public bool IsDocument => ContentType.IsDocument;

    /// <summary>Checks if this is a text file.</summary>
    public bool IsText => ContentType.IsText;

    /// <summary>Gets the file category based on MIME type.</summary>
    public AssetCategory Category => ContentType.Type switch
    {
        "image" => AssetCategory.Image,
        "video" => AssetCategory.Video,
        "audio" => AssetCategory.Audio,
        "text" => AssetCategory.Text,
        _ when ContentType.IsDocument => AssetCategory.Document,
        _ when ContentType.IsArchive => AssetCategory.Archive,
        _ => AssetCategory.Other
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Creates Asset with just data (auto-generates GUID name).
    /// </summary>
    /// <param name="data">File content bytes.</param>
    /// <param name="mimeType">MIME type (defaults to application/octet-stream).</param>
    public Asset(byte[] data, MimeType? mimeType = null)
        : this(data, (string?)null, mimeType, null, null)
    {
    }

    /// <summary>
    /// Creates Asset with data and optional name.
    /// </summary>
    /// <param name="data">File content bytes.</param>
    /// <param name="name">Original filename (null = GUID generated).</param>
    /// <param name="mimeType">MIME type (auto-detected from name if not provided).</param>
    public Asset(byte[] data, string? name, MimeType? mimeType = null)
        : this(data, name, mimeType, null, null)
    {
    }

    /// <summary>
    /// Creates Asset with data and FileName.
    /// </summary>
    /// <param name="data">File content bytes.</param>
    /// <param name="name">Original filename.</param>
    /// <param name="mimeType">MIME type (auto-detected from name if not provided).</param>
    public Asset(byte[] data, FileName name, MimeType? mimeType = null)
        : this(data, name.HasValue ? name.Value : null, mimeType, null, null)
    {
    }

    /// <summary>
    /// Creates Asset with explicit MIME type string.
    /// </summary>
    public Asset(byte[] data, string? name, string mimeType)
        : this(data, name, new MimeType(mimeType), null, null)
    {
    }

    /// <summary>
    /// Creates Asset from Base64 string.
    /// </summary>
    /// <param name="base64">Base64 encoded file content.</param>
    /// <param name="name">Original filename (null = GUID generated).</param>
    /// <param name="mimeType">MIME type (auto-detected from name if not provided).</param>
    public Asset(string base64, string? name = null, MimeType? mimeType = null)
        : this(Convert.FromBase64String(base64 ?? throw new ArgumentNullException(nameof(base64))), name, mimeType, null, null)
    {
    }

    /// <summary>
    /// Full internal constructor with all parameters.
    /// </summary>
    private Asset(byte[] data, string? name, MimeType? mimeType, Guid? id, DateTime? createdAt)
    {
        ArgumentNullException.ThrowIfNull(data, nameof(data));

        var size = new FileSize(data.Length);
        if (size > MaxSize)
            throw new ArgumentException($"File size ({size}) exceeds maximum allowed ({MaxSize}).", nameof(data));

        _data = data;
        _sha256 = null;
        _md5 = null;

        Id = id ?? Guid.NewGuid();
        CreatedAt = createdAt ?? DateTime.UtcNow;
        Size = size;

        // Determine MIME type first (needed for extension in GUID name)
        if (mimeType?.HasValue == true)
        {
            ContentType = mimeType.Value;
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            ContentType = MimeType.FromPath(name);
        }
        else
        {
            ContentType = MimeType.OctetStream;
        }

        // Set name (or generate GUID-based)
        if (!string.IsNullOrWhiteSpace(name))
        {
            OriginalName = FileName.TryCreate(name, out var fn) ? fn : GenerateFileName(ContentType);
        }
        else
        {
            OriginalName = GenerateFileName(ContentType);
        }
    }

    /// <summary>
    /// Internal constructor for deserialization (with precomputed values).
    /// </summary>
    internal Asset(byte[] data, FileName originalName, MimeType contentType, Guid id, DateTime createdAt, string? sha256, string? md5)
    {
        _data = data;
        _sha256 = sha256;
        _md5 = md5;
        Id = id;
        OriginalName = originalName;
        ContentType = contentType;
        CreatedAt = createdAt;
        Size = new FileSize(data.Length);
    }

    private static FileName GenerateFileName(MimeType mimeType)
    {
        var ext = mimeType.Extension;
        return new FileName($"{Guid.NewGuid():N}{ext}");
    }

    #endregion

    #region Stream Constructors

    /// <summary>
    /// Creates Asset from a Stream.
    /// </summary>
    /// <param name="stream">Stream containing file data.</param>
    /// <param name="name">Original filename (null = GUID generated).</param>
    /// <param name="mimeType">MIME type (auto-detected from name if not provided).</param>
    public Asset(Stream stream, string? name = null, MimeType? mimeType = null)
        : this(ReadStreamToBytes(stream), name, mimeType, null, null)
    {
    }

    /// <summary>
    /// Creates Asset from a MemoryStream.
    /// </summary>
    public Asset(MemoryStream stream, string? name = null, MimeType? mimeType = null)
        : this(stream.ToArray(), name, mimeType, null, null)
    {
    }

    private static byte[] ReadStreamToBytes(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));

        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    #endregion

    #region Implicit Conversions FROM

    /// <summary>
    /// Implicit conversion from byte[] to Asset.
    /// </summary>
    public static implicit operator Asset(byte[] data) => new(data);

    /// <summary>
    /// Implicit conversion from Stream to Asset.
    /// </summary>
    public static implicit operator Asset(Stream stream) => new(stream);

    /// <summary>
    /// Implicit conversion from MemoryStream to Asset.
    /// </summary>
    public static implicit operator Asset(MemoryStream stream) => new(stream);

    #endregion

    #region Implicit Conversions TO

    /// <summary>
    /// Implicit conversion to MemoryStream (read-only).
    /// </summary>
    public static implicit operator MemoryStream(Asset asset) => asset.ToStream();

    /// <summary>
    /// Implicit conversion to byte[].
    /// </summary>
    public static implicit operator byte[](Asset asset) => asset.Data;

    /// <summary>
    /// Implicit conversion to ReadOnlyMemory&lt;byte&gt;.
    /// </summary>
    public static implicit operator ReadOnlyMemory<byte>(Asset asset) => asset.Data.AsMemory();

    /// <summary>
    /// Implicit conversion to ReadOnlySpan&lt;byte&gt; is not possible (ref struct limitation).
    /// Use AsSpan() method instead.
    /// </summary>

    #endregion

    #region Data Conversion

    /// <summary>
    /// Gets data as Base64 string.
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(Data);

    /// <summary>
    /// Gets data URL (data:mime;base64,xxx).
    /// </summary>
    public string ToDataUrl() => $"data:{ContentType.Value};base64,{ToBase64()}";

    /// <summary>
    /// Opens a read-only MemoryStream over the data.
    /// </summary>
    public MemoryStream ToStream() => new(Data, writable: false);

    /// <summary>
    /// Gets text content (for text files).
    /// </summary>
    /// <param name="encoding">Text encoding (defaults to UTF-8).</param>
    public string ToText(System.Text.Encoding? encoding = null)
    {
        encoding ??= System.Text.Encoding.UTF8;
        return encoding.GetString(Data);
    }

    /// <summary>
    /// Gets data as ReadOnlyMemory (zero-copy).
    /// </summary>
    public ReadOnlyMemory<byte> AsMemory() => Data.AsMemory();

    /// <summary>
    /// Gets data as ReadOnlySpan (zero-copy).
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() => Data.AsSpan();

    #endregion

    #region Hash & Integrity

    /// <summary>
    /// Computes SHA256 hash of the file data.
    /// </summary>
    public string ComputeSha256() => Convert.ToBase64String(SHA256.HashData(Data));

    /// <summary>
    /// Computes MD5 hash of the file data.
    /// </summary>
    public string ComputeMd5() => Convert.ToBase64String(MD5.HashData(Data));

    /// <summary>
    /// Verifies the file data against a known SHA256 hash.
    /// </summary>
    public bool VerifyHash(string expectedHash) =>
        string.Equals(ComputeSha256(), expectedHash, StringComparison.Ordinal);

    /// <summary>
    /// Verifies the file data against a known MD5 hash.
    /// </summary>
    public bool VerifyMd5(string expectedMd5) =>
        string.Equals(ComputeMd5(), expectedMd5, StringComparison.Ordinal);

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates Asset from Base64 string.
    /// </summary>
    public static Asset FromBase64(string base64, string? name = null, MimeType? mimeType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64, nameof(base64));
        var data = Convert.FromBase64String(base64);
        return new Asset(data, name, mimeType);
    }

    /// <summary>
    /// Tries to create Asset, returning false if validation fails.
    /// </summary>
    public static bool TryCreate(byte[]? data, string? name, [NotNullWhen(true)] out Asset result)
    {
        result = Empty;

        if (data is null || data.Length == 0)
            return false;

        var size = new FileSize(data.Length);
        if (size > MaxSize)
            return false;

        try
        {
            result = new Asset(data, name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to create Asset from stream.
    /// </summary>
    public static bool TryCreate(Stream? stream, string? name, [NotNullWhen(true)] out Asset result)
    {
        result = Empty;

        if (stream is null)
            return false;

        try
        {
            result = new Asset(stream, name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Mutation (creates new instance)

    /// <summary>
    /// Creates a copy with different original name.
    /// </summary>
    public Asset WithName(FileName newName) => new(Data, newName.Value, ContentType, Id, CreatedAt);

    /// <summary>
    /// Creates a copy with different original name.
    /// </summary>
    public Asset WithName(string newName) => new(Data, newName, ContentType, Id, CreatedAt);

    /// <summary>
    /// Creates a copy with different MIME type.
    /// </summary>
    public Asset WithContentType(MimeType newContentType) => new(Data, OriginalName.Value, newContentType, Id, CreatedAt);

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(Asset other)
    {
        // Fast path: compare IDs first
        if (Id != other.Id) return false;
        if (Size != other.Size) return false;

        // Deep compare only if needed
        return Data.AsSpan().SequenceEqual(other.Data.AsSpan());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Asset other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Asset left, Asset right) => left.Equals(right);
    public static bool operator !=(Asset left, Asset right) => !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() =>
        HasValue ? $"{OriginalName.Value} ({ContentType.Value}, {Size})" : "(empty)";
}

/// <summary>
/// Categories of assets based on file type.
/// </summary>
public enum AssetCategory
{
    /// <summary>Unknown or unclassified file type.</summary>
    Other = 0,

    /// <summary>Image files (PNG, JPEG, GIF, etc.).</summary>
    Image,

    /// <summary>Video files (MP4, WebM, etc.).</summary>
    Video,

    /// <summary>Audio files (MP3, WAV, etc.).</summary>
    Audio,

    /// <summary>Document files (PDF, DOC, DOCX, etc.).</summary>
    Document,

    /// <summary>Text files (TXT, HTML, CSS, etc.).</summary>
    Text,

    /// <summary>Archive files (ZIP, RAR, 7z, etc.).</summary>
    Archive
}
