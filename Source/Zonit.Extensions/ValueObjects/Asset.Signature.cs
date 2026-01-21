namespace Zonit.Extensions;

public readonly partial struct Asset
{
    /// <summary>
    /// File signature types detected by magic bytes.
    /// </summary>
    public enum SignatureType
    {
        /// <summary>Unknown or undetected signature.</summary>
        Unknown = 0,

        // Images
        Jpeg,
        Png,
        Gif,
        WebP,
        Bmp,
        Tiff,
        Ico,

        // Documents
        Pdf,
        Zip,
        Rar,
        SevenZip,
        Gzip,

        // Audio/Video
        Mp3,
        Mp4,
        WebM,
        Ogg,
        Wav,
        Avi,
        Mov,

        // Other
        Xml,
        Html
    }

    /// <summary>
    /// Detects file signature (magic bytes) from the file content.
    /// This verifies what the file actually is, not what the extension claims.
    /// </summary>
    /// <returns>Detected file signature type.</returns>
    public SignatureType DetectSignature()
    {
        if (!HasValue || Size < 4)
            return SignatureType.Unknown;

        var data = Data;

        // JPEG: FF D8 FF
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return SignatureType.Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return SignatureType.Png;

        // GIF: 47 49 46 38 (GIF8)
        if (data.Length >= 4 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return SignatureType.Gif;

        // WebP: 52 49 46 46 xx xx xx xx 57 45 42 50 (RIFF....WEBP)
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return SignatureType.WebP;

        // BMP: 42 4D (BM)
        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
            return SignatureType.Bmp;

        // TIFF: 49 49 2A 00 (little endian) or 4D 4D 00 2A (big endian)
        if (data.Length >= 4 &&
            ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
             (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A)))
            return SignatureType.Tiff;

        // ICO: 00 00 01 00
        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
            return SignatureType.Ico;

        // PDF: 25 50 44 46 (%PDF)
        if (data.Length >= 4 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
            return SignatureType.Pdf;

        // ZIP/DOCX/XLSX/PPTX: 50 4B 03 04 (PK..)
        if (data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04)
            return SignatureType.Zip;

        // RAR: 52 61 72 21 1A 07 (Rar!..)
        if (data.Length >= 6 && data[0] == 0x52 && data[1] == 0x61 && data[2] == 0x72 && data[3] == 0x21 &&
            data[4] == 0x1A && data[5] == 0x07)
            return SignatureType.Rar;

        // 7Z: 37 7A BC AF 27 1C
        if (data.Length >= 6 && data[0] == 0x37 && data[1] == 0x7A && data[2] == 0xBC && data[3] == 0xAF &&
            data[4] == 0x27 && data[5] == 0x1C)
            return SignatureType.SevenZip;

        // GZIP: 1F 8B
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
            return SignatureType.Gzip;

        // MP3: FF FB or FF FA or FF F3 or ID3
        if (data.Length >= 3 && ((data[0] == 0xFF && (data[1] == 0xFB || data[1] == 0xFA || data[1] == 0xF3)) ||
            (data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33))) // ID3
            return SignatureType.Mp3;

        // MP4: ftyp at offset 4
        if (data.Length >= 8 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
            return SignatureType.Mp4;

        // WebM: 1A 45 DF A3 (EBML header, also used by MKV)
        if (data.Length >= 4 && data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3)
            return SignatureType.WebM;

        // OGG: 4F 67 67 53 (OggS)
        if (data.Length >= 4 && data[0] == 0x4F && data[1] == 0x67 && data[2] == 0x67 && data[3] == 0x53)
            return SignatureType.Ogg;

        // WAV: 52 49 46 46 xx xx xx xx 57 41 56 45 (RIFF....WAVE)
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x41 && data[10] == 0x56 && data[11] == 0x45)
            return SignatureType.Wav;

        // AVI: 52 49 46 46 xx xx xx xx 41 56 49 20 (RIFF....AVI )
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x41 && data[9] == 0x56 && data[10] == 0x49 && data[11] == 0x20)
            return SignatureType.Avi;

        // MOV: various ftyp variants or moov/mdat atoms
        // Simplified check - if ftyp contains 'qt' it's likely MOV
        if (data.Length >= 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70 &&
            data[8] == 0x71 && data[9] == 0x74) // 'qt'
            return SignatureType.Mov;

        // XML: <?xml or <
        if (data.Length >= 5 && data[0] == 0x3C && data[1] == 0x3F && data[2] == 0x78 && data[3] == 0x6D && data[4] == 0x6C)
            return SignatureType.Xml;

        // HTML: <!DOCTYPE html or <html (simplified)
        if (data.Length >= 15 && data[0] == 0x3C && data[1] == 0x21 && data[2] == 0x44 && data[3] == 0x4F &&
            data[4] == 0x43 && data[5] == 0x54 && data[6] == 0x59 && data[7] == 0x50 && data[8] == 0x45)
            return SignatureType.Html;

        if (data.Length >= 5 && data[0] == 0x3C && data[1] == 0x68 && data[2] == 0x74 && data[3] == 0x6D && data[4] == 0x6C)
            return SignatureType.Html;

        return SignatureType.Unknown;
    }

    /// <summary>
    /// Gets the expected MIME type based on the detected signature.
    /// </summary>
    public MimeType GetSignatureMimeType() => DetectSignature() switch
    {
        SignatureType.Jpeg => MimeType.ImageJpeg,
        SignatureType.Png => MimeType.ImagePng,
        SignatureType.Gif => MimeType.ImageGif,
        SignatureType.WebP => MimeType.ImageWebp,
        SignatureType.Bmp => MimeType.ImageBmp,
        SignatureType.Tiff => MimeType.ImageTiff,
        SignatureType.Ico => MimeType.ImageIco,
        SignatureType.Pdf => MimeType.ApplicationPdf,
        SignatureType.Zip => MimeType.ApplicationZip,
        SignatureType.Rar => MimeType.ApplicationRar,
        SignatureType.SevenZip => MimeType.Application7z,
        SignatureType.Gzip => MimeType.ApplicationGzip,
        SignatureType.Mp3 => MimeType.AudioMpeg,
        SignatureType.Mp4 => MimeType.VideoMp4,
        SignatureType.WebM => MimeType.VideoWebm,
        SignatureType.Ogg => MimeType.AudioOgg,
        SignatureType.Wav => MimeType.AudioWav,
        SignatureType.Avi => MimeType.VideoAvi,
        SignatureType.Mov => MimeType.VideoMov,
        SignatureType.Xml => MimeType.ApplicationXml,
        SignatureType.Html => MimeType.TextHtml,
        _ => MimeType.OctetStream
    };

    /// <summary>
    /// Validates that the file signature matches the claimed MIME type.
    /// Useful for security - detecting renamed files.
    /// </summary>
    /// <returns>True if signature matches MIME type or signature is unknown.</returns>
    public bool IsSignatureValid()
    {
        var signature = DetectSignature();

        // If we can't detect signature, assume valid (text files, etc.)
        if (signature == SignatureType.Unknown)
            return true;

        // Get expected MIME type from signature
        var signatureMime = GetSignatureMimeType();

        // Special case: ZIP can be DOCX, XLSX, PPTX, etc.
        if (signature == SignatureType.Zip)
        {
            var ext = OriginalName.ExtensionWithoutDot.ToLowerInvariant();
            return ext is "zip" or "docx" or "xlsx" or "pptx" or "odt" or "ods" or "odp" or "jar" or "apk";
        }

        // Compare primary types (image, audio, video, etc.)
        return signatureMime.Type == ContentType.Type;
    }

    /// <summary>
    /// Gets a warning message if signature doesn't match MIME type.
    /// </summary>
    public string? GetSignatureMismatchWarning()
    {
        if (IsSignatureValid())
            return null;

        var signature = DetectSignature();
        var signatureMime = GetSignatureMimeType();

        return $"File claims to be '{ContentType.Value}' but signature indicates '{signatureMime.Value}' ({signature}).";
    }
}
