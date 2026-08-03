# Asset, FileSize, Color

Three value objects in namespace `Zonit.Extensions`. `Asset` is a self-contained file capsule; `FileSize`
is a `TimeSpan`-shaped byte count; `Color` is an OKLCH colour. Nothing to register.

The general creation/`Empty`/JSON contract these follow is in
`.zonit/extensions/core/value-objects.md`; uploading through a Blazor form is in
`.zonit/extensions/core/binding.md`.

## Read this first

- **`Asset` takes ownership of the byte array you hand it — it does not copy.** If you mutate that array
  afterwards, the payload changes while `Sha256`, `Size` and `Signature` keep their construction-time
  values. Never keep a reference to the array you passed in.
- **The MIME type comes from the magic bytes, not the file name.** PNG bytes named `report.pdf` produce
  `MediaType == "image/png"`, `Extension == ".png"` and `UniqueName == "{id}.png"`. This also means a
  `.docx` (a ZIP) reports `application/zip` and `.zip`.
- **`asset.Data` allocates a full copy on every call.** Use `AsSpan()` / `AsMemory()` / `ToStream()` for
  reads.
- **`Color` does not survive its own string round-trip** — see Known limitations.

## Asset

### Creating one

```csharp
Asset a1 = fileBytes;                              // implicit from byte[] — GUID-based name
Asset a2 = new Asset(fileBytes, "document.pdf");   // with an original name
Asset a3 = new Asset(stream, "document.pdf");      // reads the stream to the end
Asset a4 = Asset.FromBase64(base64, "document.pdf");

Asset.TryCreate(bytes, "document.pdf", out var a5);   // false for null/empty/over-size
Asset.TryCreate(stream, "document.pdf", out var a6);
```

The constructors throw: `ArgumentNullException` for null data, `ArgumentException` when the payload
exceeds `Asset.MaxSize` (**100 MB**, `FileSize.HundredMegabytes`). `TryCreate` returns `false` instead —
including for a zero-length array, which is never a valid `Asset`.

### The byte-array ownership contract

The internal constructor stores your array **as-is**; cloning would double peak memory for a type that
accepts 100 MB payloads. Nothing on the public surface hands it back — but nothing stops *you* from
holding on to it either:

```csharp
byte[] buffer = ReadUpload();
var asset = new Asset(buffer, "photo.png");

buffer[0] = 0xFF;          // you just corrupted the asset
asset.Sha256;              // still the hash of the ORIGINAL bytes — now a lie
asset.Signature;           // still Png — computed at construction
asset.AsSpan()[0];         // 0xFF — the payload really did change
```

Rules that follow from this:

- Treat the array as consumed the moment you pass it in. Do not reuse a pooled or rented buffer.
- Do not build an `Asset` from a buffer you are still writing into.
- `WithName(...)` deliberately shares the payload between old and new instance — that is safe, because
  neither can hand the array out.

### Reading the payload

| member | allocates | notes |
|---|---|---|
| `AsSpan()` | no | `ReadOnlySpan<byte>` — preferred |
| `AsMemory()` | no | `ReadOnlyMemory<byte>`, also the implicit conversion target |
| `ToStream()` | wrapper only | non-writable, non-resizable `MemoryStream` over the payload |
| `ToArray()` / `Data` | **full copy** | defensive copy, up to `MaxSize` per call |
| `Base64` / `DataUrl` | full string | computed on demand, never cached |

```csharp
ReferenceEquals(asset.Data, asset.Data);   // false — a new array each time
```

`Data` and the implicit `byte[]` conversion are the same defensive copy. Use them only when you need a
mutable, independent array.

### Metadata

```csharp
asset.Id;             // Guid, generated at construction, stable
asset.OriginalName;   // Asset.FileName — what the caller supplied (or a generated GUID name)
asset.UniqueName;     // "{Id}{Extension}" — safe for filesystem storage
asset.MediaType;      // Asset.MimeType — from the magic bytes
asset.Signature;      // Asset.SignatureType enum
asset.Extension;      // derived from MediaType, NOT from OriginalName
asset.Size;           // FileSize
asset.CreatedAt;      // DateTime, UTC
asset.Sha256;         // Base64 of the SHA-256, computed once
asset.Hash;           // alias for Sha256
asset.Category;       // AssetCategory: Image/Video/Audio/Document/Text/Archive/Other
asset.IsImage; asset.IsVideo; asset.IsAudio; asset.IsDocument; asset.IsText;
```

