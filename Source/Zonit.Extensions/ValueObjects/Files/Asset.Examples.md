# Asset, FileSize, Color

Documentation for `Asset`, its nested `FileName` / `MimeType` types, `FileSize` and `Color` now lives in
one place:
[`Instruction/extensions/core/assets.md`](../../../../Instruction/extensions/core/assets.md)
(installed into a consumer's repo as `.zonit/extensions/core/assets.md`).

This file used to hold a 700-line example gallery. It was written against the pre-10.0.0 surface and
**four of its recommendations are now wrong**:

| It showed | Reality in 10.0.0-preview.10 |
|---|---|
| `File.WriteAllBytesAsync(path, asset.Data)` and `File(asset.Data, …)` | `Data` now allocates a **full defensive copy** on every call, up to `Asset.MaxSize` (100 MB). Use `asset.AsSpan()`, `asset.AsMemory()` or `asset.ToStream()` — all three are allocation-free. Keep `Data` / `ToArray()` only when you genuinely need an independent mutable array. |
| `Console.WriteLine(asset.Md5)` | `Md5` and `VerifyMd5` are `[Obsolete]` with diagnostic id **`ZONIT0001`**. Use `Sha256` / `VerifyHash(expected)`; suppress `ZONIT0001` only for legacy ETag interop. |
| `if (asset.IsSignatureValid())` as the file-type check | `IsSignatureValid()` only answers "did the magic-byte table recognise these bytes?" and is `false` for every `.txt`, `.csv`, `.doc` and `.flac` — that is normal, not an attack. The security check is the new `asset.IsSignatureConsistent()`. |
| `ValidateSignature = true` meaning "a signature must be detectable" | It now means "the detected signature must not **contradict** the file name". Files with no signature always pass; container signatures (ZIP, XML, HTML, GZIP) always pass; only a definite mismatch is an error. |

Everything else it covered is in `assets.md`, which is verified against the shipped assembly:

- creating an `Asset` from `byte[]` / `Stream` / base64, `TryCreate`, and the **byte-array ownership
  contract** (the constructor stores your array as-is — mutating it afterwards corrupts the payload
  behind a stale `Sha256`);
- the allocation table for `AsSpan` / `AsMemory` / `ToStream` vs `ToArray` / `Data`;
- signature-first MIME resolution, its four-step ladder, and why a `.docx` reports `application/zip`;
- the four `AssetValidationOptions` presets with their real `MaxSize` and extension lists;
- the `ToStorageBytes` / `FromStorageBytes` V4 layout and the EF Core mapping;
- `Asset.FileName` and `Asset.MimeType`;
- `FileSize` units, saturating arithmetic and culture-sensitive formatting;
- `Color` (OKLCH) output shapes, manipulation, and the documented round-trip bug.

Blazor upload and API-controller usage are covered by
[`Instruction/extensions/core/binding.md`](../../../../Instruction/extensions/core/binding.md)
(`InputFile` → `Asset`) and the `assets.md` validation section.
