# Asset Value Object - Examples

Complete examples for using the `Asset` value object.

## Table of Contents
- [Quick Start](#quick-start)
- [Creating Assets](#creating-assets)
- [FileSize VO](#filesize-vo)
- [Properties & Metadata](#properties--metadata)
- [Signature-Based MIME Detection](#signature-based-mime-detection)
- [Implicit Conversions](#implicit-conversions)
- [Nested Types](#nested-types)
- [Validation](#validation)
- [Binary Storage (EF Core / Disk)](#binary-storage-ef-core--disk)
- [Blazor File Upload](#blazor-file-upload)
- [API Controller](#api-controller)

---

## Quick Start

```csharp
using Zonit.Extensions;

// Multiple ways to create Asset
Asset asset = File.ReadAllBytes("photo.jpg");       // From byte[]
Asset asset = File.OpenRead("document.pdf");        // From FileStream
Asset asset = memoryStream;                         // From MemoryStream
Asset asset = new Asset(bytes, "report.pdf");       // With explicit name

// Core properties
Console.WriteLine(asset.Id);             // "7a3b9c4d-1234-5678-90ab-cdef12345678"
Console.WriteLine(asset.OriginalName);   // "report.pdf"
Console.WriteLine(asset.UniqueName);     // "7a3b9c4d-1234-5678-90ab-cdef12345678.pdf"
Console.WriteLine(asset.MediaType);      // "application/pdf" (detected from signature!)
Console.WriteLine(asset.Signature);      // SignatureType.Pdf
Console.WriteLine(asset.Extension);      // ".pdf"
Console.WriteLine(asset.Size);           // "1.5 MB" (FileSize VO)
Console.WriteLine(asset.Size.Megabytes); // 1.5
Console.WriteLine(asset.CreatedAt);      // 2026-01-21 12:34:56 UTC
Console.WriteLine(asset.Hash);           // SHA256 base64
Console.WriteLine(asset.Md5);            // MD5 base64

// Convert back to Stream
Stream stream = asset;
await stream.CopyToAsync(outputStream);
```

---

## Creating Assets

### From Stream (Most Common)

```csharp
// From FileStream - common in file processing
using var fileStream = File.OpenRead(@"C:\Documents\report.pdf");
Asset asset = fileStream;

// With explicit name (useful when stream doesn't have name)
using var stream = await httpClient.GetStreamAsync(url);
Asset asset = new Asset(stream, "downloaded-file.pdf");

// From MemoryStream
using var memoryStream = new MemoryStream(buffer);
Asset asset = memoryStream;
```

### From Bytes

```csharp
// Simple - auto GUID name
byte[] bytes = await File.ReadAllBytesAsync("photo.jpg");
Asset asset = bytes;

// With filename
Asset asset = new Asset(bytes, "photo.jpg");

// With explicit MIME type
Asset asset = new Asset(bytes, "data.bin", Asset.MimeType.ApplicationPdf);
```

### From Base64

```csharp
// Constructor
Asset asset = new Asset(base64String, "document.pdf");

// Static method
Asset asset = Asset.FromBase64(base64String, "document.pdf");
```

### Safe Creation (TryCreate)

```csharp
// From bytes
if (Asset.TryCreate(bytes, filename, out var asset))
{
    // Use asset
}
else
{
    // Invalid input (null, empty, too large)
}

// From stream
if (Asset.TryCreate(stream, filename, out var asset))
{
    await SaveAssetAsync(asset);
}
```

### Empty Asset

```csharp
// For optional properties
var empty = Asset.Empty;
Console.WriteLine(empty.HasValue);  // false

// In entities with nullable
public class Document
{
    public Asset? Attachment { get; set; }
}
```

---

## FileSize VO

`FileSize` works like `TimeSpan` but for file sizes:

### Creating FileSize

```csharp
// From bytes
var size = new FileSize(1_500_000);

// From units
var size = FileSize.FromKilobytes(1024);
var size = FileSize.FromMegabytes(1.5);
var size = FileSize.FromGigabytes(2);
var size = FileSize.FromTerabytes(0.5);

// Constants
FileSize.OneMegabyte       // 1 MB
FileSize.TenMegabytes      // 10 MB
FileSize.HundredMegabytes  // 100 MB
FileSize.OneGigabyte       // 1 GB
```

### Converting FileSize

```csharp
var size = FileSize.FromMegabytes(1.5);

Console.WriteLine(size.Bytes);       // 1572864
Console.WriteLine(size.Kilobytes);   // 1536
Console.WriteLine(size.Megabytes);   // 1.5
Console.WriteLine(size.Gigabytes);   // 0.00146...
Console.WriteLine(size.Terabytes);   // 0.00000146...

// Auto-formatted string
Console.WriteLine(size);  // "1.50 MB"
Console.WriteLine(FileSize.FromGigabytes(2.5));  // "2.50 GB"
```

### FileSize Arithmetic

```csharp
var size1 = FileSize.FromMegabytes(10);
var size2 = FileSize.FromMegabytes(5);

var total = size1 + size2;         // 15 MB
var diff = size1 - size2;          // 5 MB
var doubled = size1 * 2;           // 20 MB
var half = size1 / 2;              // 5 MB
```

### FileSize Comparison

```csharp
var maxSize = FileSize.FromMegabytes(10);
var fileSize = new FileSize(file.Length);

if (fileSize > maxSize)
{
    throw new Exception($"File {fileSize} exceeds limit {maxSize}");
}

// Compare in Asset
if (asset.Size > FileSize.FromMegabytes(50))
{
    Console.WriteLine("Large file detected");
}
```

---

## Properties & Metadata

```csharp
Asset asset = new Asset(bytes, "report.pdf");

// Identity
Console.WriteLine(asset.Id);          // Guid - unique, never changes
Console.WriteLine(asset.UniqueName);  // "{Id}.pdf" - safe for filesystem

// Name
Console.WriteLine(asset.OriginalName.Value);                 // "report.pdf"
Console.WriteLine(asset.OriginalName.NameWithoutExtension);  // "report"
Console.WriteLine(asset.OriginalName.Extension);             // ".pdf"
Console.WriteLine(asset.OriginalName.ExtensionWithoutDot);   // "pdf"

// MIME type (detected from binary signature!)
Console.WriteLine(asset.MediaType.Value);     // "application/pdf"
Console.WriteLine(asset.MediaType.Type);      // "application"
Console.WriteLine(asset.MediaType.Subtype);   // "pdf"
Console.WriteLine(asset.MediaType.Extension); // ".pdf"

// Signature (magic bytes detection)
Console.WriteLine(asset.Signature);           // SignatureType.Pdf

// Size (FileSize VO)
Console.WriteLine(asset.Size);           // "1.5 MB"
Console.WriteLine(asset.Size.Megabytes); // 1.5
Console.WriteLine(asset.Size.Kilobytes); // 1536

// Timestamps
Console.WriteLine(asset.CreatedAt);  // DateTime UTC

// Hashes (computed once at creation)
Console.WriteLine(asset.Hash);   // SHA256 base64
Console.WriteLine(asset.Sha256); // SHA256 base64 (alias)
Console.WriteLine(asset.Md5);    // MD5 base64

// Type checks (based on MediaType)
Console.WriteLine(asset.IsImage);     // false
Console.WriteLine(asset.IsDocument);  // true
Console.WriteLine(asset.Category);    // AssetCategory.Document

// Data formats (computed on demand - not stored)
Console.WriteLine(asset.Base64);      // Base64 encoded data
Console.WriteLine(asset.DataUrl);     // data:application/pdf;base64,...
```

---

## Signature-Based MIME Detection

**MediaType is ALWAYS detected from binary signature (magic bytes)**, not from file extension. This prevents extension spoofing and ensures correct handling by external APIs.

```csharp
// Example: WebP file uploaded with wrong .jpg extension
byte[] webpBytes = File.ReadAllBytes("image.webp");
Asset asset = new Asset(webpBytes, "fake.jpg");

// MediaType is detected from signature, NOT from extension
Console.WriteLine(asset.Signature);    // SignatureType.WebP
Console.WriteLine(asset.MediaType);    // "image/webp" (correct!)
Console.WriteLine(asset.OriginalName); // "fake.jpg" (preserved)
Console.WriteLine(asset.Extension);    // ".webp" (from MediaType!)
Console.WriteLine(asset.UniqueName);   // "{guid}.webp" (correct extension)

// This prevents API errors like:
// "Image does not match the provided media type image/jpeg"
// because we always send the TRUE media type from signature
```

### Supported Signatures

```csharp
public enum SignatureType
{
    Unknown,
    
    // Images
    Jpeg, Png, Gif, WebP, Bmp, Tiff, Ico,
    
    // Documents
    Pdf, Zip, Rar, SevenZip, Gzip,
    
    // Audio/Video
    Mp3, Mp4, WebM, Ogg, Wav, Avi, Mov,
    
    // Other
    Xml, Html
}
```

### Signature Validation

```csharp
Asset asset = new Asset(bytes, "file.pdf");

// Check if signature was detected
if (asset.IsSignatureValid())
{
    Console.WriteLine($"Detected: {asset.Signature}");
}
else
{
    Console.WriteLine("Could not detect file type from magic bytes");
}
```

---

## Implicit Conversions

### From (input)

```csharp
// From byte[]
byte[] bytes = File.ReadAllBytes("file.pdf");
Asset asset = bytes;

// From Stream
using FileStream fs = File.OpenRead("file.pdf");
Asset asset = fs;

// From MemoryStream
MemoryStream ms = new MemoryStream(buffer);
Asset asset = ms;
```

### To (output)

```csharp
Asset asset = new Asset(bytes, "file.pdf");

// To byte[]
byte[] data = asset;

// To MemoryStream (for APIs that need Stream)
MemoryStream stream = asset;
await stream.CopyToAsync(destination);

// To ReadOnlyMemory<byte> (zero-copy)
ReadOnlyMemory<byte> memory = asset;

// To Span (method, not implicit)
ReadOnlySpan<byte> span = asset.AsSpan();
```

### Stream Usage Examples

```csharp
// Copy to file
Asset asset = GetAsset();
using Stream stream = asset;
using var file = File.Create("output.pdf");
await stream.CopyToAsync(file);

// Send via HTTP
Asset asset = GetAsset();
using Stream stream = asset;
var content = new StreamContent(stream);
await httpClient.PostAsync(url, content);

// Zip compression
using var zipStream = new MemoryStream();
using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
{
    var entry = archive.CreateEntry(asset.UniqueName);
    using var entryStream = entry.Open();
    ((Stream)asset).CopyTo(entryStream);
}
```

---

## Nested Types

### Asset.FileName

```csharp
// Create
var fileName = new Asset.FileName("report.pdf");

// Properties
Console.WriteLine(fileName.Value);                 // "report.pdf"
Console.WriteLine(fileName.NameWithoutExtension);  // "report"
Console.WriteLine(fileName.Extension);             // ".pdf"
Console.WriteLine(fileName.ExtensionWithoutDot);   // "pdf"
Console.WriteLine(fileName.HasValue);              // true
Console.WriteLine(fileName.MimeType);              // Asset.MimeType

// Mutations (returns new instance)
var renamed = fileName.WithExtension(".docx");  // "report.docx"
var backup = fileName.WithSuffix("_backup");    // "report_backup.pdf"

// Sanitize user input
var safe = Asset.FileName.Sanitize("../../etc/passwd");  // "etc_passwd"

// Safe creation
if (Asset.FileName.TryCreate(userInput, out var name))
{
    // Valid filename
}
```

### Asset.MimeType

```csharp
// Create
var mimeType = new Asset.MimeType("image/png");

// Use predefined constants
var png = Asset.MimeType.ImagePng;
var pdf = Asset.MimeType.ApplicationPdf;

// Properties
Console.WriteLine(mimeType.Value);      // "image/png"
Console.WriteLine(mimeType.Type);       // "image"
Console.WriteLine(mimeType.Subtype);    // "png"
Console.WriteLine(mimeType.Extension);  // ".png"

// Type checks
Console.WriteLine(mimeType.IsImage);     // true
Console.WriteLine(mimeType.IsDocument);  // false

// Create from extension/path
var fromExt = Asset.MimeType.FromExtension(".jpg");   // image/jpeg
var fromPath = Asset.MimeType.FromPath("photo.webp"); // image/webp

// Implicit conversions
string str = mimeType;              // "image/png"
Asset.MimeType mime = "image/jpeg"; // Auto-converts
```

### Common MIME Type Constants

```csharp
// Images
Asset.MimeType.ImagePng       // image/png
Asset.MimeType.ImageJpeg      // image/jpeg
Asset.MimeType.ImageGif       // image/gif
Asset.MimeType.ImageWebp      // image/webp
Asset.MimeType.ImageSvg       // image/svg+xml

// Documents
Asset.MimeType.ApplicationPdf   // application/pdf
Asset.MimeType.ApplicationDocx  // application/vnd.openxmlformats-officedocument.wordprocessingml.document
Asset.MimeType.ApplicationXlsx  // application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

// Text
Asset.MimeType.TextPlain        // text/plain
Asset.MimeType.ApplicationJson  // application/json

// Video/Audio
Asset.MimeType.VideoMp4   // video/mp4
Asset.MimeType.AudioMpeg  // audio/mpeg

// Archives
Asset.MimeType.ApplicationZip  // application/zip
```

---

## Validation

```csharp
// Predefined options
var imageOptions = AssetValidationOptions.Images();      // max 10 MB, image types
var docOptions = AssetValidationOptions.Documents();     // max 50 MB, doc types

// Custom validation
var options = new AssetValidationOptions
{
    MaxSize = FileSize.FromMegabytes(5),  // 5 MB
    AllowedExtensions = new[] { "jpg", "png" },
    ValidateSignature = true
};

// Validate
var result = asset.Validate(options);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine(error);
}

// Quick checks
if (!asset.IsWithinSizeLimit(FileSize.FromMegabytes(10)))
    throw new Exception("File too large");

if (!asset.IsAllowedType(Asset.MimeType.ImageJpeg, Asset.MimeType.ImagePng))
    throw new Exception("Only JPEG and PNG allowed");
```

---

## Binary Storage (EF Core / Disk)

Asset uses a **compact binary format (V4)** for maximum performance when storing in databases or on disk.

### Storage Format V4 (Binary)

```
[1 byte]   Version (4)
[16 bytes] Id (GUID)
[1 byte]   Signature (enum)
[8 bytes]  CreatedAt (UTC ticks)
[2 bytes]  MimeType length
[N bytes]  MimeType (UTF-8)
[2 bytes]  OriginalName length  
[N bytes]  OriginalName (UTF-8)
[44 bytes] Sha256 (Base64 fixed)
[24 bytes] Md5 (Base64 fixed)
[remaining] File data
```

**Header overhead: ~100-150 bytes** (vs ~300 bytes in legacy JSON format)

### Usage

```csharp
// Serialize to storage bytes
Asset asset = new Asset(bytes, "document.pdf");
byte[] storageBytes = asset.ToStorageBytes();

// Store in database (EF Core byte[] column)
entity.Attachment = asset.ToStorageBytes();
await dbContext.SaveChangesAsync();

// Deserialize from storage
byte[] loaded = entity.Attachment;
Asset restored = Asset.FromStorageBytes(loaded);

// All metadata is preserved
Console.WriteLine(restored.Id);           // Same GUID
Console.WriteLine(restored.MediaType);    // Same MIME type
Console.WriteLine(restored.Signature);    // Same signature
Console.WriteLine(restored.Sha256);       // Same hash
```

### Entity Configuration

```csharp
public class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public byte[] Attachment { get; set; }  // Store as byte[]
}

// In your service
public async Task SaveDocumentAsync(Document doc, Asset attachment)
{
    doc.Attachment = attachment.ToStorageBytes();
    await dbContext.SaveChangesAsync();
}

public Asset GetAttachment(Document doc)
{
    return Asset.FromStorageBytes(doc.Attachment);
}
```

### Backward Compatibility

`FromStorageBytes` automatically detects format version:
- **V4**: Binary format (new, fastest)
- **V3**: JSON with Signature
- **V2**: JSON without Signature  
- **V1**: Legacy JSON

All old data is readable - no migration needed.

---

## Blazor File Upload

```razor
@page "/upload"
@using Zonit.Extensions

<InputFile OnChange="HandleFile" accept=".jpg,.png,.pdf" />

@if (asset.HasValue)
{
    <p>File: @asset.OriginalName</p>
    <p>Size: @asset.Size</p>
    <p>Type: @asset.MediaType (detected from signature)</p>
    <p>Signature: @asset.Signature</p>
    
    @if (asset.IsImage)
    {
        <img src="@asset.DataUrl" style="max-width: 300px" />
    }
}

@code {
    private Asset asset = Asset.Empty;

    private async Task HandleFile(InputFileChangeEventArgs e)
    {
        var file = e.File;
        
        // Read file (max 10 MB)
        await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        
        // Create Asset from stream - MIME type detected from signature!
        asset = new Asset(stream, file.Name);
        
        // MediaType is from signature, not from extension
        // Prevents "file.jpg" that is actually WebP from causing issues
        Console.WriteLine($"Claimed: {file.ContentType}, Actual: {asset.MediaType}");
        
        // Validate
        if (asset.Size > FileSize.FromMegabytes(5))
        {
            // Handle too large
        }
    }
}
```

---

## API Controller

```csharp
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("No file");

        // Create Asset from upload - MIME type detected from signature!
        await using var stream = file.OpenReadStream();
        Asset asset = new Asset(stream, file.FileName);

        // Validate
        if (asset.Size > FileSize.FromMegabytes(50))
            return BadRequest($"File too large: {asset.Size}");

        // MediaType is from signature, safe for external APIs (Anthropic, OpenAI, etc.)
        Console.WriteLine($"Claimed: {file.ContentType}, Actual: {asset.MediaType}");

        // Save using unique name (with correct extension from MediaType)
        var path = Path.Combine("uploads", asset.UniqueName);
        await File.WriteAllBytesAsync(path, asset.Data);

        return Ok(new { 
            Id = asset.Id,
            Name = asset.OriginalName.Value,
            MediaType = asset.MediaType.Value,
            Signature = asset.Signature.ToString(),
            Size = asset.Size.ToString(),
            Hash = asset.Hash
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Download(Guid id)
    {
        // Load asset from storage
        Asset asset = await LoadAssetAsync(id);
        
        if (!asset.HasValue)
            return NotFound();

        // Return with correct MIME type (from signature) and filename
        return File(asset.Data, asset.MediaType.Value, asset.OriginalName.Value);
    }
}
```

---

## JSON Serialization

Asset serializes to JSON with full metadata:

```json
{
  "id": "7a3b9c4d-1234-5678-90ab-cdef12345678",
  "originalName": "document.pdf",
  "uniqueName": "7a3b9c4d-1234-5678-90ab-cdef12345678.pdf",
  "mimeType": "application/pdf",
  "signature": "Pdf",
  "extension": ".pdf",
  "sizeBytes": 1572864,
  "size": "1.50 MB",
  "createdAt": "2026-01-21T12:34:56.0000000Z",
  "sha256": "abc123...",
  "md5": "def456...",
  "category": "Document",
  "data": "JVBERi0xLjQKJ..."
}
```

### Deserializing

```csharp
// Simple Base64 string
Asset asset = JsonSerializer.Deserialize<Asset>("\"JVBERi0xLjQK...\"");

// Full object format
Asset asset = JsonSerializer.Deserialize<Asset>(jsonString);
```
