using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions;

public readonly partial struct Asset
{
    /// <summary>
    /// Represents a MIME type (e.g., "image/png", "application/pdf").
    /// Nested within Asset - not intended for standalone use.
    /// </summary>
    public readonly struct MimeType : IEquatable<MimeType>, IComparable<MimeType>, IParsable<MimeType>
    {
        /// <summary>
        /// Maximum allowed length for a MIME type string.
        /// </summary>
        public const int MaxLength = 255;

        /// <summary>
        /// Default MIME type for unknown files.
        /// </summary>
        public static readonly MimeType OctetStream = new("application/octet-stream");

        /// <summary>
        /// Empty MIME type instance.
        /// </summary>
        public static readonly MimeType Empty = default;

        #region Common MIME Types

        // Images
        public static readonly MimeType ImagePng = new("image/png");
        public static readonly MimeType ImageJpeg = new("image/jpeg");
        public static readonly MimeType ImageGif = new("image/gif");
        public static readonly MimeType ImageWebp = new("image/webp");
        public static readonly MimeType ImageSvg = new("image/svg+xml");
        public static readonly MimeType ImageBmp = new("image/bmp");
        public static readonly MimeType ImageIco = new("image/x-icon");
        public static readonly MimeType ImageTiff = new("image/tiff");

        // Documents
        public static readonly MimeType ApplicationPdf = new("application/pdf");
        public static readonly MimeType ApplicationDoc = new("application/msword");
        public static readonly MimeType ApplicationDocx = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        public static readonly MimeType ApplicationXls = new("application/vnd.ms-excel");
        public static readonly MimeType ApplicationXlsx = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        public static readonly MimeType ApplicationPpt = new("application/vnd.ms-powerpoint");
        public static readonly MimeType ApplicationPptx = new("application/vnd.openxmlformats-officedocument.presentationml.presentation");

        // Text
        public static readonly MimeType TextPlain = new("text/plain");
        public static readonly MimeType TextHtml = new("text/html");
        public static readonly MimeType TextCss = new("text/css");
        public static readonly MimeType TextCsv = new("text/csv");
        public static readonly MimeType TextXml = new("text/xml");

        // Application
        public static readonly MimeType ApplicationJson = new("application/json");
        public static readonly MimeType ApplicationXml = new("application/xml");
        public static readonly MimeType ApplicationZip = new("application/zip");
        public static readonly MimeType ApplicationGzip = new("application/gzip");
        public static readonly MimeType ApplicationRar = new("application/vnd.rar");
        public static readonly MimeType Application7z = new("application/x-7z-compressed");

        // Audio
        public static readonly MimeType AudioMpeg = new("audio/mpeg");
        public static readonly MimeType AudioWav = new("audio/wav");
        public static readonly MimeType AudioOgg = new("audio/ogg");
        public static readonly MimeType AudioWebm = new("audio/webm");
        public static readonly MimeType AudioFlac = new("audio/flac");

        // Video
        public static readonly MimeType VideoMp4 = new("video/mp4");
        public static readonly MimeType VideoWebm = new("video/webm");
        public static readonly MimeType VideoOgg = new("video/ogg");
        public static readonly MimeType VideoMov = new("video/quicktime");
        public static readonly MimeType VideoAvi = new("video/x-msvideo");

        #endregion

        private readonly string? _value;

        /// <summary>
        /// The MIME type value. Never null.
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// Indicates whether the MIME type has a meaningful value.
        /// </summary>
        public bool HasValue => !string.IsNullOrWhiteSpace(_value);

        /// <summary>
        /// Gets the primary type (e.g., "image" from "image/png").
        /// </summary>
        public string Type => HasValue && _value!.Contains('/')
            ? _value.Split('/')[0]
            : string.Empty;

        /// <summary>
        /// Gets the subtype (e.g., "png" from "image/png").
        /// </summary>
        public string Subtype => HasValue && _value!.Contains('/')
            ? _value.Split('/')[1].Split(';')[0].Trim()
            : string.Empty;

        /// <summary>Checks if this is an image MIME type.</summary>
        public bool IsImage => Type.Equals("image", StringComparison.OrdinalIgnoreCase);

        /// <summary>Checks if this is a video MIME type.</summary>
        public bool IsVideo => Type.Equals("video", StringComparison.OrdinalIgnoreCase);

        /// <summary>Checks if this is an audio MIME type.</summary>
        public bool IsAudio => Type.Equals("audio", StringComparison.OrdinalIgnoreCase);

        /// <summary>Checks if this is a text MIME type.</summary>
        public bool IsText => Type.Equals("text", StringComparison.OrdinalIgnoreCase);

        /// <summary>Checks if this is a document (PDF, DOC, DOCX, etc.).</summary>
        public bool IsDocument => Value switch
        {
            "application/pdf" => true,
            "application/msword" => true,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
            "application/vnd.ms-excel" => true,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true,
            "application/vnd.ms-powerpoint" => true,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => true,
            _ => false
        };

        /// <summary>Checks if this is an archive (ZIP, RAR, 7z, etc.).</summary>
        public bool IsArchive => Value switch
        {
            "application/zip" => true,
            "application/x-zip-compressed" => true,
            "application/gzip" => true,
            "application/x-gzip" => true,
            "application/vnd.rar" => true,
            "application/x-rar-compressed" => true,
            "application/x-7z-compressed" => true,
            "application/x-tar" => true,
            _ => false
        };

        /// <summary>
        /// Gets the typical file extension for this MIME type (e.g., ".pdf").
        /// </summary>
        public string Extension => Value switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/bmp" => ".bmp",
            "image/x-icon" => ".ico",
            "image/tiff" => ".tiff",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "text/plain" => ".txt",
            "text/html" => ".html",
            "text/css" => ".css",
            "text/csv" => ".csv",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "application/zip" or "application/x-zip-compressed" => ".zip",
            "application/gzip" or "application/x-gzip" => ".gz",
            "application/vnd.rar" or "application/x-rar-compressed" => ".rar",
            "application/x-7z-compressed" => ".7z",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/flac" => ".flac",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            "video/x-msvideo" => ".avi",
            "application/octet-stream" => ".bin",
            _ => string.Empty
        };

        /// <summary>
        /// Creates a new MIME type with the specified value.
        /// </summary>
        public MimeType(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

            var normalizedValue = value.Trim().ToLowerInvariant();

            if (normalizedValue.Length > MaxLength)
                throw new ArgumentException($"MIME type cannot exceed {MaxLength} characters.", nameof(value));

            if (!normalizedValue.Contains('/'))
                throw new ArgumentException("MIME type must contain '/' separator (e.g., 'image/png').", nameof(value));

            var parts = normalizedValue.Split('/');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new ArgumentException("MIME type must have format 'type/subtype'.", nameof(value));

            _value = normalizedValue;
        }

        /// <summary>
        /// Gets the typical file extension for this MIME type.
        /// </summary>
        /// <remarks>Deprecated: Use <see cref="Extension"/> property instead.</remarks>
        [Obsolete("Use Extension property instead.")]
        public string GetExtension() => Extension;

        /// <summary>
        /// Creates a MimeType from a file extension.
        /// </summary>
        public static MimeType FromExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return OctetStream;

            var ext = extension.TrimStart('.').ToLowerInvariant();

            return ext switch
            {
                "jpg" or "jpeg" => ImageJpeg,
                "png" => ImagePng,
                "gif" => ImageGif,
                "webp" => ImageWebp,
                "svg" => ImageSvg,
                "bmp" => ImageBmp,
                "ico" => ImageIco,
                "tiff" or "tif" => ImageTiff,
                "pdf" => ApplicationPdf,
                "doc" => ApplicationDoc,
                "docx" => ApplicationDocx,
                "xls" => ApplicationXls,
                "xlsx" => ApplicationXlsx,
                "ppt" => ApplicationPpt,
                "pptx" => ApplicationPptx,
                "txt" => TextPlain,
                "html" or "htm" => TextHtml,
                "css" => TextCss,
                "csv" => TextCsv,
                "json" => ApplicationJson,
                "xml" => ApplicationXml,
                "zip" => ApplicationZip,
                "gz" or "gzip" => ApplicationGzip,
                "rar" => ApplicationRar,
                "7z" => Application7z,
                "mp3" => AudioMpeg,
                "wav" => AudioWav,
                "ogg" => AudioOgg,
                "flac" => AudioFlac,
                "mp4" => VideoMp4,
                "webm" => VideoWebm,
                "mov" => VideoMov,
                "avi" => VideoAvi,
                _ => OctetStream
            };
        }

        /// <summary>
        /// Creates a MimeType from a file path.
        /// </summary>
        public static MimeType FromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return OctetStream;

            var extension = Path.GetExtension(path);
            return FromExtension(extension);
        }

        public static implicit operator string(MimeType mimeType) => mimeType.Value;

        public static implicit operator MimeType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Empty;

            return TryCreate(value, out var mimeType) ? mimeType : OctetStream;
        }

        public bool Equals(MimeType other) =>
            string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is MimeType other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public static bool operator ==(MimeType left, MimeType right) => left.Equals(right);
        public static bool operator !=(MimeType left, MimeType right) => !left.Equals(right);

        public int CompareTo(MimeType other) =>
            string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public static bool operator <(MimeType left, MimeType right) => left.CompareTo(right) < 0;
        public static bool operator <=(MimeType left, MimeType right) => left.CompareTo(right) <= 0;
        public static bool operator >(MimeType left, MimeType right) => left.CompareTo(right) > 0;
        public static bool operator >=(MimeType left, MimeType right) => left.CompareTo(right) >= 0;

        public override string ToString() => Value;

        public static MimeType Create(string value) => new(value);

        public static bool TryCreate(string? value, out MimeType mimeType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                mimeType = Empty;
                return false;
            }

            try
            {
                mimeType = new MimeType(value);
                return true;
            }
            catch (ArgumentException)
            {
                mimeType = Empty;
                return false;
            }
        }

        public static MimeType Parse(string s, IFormatProvider? provider)
        {
            if (TryParse(s, provider, out var result))
                return result;

            throw new FormatException($"Cannot parse '{s}' as MimeType.");
        }

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out MimeType result)
            => TryCreate(s, out result);
    }
}