`Md5` and `VerifyMd5` exist but are `[Obsolete]` with diagnostic id **`ZONIT0001`** — MD5 is
cryptographically broken and they are retained only for legacy ETag interop. Use `Sha256` /
`VerifyHash(expected)`.

### Signature-first MIME resolution

The constructor picks `MediaType` in this order:

1. the magic-byte signature, if it resolved to anything other than `application/octet-stream`;
2. the `MimeType?` argument you passed;
3. the extension of the supplied name (`MimeType.FromPath`);
4. `application/octet-stream`.

```csharp
byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, /* … */];
var a = new Asset(png, "report.pdf");

a.MediaType.Value;   // "image/png"   — the bytes win
a.Signature;         // Asset.SignatureType.Png
a.Extension;         // ".png"
a.OriginalName;      // "report.pdf"  — preserved as supplied
a.UniqueName;        // "{guid}.png"
```

The magic-byte table knows 21 formats (JPEG, PNG, GIF, WebP, BMP, TIFF, ICO, PDF, ZIP, RAR, 7z, GZIP,
MP3, MP4, WebM, OGG, WAV, AVI, MOV, XML, HTML). Anything else — plain text, CSV, legacy Office, FLAC,
prolog-less SVG — yields `SignatureType.Unknown`, and rule 3 falls back to the extension:

```csharp
var t = new Asset(Encoding.UTF8.GetBytes("hello"), "notes.txt");
t.Signature;         // Unknown
t.MediaType.Value;   // "text/plain" — from the .txt extension
```

Because ZIP is a real signature, an Office file resolves to the container type, not the document type:

```csharp
var d = new Asset(docxBytes, "report.docx");
d.MediaType.Value;   // "application/zip"
d.Extension;         // ".zip"    <- so UniqueName is "{guid}.zip"
```

If you store files under `UniqueName` and serve them by extension, keep `OriginalName` alongside and use
*that* for the download filename.

### Validation

Two separate questions, and only one of them is a security check:

```csharp
asset.IsSignatureValid();        // "did the magic-byte table recognise these bytes?" — NOT a validity gate
asset.IsSignatureConsistent();   // "do the bytes contradict the file name?" — this is the real check
```

`IsSignatureValid()` is `false` for every `.txt`, `.csv`, `.doc`, `.flac` — that is normal, not an
attack. `IsSignatureConsistent()` returns `true` whenever no contradiction can be established (no
signature, unrecognised extension, container signature, or agreement) and `false` **only** on a definite
mismatch.

```csharp
var options = AssetValidationOptions.Documents();   // MaxSize 50 MB, doc extensions, ValidateSignature true
AssetValidationResult result = asset.Validate(options);

if (!result.IsValid)
    return string.Join(" ", result.Errors);
// PNG bytes named report.pdf ->
// "File content is 'image/png' but the name 'report.pdf' claims 'application/pdf'."
```

| preset | `MaxSize` | `AllowedExtensions` | `ValidateSignature` |
|---|---|---|---|
| `AssetValidationOptions.Images()` | 10 MB | jpg, jpeg, png, gif, webp, svg | `true` |
| `AssetValidationOptions.Documents()` | 50 MB | pdf, doc, docx, xls, xlsx, ppt, pptx, txt | `true` |
| `AssetValidationOptions.Audio()` | 100 MB | mp3, wav, ogg, flac | `true` |
| `AssetValidationOptions.Video()` | 500 MB | mp4, webm, mov, avi | `true` |
| `new AssetValidationOptions()` | none | none | **`false`** |

Every preset takes `(FileSize? maxSize = null, bool validateSignature = true)` if you want to override.
None of them sets `AllowedMimeTypes` — they filter on extension, because signature-first resolution makes
the MIME type of a `.docx` `application/zip`. If you set `AllowedMimeTypes` yourself, list the *detected*
types, not the ones the file name implies.

