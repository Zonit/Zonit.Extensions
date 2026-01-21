# Asset Value Object - Examples

Complete examples for using the `Asset` value object.

## Table of Contents
- [Quick Start](#quick-start)
- [Creating Assets](#creating-assets)
- [FileSize VO](#filesize-vo)
- [Properties & Metadata](#properties--metadata)
- [Implicit Conversions](#implicit-conversions)
- [Nested Types](#nested-types)
- [File Signature Detection](#file-signature-detection)
- [Validation](#validation)
- [Entity Framework Core](#entity-framework-core)
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
Console.WriteLine(asset.ContentType);    // "application/pdf"
Console.WriteLine(asset.Extension);      // ".pdf"
Console.WriteLine(asset.Size);            // "1.5 MB" (FileSize VO)
Console.WriteLine(asset.Size.Megabytes);  // 1.5
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

// MIME type
Console.WriteLine(asset.ContentType.Value);     // "application/pdf"
Console.WriteLine(asset.ContentType.Type);      // "application"
Console.WriteLine(asset.ContentType.Subtype);   // "pdf"
Console.WriteLine(asset.ContentType.Extension); // ".pdf"

// Size (FileSize VO)
Console.WriteLine(asset.Size);           // "1.5 MB"
Console.WriteLine(asset.Size.Megabytes); // 1.5
Console.WriteLine(asset.Size.Kilobytes); // 1536

// Timestamps
Console.WriteLine(asset.CreatedAt);  // DateTime UTC

// Hashes (lazy computed)
Console.WriteLine(asset.Hash);   // SHA256 base64
Console.WriteLine(asset.Sha256); // SHA256 base64 (alias)
Console.WriteLine(asset.Md5);    // MD5 base64

// Type checks
Console.WriteLine(asset.IsImage);     // false
Console.WriteLine(asset.IsDocument);  // true
Console.WriteLine(asset.Category);    // AssetCategory.Document
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

## File Signature Detection

```csharp
Asset asset = new Asset(bytes, "file.jpg");

// Detect actual file type from magic bytes
var signature = asset.DetectSignature();
Console.WriteLine(signature);  // SignatureType.Jpeg

// Verify claimed type matches actual
if (!asset.IsSignatureValid())
{
    var warning = asset.GetSignatureMismatchWarning();
    // "File claims to be 'image/jpeg' but signature indicates 'application/pdf'"
}
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
    MaxSizeBytes = 5 * 1024 * 1024,  // 5 MB
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

## Entity Framework Core

### Entity

```csharp
public class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public Asset Attachment { get; set; }  // Stored as byte[] with metadata header
}
```

### DbContext Configuration

```csharp
// Using Zonit.Extensions.Databases.SqlServer
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseZonitDatabasesConverters();
}
```

### Storage Format

Asset is stored as binary with embedded header:
```
[4 bytes: header length][UTF-8 JSON header][file data]
```

Header contains: Id, Name, MimeType, CreatedAt, Hash, Md5

This allows full restoration of Asset with all metadata from a single column.

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
    <p>Type: @asset.ContentType</p>
    
    @if (asset.IsImage)
    {
        <img src="@asset.ToDataUrl()" style="max-width: 300px" />
    }
}

@code {
    private Asset asset = Asset.Empty;

    private async Task HandleFile(InputFileChangeEventArgs e)
    {
        var file = e.File;
        
        // Read file (max 10 MB)
        await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        
        // Create Asset from stream
        asset = new Asset(stream, file.Name);
        
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

        // Create Asset from upload
        await using var stream = file.OpenReadStream();
        Asset asset = new Asset(stream, file.FileName);

        // Validate
        if (asset.Size > FileSize.FromMegabytes(50))
            return BadRequest($"File too large: {asset.Size}");

        if (!asset.IsSignatureValid())
            return BadRequest(asset.GetSignatureMismatchWarning());

        // Save using unique name
        var path = Path.Combine("uploads", asset.UniqueName);
        await File.WriteAllBytesAsync(path, asset.Data);

        return Ok(new { 
            Id = asset.Id,
            Name = asset.OriginalName.Value,
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

        // Return with correct MIME type and filename
        return File(asset.Data, asset.ContentType.Value, asset.OriginalName.Value);
    }
}
```
