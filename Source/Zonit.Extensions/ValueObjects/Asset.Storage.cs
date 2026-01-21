using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

public readonly partial struct Asset
{
    /// <summary>
    /// Serializes Asset to byte array with embedded header (for database storage).
    /// Contains all metadata (Id, OriginalName, MimeType, CreatedAt, Sha256, Md5) + file data.
    /// </summary>
    /// <remarks>
    /// Format: [4 bytes: header length][UTF-8 JSON header][file data]
    /// 
    /// The header contains all metadata needed to fully reconstruct the Asset:
    /// - Id (GUID) - unique identifier
    /// - OriginalName - original filename
    /// - MimeType - content type
    /// - CreatedAt - creation timestamp
    /// - Sha256 - SHA256 hash (computed if not already)
    /// - Md5 - MD5 hash (computed if not already)
    /// </remarks>
    public byte[] ToStorageBytes()
    {
        if (!HasValue)
            return [];

        var header = new AssetStorageHeader(
            Version: 2,
            Id: Id,
            OriginalName: OriginalName.Value,
            MimeType: ContentType.Value,
            CreatedAt: CreatedAt,
            Sha256: Sha256,
            Md5: Md5
        );

        var headerJson = JsonSerializer.Serialize(header, AssetStorageJsonContext.Default.AssetStorageHeader);
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);
        var headerLength = BitConverter.GetBytes(headerBytes.Length);

        var result = new byte[4 + headerBytes.Length + Data.Length];
        headerLength.CopyTo(result, 0);
        headerBytes.CopyTo(result, 4);
        Data.CopyTo(result, 4 + headerBytes.Length);

        return result;
    }

    /// <summary>
    /// Deserializes Asset from storage bytes with embedded header.
    /// Restores all metadata including Id, timestamps, and precomputed hashes.
    /// </summary>
    public static Asset FromStorageBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 4)
            return Empty;

        var headerLength = BitConverter.ToInt32(bytes, 0);
        if (headerLength <= 0 || bytes.Length < 4 + headerLength)
            return Empty;

        var headerJson = System.Text.Encoding.UTF8.GetString(bytes, 4, headerLength);

        // Try to deserialize with new format (v2) first
        var header = JsonSerializer.Deserialize(headerJson, AssetStorageJsonContext.Default.AssetStorageHeader);

        if (header is null)
        {
            // Try legacy format (v1)
            var legacyHeader = JsonSerializer.Deserialize(headerJson, AssetStorageJsonContext.Default.AssetStorageHeaderLegacy);
            if (legacyHeader is null)
                return Empty;

            // Convert legacy to v2 format
            header = new AssetStorageHeader(
                Version: 1,
                Id: Guid.NewGuid(),
                OriginalName: legacyHeader.Name,
                MimeType: legacyHeader.MimeType,
                CreatedAt: DateTime.UtcNow,
                Sha256: null,
                Md5: null
            );
        }

        var dataStart = 4 + headerLength;
        var dataLength = bytes.Length - dataStart;
        var data = new byte[dataLength];
        Array.Copy(bytes, dataStart, data, 0, dataLength);

        // Use internal constructor that accepts precomputed values
        return new Asset(
            data: data,
            originalName: new FileName(header.OriginalName),
            contentType: new MimeType(header.MimeType),
            id: header.Id,
            createdAt: header.CreatedAt,
            sha256: header.Sha256,
            md5: header.Md5
        );
    }

    #region Validation

    /// <summary>
    /// Validates asset against allowed MIME types.
    /// </summary>
    public bool IsAllowedType(params MimeType[] allowedTypes)
    {
        var mime = ContentType;
        return allowedTypes.Any(t => t == mime);
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
            errors.Add($"File type '{ContentType.Value}' is not allowed.");

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
/// Header structure for Asset storage serialization (version 2).
/// Contains all metadata needed to fully reconstruct an Asset.
/// </summary>
internal sealed record AssetStorageHeader(
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
internal sealed record AssetStorageHeaderLegacy(
    string Name,
    string MimeType
);

/// <summary>
/// JSON source generator context for Asset storage serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AssetStorageHeader))]
[JsonSerializable(typeof(AssetStorageHeaderLegacy))]
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