`ValidateSignature` means "reject a definite mismatch". Files with no detectable signature always pass,
and container signatures (ZIP, XML, HTML, GZIP) always pass because one signature legitimately backs many
extensions. MP4 and MOV are treated as interchangeable — both are ISO base media with the same `ftyp`
box.

Individual checks are also available: `IsAllowedType(params MimeType[])`,
`IsAllowedExtension(params string[])`, `IsWithinSizeLimit(FileSize)` / `IsWithinSizeLimit(long)`.

### Storage round-trip

`ToStorageBytes()` writes a compact binary header followed by the payload; `FromStorageBytes(byte[]?)`
reads it back with `Id`, `CreatedAt` and the hashes preserved.

```
[1]   version (4)
[16]  Id (GUID)
[1]   Signature
[8]   CreatedAt (UTC ticks, Int64 LE)
[2+N] MimeType   (UInt16 length + UTF-8)
[2+N] OriginalName (UInt16 length + UTF-8)
[44]  Sha256 (Base64, fixed width)
[24]  Md5    (Base64, fixed width)
[…]   payload
```

```csharp
byte[] blob = asset.ToStorageBytes();
Asset back = Asset.FromStorageBytes(blob);
back == asset;    // true — Id, size and payload all match

Asset.FromStorageBytes(null).HasValue;              // false
Asset.FromStorageBytes([9, 9, 9, 9, 9]).HasValue;   // false
Asset.Empty.ToStorageBytes().Length;                // 0
```

`FromStorageBytes` also reads three legacy JSON-header formats (V1-V3) for backward compatibility;
`ToStorageBytes` only ever writes V4.

EF Core:

```csharp
modelBuilder.Entity<Attachment>()
    .Property(a => a.File)
    .HasConversion(v => v.ToStorageBytes(), v => Asset.FromStorageBytes(v));
```

`AssetJsonConverter` writes a verbose object (`id`, `originalName`, `uniqueName`, `mimeType`,
`signature`, `extension`, `sizeBytes`, `size`, `createdAt`, `sha256`, `md5`, `category`, `data`) with the
payload base64-encoded in `data`, and reads either that object or a bare base64 string. Do not put a
100 MB asset in a JSON response.

### Nested types

`Asset.FileName` — 255 characters max (`Asset.FileName.MaxLength`), rejects `< > : " / \ | ? *` and NUL,
and rejects Windows reserved names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`).

```csharp
Asset.FileName.TryCreate("a/b.txt", out _);   // false
Asset.FileName.TryCreate("CON.txt", out _);   // false
Asset.FileName.Sanitize("a/b:c.txt").Value;   // "a_b_c.txt" — never throws
new Asset.FileName("report", "pdf");          // "report.pdf"
name.WithExtension("png"); name.WithSuffix("-v2"); name.MakeUnique(existingNames);
```

`Asset.MimeType` — 255 characters max, must contain `/`. Predefined statics cover the usual set
(`ImagePng`, `ApplicationPdf`, `TextPlain`, `VideoMp4`, …). `MimeType.FromExtension(".png")` and
`MimeType.FromPath("a/b.png")` map an extension to a type and fall back to `OctetStream`. The implicit
`string` conversion yields `OctetStream` for an unparseable value and `Empty` for blank — two different
"nothing"s, so test with `HasValue`.

## FileSize

A non-negative byte count with `TimeSpan`-style unit accessors. Binary units (1 KB = 1024 B).

```csharp
FileSize s = 1_572_864L;                  // implicit from long or int
var s2 = FileSize.FromMegabytes(1.5);     // 1572864 bytes
s2.Bytes; s2.Kilobytes; s2.Megabytes; s2.Gigabytes; s2.Terabytes;
s2.HasValue;   // true when Bytes > 0
s2.IsZero;
```

Constants: `Zero`, `MaxValue`, `OneKilobyte`, `OneMegabyte`, `OneGigabyte`, `OneTerabyte`,
`FiveMegabytes`, `TenMegabytes`, `TwentyFiveMegabytes`, `FiftyMegabytes`, `HundredMegabytes`,
`FiveHundredMegabytes`, `OneGigabyteLimited`, `TwoGigabytes`, `FourGigabytes`.

Arithmetic saturates at zero rather than going negative:

```csharp
((FileSize)5L - (FileSize)10L).Bytes;   // 0, not -5
```

Formatting picks a unit automatically and is **culture-sensitive** — pass a provider for machine-readable
output:

```csharp
FileSize.FromMegabytes(1.5).ToString();                                  // "1.5 MB" / "1,5 MB"
FileSize.TenMegabytes.ToString("KB", CultureInfo.InvariantCulture);      // "10,240.00 KB"
((FileSize)1023L).ToString();                                            // "1023 B"
```

Format specifiers: `B`, `KB`, `MB`, `GB`, `TB`, and `A`/null for automatic. Parsing accepts both a bare
byte count and a suffixed value:

```csharp
FileSize.Parse("1.5 MB", CultureInfo.InvariantCulture).Bytes;   // 1572864
FileSize.TryParse("500 KB", null, out var limit);
```

JSON serializes as a plain number of bytes.

## Color

Stored as OKLCH (`L` 0-1, `C` ≥ 0, `H` 0-360, `Alpha` 0-1). Components are clamped, never rejected — the
constructor cannot throw.

```csharp
var blue = Color.FromHex("#3498db");
Color b2 = "#3498db";                    // implicit; unparseable input -> Color.Transparent
Color.FromRgb(52, 152, 219);
Color.FromHsl(204, 0.7, 0.53);
Color.FromOklch(0.6531, 0.1347, 242.69);
Color.TryParse(input, null, out var c);  // accepts #hex, rgb(), rgba(), hsl(), hsla(), oklch()
```

Statics: `Color.Transparent` (== `default(Color)`), `Color.Black`, `Color.White`.

Output shapes for `#3498db`:

