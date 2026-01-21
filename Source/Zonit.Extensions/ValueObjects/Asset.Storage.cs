using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

public readonly partial struct Asset
{
    /// <summary>
    /// Current binary storage format version.
    /// </summary>
    private const byte CurrentStorageVersion = 4;

    /// <summary>
    /// Serializes Asset to byte array with embedded binary header (for database storage).
    /// Uses compact binary format for maximum performance.
    /// </summary>
    /// <remarks>
    /// <para><strong>Binary Format V4:</strong></para>
    /// <code>
    /// [1 byte]  Version (4)
    /// [16 bytes] Id (GUID)
    /// [1 byte]  Signature (enum as byte)
    /// [8 bytes] CreatedAt (UTC ticks as Int64)
    /// [2 bytes] MimeType length (UInt16)
    /// [N bytes] MimeType (UTF-8)
    /// [2 bytes] OriginalName length (UInt16)
    /// [N bytes] OriginalName (UTF-8)
    /// [44 bytes] Sha256 (Base64 fixed size)
    /// [24 bytes] Md5 (Base64 fixed size)
    /// [remaining] File data
    /// </code>
    /// <para>Total header overhead: ~98 bytes + name + mimeType (vs ~300 bytes JSON)</para>
    /// </remarks>
    public byte[] ToStorageBytes()
    {
        if (!HasValue)
            return [];

        var mimeTypeBytes = Encoding.UTF8.GetBytes(MediaType.Value);
        var nameBytes = Encoding.UTF8.GetBytes(OriginalName.Value);
        var sha256Bytes = Encoding.UTF8.GetBytes(Sha256);
        var md5Bytes = Encoding.UTF8.GetBytes(Md5);

        // Calculate header size
        var headerSize = 1 + 16 + 1 + 8 + 2 + mimeTypeBytes.Length + 2 + nameBytes.Length + 44 + 24;
        var result = new byte[headerSize + Data.Length];

        var offset = 0;

        // Version (1 byte)
        result[offset++] = CurrentStorageVersion;

        // Id (16 bytes)
        Id.TryWriteBytes(result.AsSpan(offset));
        offset += 16;

        // Signature (1 byte)
        result[offset++] = (byte)Signature;

        // CreatedAt (8 bytes - UTC ticks)
        BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(offset), CreatedAt.Ticks);
        offset += 8;

        // MimeType (2 bytes length + data)
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), (ushort)mimeTypeBytes.Length);
        offset += 2;
        mimeTypeBytes.CopyTo(result, offset);
        offset += mimeTypeBytes.Length;

        // OriginalName (2 bytes length + data)
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), (ushort)nameBytes.Length);
        offset += 2;
        nameBytes.CopyTo(result, offset);
        offset += nameBytes.Length;

        // Sha256 (44 bytes fixed - Base64 of 32 bytes = 44 chars)
        sha256Bytes.CopyTo(result, offset);
        if (sha256Bytes.Length < 44)
            Array.Clear(result, offset + sha256Bytes.Length, 44 - sha256Bytes.Length);
        offset += 44;

        // Md5 (24 bytes fixed - Base64 of 16 bytes = 24 chars)
        md5Bytes.CopyTo(result, offset);
        if (md5Bytes.Length < 24)
            Array.Clear(result, offset + md5Bytes.Length, 24 - md5Bytes.Length);
        offset += 24;

        // File data
        Data.CopyTo(result, offset);

        return result;
    }

    /// <summary>
    /// Deserializes Asset from storage bytes with embedded header.
    /// Supports both binary V4 format and legacy JSON formats (V1-V3).
    /// </summary>
    public static Asset FromStorageBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 4)
            return Empty;

        // Check if V4 binary format (version byte = 4)
        if (bytes[0] == 4)
            return FromStorageBytesV4(bytes);

        // Legacy JSON format (V1-V3) - first 4 bytes are header length
        return FromStorageBytesLegacy(bytes);
    }

    /// <summary>
    /// Fast binary deserialization (V4 format).
    /// </summary>
    private static Asset FromStorageBytesV4(byte[] bytes)
    {
        var offset = 1; // Skip version byte

        // Minimum header size check
        if (bytes.Length < 98) // 1 + 16 + 1 + 8 + 2 + 2 + 44 + 24
            return Empty;

        // Id (16 bytes)
        var id = new Guid(bytes.AsSpan(offset, 16));
        offset += 16;

        // Signature (1 byte)
        var signature = (SignatureType)bytes[offset++];

        // CreatedAt (8 bytes)
        var createdAtTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
        var createdAt = new DateTime(createdAtTicks, DateTimeKind.Utc);
        offset += 8;

        // MimeType
        var mimeTypeLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        offset += 2;
        if (bytes.Length < offset + mimeTypeLength)
            return Empty;
        var mimeType = new MimeType(Encoding.UTF8.GetString(bytes, offset, mimeTypeLength));
        offset += mimeTypeLength;

        // OriginalName
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        offset += 2;
        if (bytes.Length < offset + nameLength)
            return Empty;
        var originalName = new FileName(Encoding.UTF8.GetString(bytes, offset, nameLength));
        offset += nameLength;

        // Sha256 (44 bytes)
        var sha256 = Encoding.UTF8.GetString(bytes, offset, 44).TrimEnd('\0');
        offset += 44;

        // Md5 (24 bytes)
        var md5 = Encoding.UTF8.GetString(bytes, offset, 24).TrimEnd('\0');
        offset += 24;

        // File data
        var dataLength = bytes.Length - offset;
        if (dataLength <= 0)
            return Empty;

        var data = new byte[dataLength];
        Array.Copy(bytes, offset, data, 0, dataLength);

        return new Asset(
            data: data,
            originalName: originalName,
            mimeType: mimeType,
            signature: signature,
            id: id,
            createdAt: createdAt,
            sha256: sha256,
            md5: md5
        );
    }

    /// <summary>
    /// Legacy JSON deserialization (V1-V3 formats) for backward compatibility.
    /// </summary>
    private static Asset FromStorageBytesLegacy(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 4)
            return Empty;

        var headerLength = BitConverter.ToInt32(bytes, 0);
        if (headerLength <= 0 || bytes.Length < 4 + headerLength)
            return Empty;

        var headerJson = System.Text.Encoding.UTF8.GetString(bytes, 4, headerLength);

        var dataStart = 4 + headerLength;
        var dataLength = bytes.Length - dataStart;
        var data = new byte[dataLength];
        Array.Copy(bytes, dataStart, data, 0, dataLength);

        // Try V3 format first (with Signature)
        var headerV3 = JsonSerializer.Deserialize(headerJson, AssetStorageJsonContext.Default.AssetStorageHeaderV3);
        if (headerV3 is not null && headerV3.Version >= 3)
        {
            var signature = Enum.TryParse<SignatureType>(headerV3.Signature, out var sig) ? sig : SignatureType.Unknown;
            return new Asset(
                data: data,
                originalName: new FileName(headerV3.OriginalName),
                mimeType: new MimeType(headerV3.MimeType),
                signature: signature,
                id: headerV3.Id,
                createdAt: headerV3.CreatedAt,
                sha256: headerV3.Sha256 ?? string.Empty,
                md5: headerV3.Md5 ?? string.Empty
            );
        }

        // Try V2 format (without Signature)
        var headerV2 = JsonSerializer.Deserialize(headerJson, AssetStorageJsonContext.Default.AssetStorageHeaderV2);
        if (headerV2 is not null && headerV2.Version >= 2)
        {
            return new Asset(
                data: data,
                originalName: new FileName(headerV2.OriginalName),
                legacyMimeType: new MimeType(headerV2.MimeType),
                id: headerV2.Id,
                createdAt: headerV2.CreatedAt,
                sha256: headerV2.Sha256,
                md5: headerV2.Md5
            );
        }

        // Try V1 legacy format
        var legacyHeader = JsonSerializer.Deserialize(headerJson, AssetStorageJsonContext.Default.AssetStorageHeaderV1);
        if (legacyHeader is not null)
        {
            return new Asset(
                data: data,
                originalName: new FileName(legacyHeader.Name),
                legacyMimeType: new MimeType(legacyHeader.MimeType),
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow,
                sha256: null,
                md5: null
            );
        }

        return Empty;
    }

    #region Validation

    /// <summary>
    /// Validates asset against allowed MIME types.
    /// </summary>
    public bool IsAllowedType(params MimeType[] allowedTypes)
    {
        var mediaType = MediaType;
        return allowedTypes.Any(t => t == mediaType);
    }

    /// <summary>
    /// Validates asset against allowed extensions.
    /// </summary>
    public bool IsAllowedExtension(params string[] extensions)
    {
        var ext = OriginalName.ExtensionWithoutDot;
        return extensions.Any(e => ext.Equals(e.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates asset size against maximum.
    /// </summary>
    public bool IsWithinSizeLimit(FileSize maxSize) => Size <= maxSize;

    /// <summary>
    /// Validates asset size against maximum bytes.
    /// </summary>
    public bool IsWithinSizeLimit(long maxBytes) => Size.Bytes <= maxBytes;

    /// <summary>
    /// Validates asset against multiple constraints.
    /// </summary>
    public AssetValidationResult Validate(AssetValidationOptions options)
    {
        var errors = new List<string>();

        if (options.MaxSize.HasValue && Size > options.MaxSize.Value)
            errors.Add($"File size ({Size}) exceeds maximum ({options.MaxSize.Value}).");

        if (options.AllowedMimeTypes?.Length > 0 && !IsAllowedType(options.AllowedMimeTypes))
            errors.Add($"File type '{MediaType.Value}' is not allowed.");

        if (options.AllowedExtensions?.Length > 0 && !IsAllowedExtension(options.AllowedExtensions))
            errors.Add($"File extension '{OriginalName.Extension}' is not allowed.");

        if (options.ValidateSignature && !IsSignatureValid())
        {
            var warning = GetSignatureMismatchWarning();
            if (warning is not null)
                errors.Add(warning);
        }

        return new AssetValidationResult(errors.Count == 0, errors);
    }

    #endregion
}

/// <summary>
/// Header structure for Asset storage serialization (version 3).
/// Contains all metadata needed to fully reconstruct an Asset.
/// </summary>
internal sealed record AssetStorageHeaderV3(
    int Version,
    Guid Id,
    string OriginalName,
    string MimeType,
    string Signature,
    DateTime CreatedAt,
    string? Sha256,
    string? Md5
);

/// <summary>
/// Header structure for Asset storage serialization (version 2).
/// For backward compatibility.
/// </summary>
internal sealed record AssetStorageHeaderV2(
    int Version,
    Guid Id,
    string OriginalName,
    string MimeType,
    DateTime CreatedAt,
    string? Sha256,
    string? Md5
);

/// <summary>
/// Legacy header structure for backward compatibility (version 1).
/// </summary>
internal sealed record AssetStorageHeaderV1(
    string Name,
    string MimeType
);

/// <summary>
/// JSON source generator context for Asset storage serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AssetStorageHeaderV3))]
[JsonSerializable(typeof(AssetStorageHeaderV2))]
[JsonSerializable(typeof(AssetStorageHeaderV1))]
internal partial class AssetStorageJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Options for asset validation.
/// </summary>
public sealed record AssetValidationOptions
{
    /// <summary>
    /// Maximum file size.
    /// </summary>
    public FileSize? MaxSize { get; init; }

    /// <summary>
    /// Allowed MIME types.
    /// </summary>
    public Asset.MimeType[]? AllowedMimeTypes { get; init; }

    /// <summary>
    /// Allowed file extensions (with or without dot).
    /// </summary>
    public string[]? AllowedExtensions { get; init; }

    /// <summary>
    /// Whether to validate file signature (magic bytes) against MIME type.
    /// </summary>
    public bool ValidateSignature { get; init; }

    /// <summary>
    /// Creates options for image uploads.
    /// </summary>
    public static AssetValidationOptions Images(FileSize? maxSize = null, bool validateSignature = true) => new()
    {
        MaxSize = maxSize ?? FileSize.TenMegabytes,
        AllowedExtensions = ["jpg", "jpeg", "png", "gif", "webp", "svg"],
        ValidateSignature = validateSignature
    };

    /// <summary>
    /// Creates options for document uploads.
    /// </summary>
    public static AssetValidationOptions Documents(FileSize? maxSize = null, bool validateSignature = true) => new()
    {
        MaxSize = maxSize ?? FileSize.FiftyMegabytes,
        AllowedExtensions = ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt"],
        ValidateSignature = validateSignature
    };

    /// <summary>
    /// Creates options for audio uploads.
    /// </summary>
    public static AssetValidationOptions Audio(FileSize? maxSize = null, bool validateSignature = true) => new()
    {
        MaxSize = maxSize ?? FileSize.HundredMegabytes,
        AllowedExtensions = ["mp3", "wav", "ogg", "flac"],
        ValidateSignature = validateSignature
    };

    /// <summary>
    /// Creates options for video uploads.
    /// </summary>
    public static AssetValidationOptions Video(FileSize? maxSize = null, bool validateSignature = true) => new()
    {
        MaxSize = maxSize ?? FileSize.FiveHundredMegabytes,
        AllowedExtensions = ["mp4", "webm", "mov", "avi"],
        ValidateSignature = validateSignature
    };
}

/// <summary>
/// Result of asset validation.
/// </summary>
public sealed record AssetValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Valid result with no errors.
    /// </summary>
    public static readonly AssetValidationResult Valid = new(true, []);
}