| member | value |
|---|---|
| `Hex` | `#3498DB` (or `#RRGGBBAA` when `Alpha < 1`) |
| `Rgb` | `(52, 152, 219)` tuple |
| `Rgba` | `(52, 152, 219, 1)` |
| `CssRgb` | `rgb(52, 152, 219)` / `rgba(…, 0.5)` |
| `CssHsl` | `hsl(204.1, 69.9%, 53.1%)` |
| `CssOklch` | `oklch(65.31% 0.1347 242.69)` — also `ToString()` and the JSON form |
| `Hsl` | `(H, S, L)` tuple |

Manipulation returns new values; hue interpolation takes the short way round:

```csharp
blue.Lighten(0.1).Hex;        // "#58B8FD"
blue.Darken(0.1);
blue.Saturate(); blue.Desaturate();
blue.WithHue(30); blue.SetLightness(0.8); blue.WithAlpha(0.5);
blue.Complementary; blue.Grayscale;
blue.Mix(Color.FromHex("#e74c3c"), 0.5);
```

`ToString(format, provider)` accepts `"hex"`/`"x"`, `"rgb"`, `"hsl"`, `"oklch"` (default). Equality is
approximate (tolerance 1e-4 on L/C/Alpha, 1e-2 on H).

## Known limitations

- **`Color` round-trips lossily through `CssOklch`, `ToString()` and JSON.** The writer emits percentage
  lightness (`oklch(65.31% …)`) but `Color.TryParse`'s regex captures the number *outside* the `%`, so
  `65.31` is read as a 0-1 lightness and clamps to 1. `#3498DB` → serialize → deserialize gives
  `#AAFFFF`. Persist `Color.Hex`, or the raw `L`/`C`/`H`/`Alpha` doubles, and only use `CssOklch` for
  rendering into CSS. Unit-form input (`oklch(0.6531 0.1347 242.69)`) parses correctly.
- `AssetTypeConverter` cannot convert **from a string** — only from `byte[]`, `Stream` and `MemoryStream`.
  An `Asset` cannot be bound from a form field or a query-string value.
- `Asset.Equals` compares `Id`, then `Size`, then the full payload. Comparing two large assets is an
  O(n) byte scan; `GetHashCode` is `Id`-only, so a hash lookup is cheap but a bucket collision is not.
- `Asset.Md5` / `VerifyMd5` raise `ZONIT0001` at every use site. Suppress it only where legacy interop
  genuinely requires MD5.
- `FileSize` multiplication and addition do not check for `long` overflow.
