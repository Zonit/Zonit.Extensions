using Zonit.Extensions;

namespace Example;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Asset Value Object Demo ===\n");

        // ----------------------------------------
        // 1. Tworzenie Asset z byte[]
        // ----------------------------------------
        Console.WriteLine("1. Tworzenie z byte[]:");
        
        byte[] sampleData = "Hello World - This is sample file content!"u8.ToArray();
        Asset asset1 = sampleData;  // Implicit conversion
        
        Console.WriteLine($"   Id: {asset1.Id}");
        Console.WriteLine($"   OriginalName: {asset1.OriginalName}");
        Console.WriteLine($"   UniqueName: {asset1.UniqueName}");
        Console.WriteLine($"   Size: {asset1.Size}");
        Console.WriteLine($"   ContentType: {asset1.ContentType}");
        Console.WriteLine();

        // ----------------------------------------
        // 2. Tworzenie Asset z MemoryStream
        // ----------------------------------------
        Console.WriteLine("2. Tworzenie z MemoryStream:");
        
        using var memoryStream = new MemoryStream("PDF content simulation..."u8.ToArray());
        Asset asset2 = new Asset(memoryStream, "document.pdf");
        
        Console.WriteLine($"   Id: {asset2.Id}");
        Console.WriteLine($"   OriginalName: {asset2.OriginalName}");
        Console.WriteLine($"   UniqueName: {asset2.UniqueName}");
        Console.WriteLine($"   Extension: {asset2.Extension}");
        Console.WriteLine($"   ContentType: {asset2.ContentType}");
        Console.WriteLine($"   Size: {asset2.Size}");
        Console.WriteLine($"   Size.Kilobytes: {asset2.Size.Kilobytes:F2} KB");
        Console.WriteLine($"   CreatedAt: {asset2.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"   Hash (SHA256): {asset2.Hash}");
        Console.WriteLine($"   Md5: {asset2.Md5}");
        Console.WriteLine();

        // ----------------------------------------
        // 3. Tworzenie Asset z pliku (FileStream)
        // ----------------------------------------
        Console.WriteLine("3. Tworzenie z FileStream:");
        
        // Tworzymy tymczasowy plik do testów
        var tempFile = Path.GetTempFileName();
        var tempFileWithExt = Path.ChangeExtension(tempFile, ".txt");
        File.Move(tempFile, tempFileWithExt);
        File.WriteAllText(tempFileWithExt, "This is a test file for Asset demonstration.\nLine 2.\nLine 3.");
        
        try
        {
            using var fileStream = File.OpenRead(tempFileWithExt);
            Asset asset3 = new Asset(fileStream, Path.GetFileName(tempFileWithExt));
            
            Console.WriteLine($"   Id: {asset3.Id}");
            Console.WriteLine($"   OriginalName: {asset3.OriginalName}");
            Console.WriteLine($"   UniqueName: {asset3.UniqueName}");
            Console.WriteLine($"   Size: {asset3.Size}");
            Console.WriteLine($"   IsText: {asset3.IsText}");
            Console.WriteLine($"   Category: {asset3.Category}");
            Console.WriteLine($"   Content: {asset3.ToText()}");
        }
        finally
        {
            File.Delete(tempFileWithExt);
        }
        Console.WriteLine();

        // ----------------------------------------
        // 4. FileSize VO - jak TimeSpan dla plików
        // ----------------------------------------
        Console.WriteLine("4. FileSize VO (jak TimeSpan):");
        
        var size1 = FileSize.FromMegabytes(1.5);
        var size2 = FileSize.FromKilobytes(512);
        
        Console.WriteLine($"   1.5 MB = {size1.Bytes} bytes");
        Console.WriteLine($"   1.5 MB = {size1.Kilobytes:F2} KB");
        Console.WriteLine($"   1.5 MB formatted: {size1}");
        Console.WriteLine();
        Console.WriteLine($"   512 KB = {size2.Bytes} bytes");
        Console.WriteLine($"   512 KB = {size2.Megabytes:F4} MB");
        Console.WriteLine($"   512 KB formatted: {size2}");
        Console.WriteLine();
        
        // Arytmetyka
        var total = size1 + size2;
        Console.WriteLine($"   1.5 MB + 512 KB = {total}");
        
        // Porównanie
        Console.WriteLine($"   1.5 MB > 512 KB? {size1 > size2}");
        Console.WriteLine();

        // ----------------------------------------
        // 5. Implicit conversion TO Stream
        // ----------------------------------------
        Console.WriteLine("5. Implicit conversion TO Stream:");
        
        Asset asset4 = new Asset("SGVsbG8gV29ybGQh"u8.ToArray(), "hello.txt");
        
        // Implicit conversion do MemoryStream
        MemoryStream outputStream = asset4;
        Console.WriteLine($"   Asset -> MemoryStream.Length: {outputStream.Length}");
        
        // Implicit conversion do byte[]
        byte[] outputBytes = asset4;
        Console.WriteLine($"   Asset -> byte[].Length: {outputBytes.Length}");
        Console.WriteLine();

        // ----------------------------------------
        // 6. Type detection
        // ----------------------------------------
        Console.WriteLine("6. Type detection:");
        
        Asset imageAsset = new Asset(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "photo.jpg");  // JPEG magic bytes
        Asset pdfAsset = new Asset(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "doc.pdf");      // PDF magic bytes
        
        Console.WriteLine($"   photo.jpg - IsImage: {imageAsset.IsImage}, Category: {imageAsset.Category}");
        Console.WriteLine($"   doc.pdf - IsDocument: {pdfAsset.IsDocument}, Category: {pdfAsset.Category}");
        Console.WriteLine();

        // ----------------------------------------
        // 7. Nested types
        // ----------------------------------------
        Console.WriteLine("7. Nested types (FileName, MimeType):");
        
        var fileName = new Asset.FileName("report_final_v2.pdf");
        Console.WriteLine($"   FileName.Value: {fileName.Value}");
        Console.WriteLine($"   FileName.NameWithoutExtension: {fileName.NameWithoutExtension}");
        Console.WriteLine($"   FileName.Extension: {fileName.Extension}");
        Console.WriteLine($"   FileName.ExtensionWithoutDot: {fileName.ExtensionWithoutDot}");
        Console.WriteLine();
        
        var mimeType = Asset.MimeType.ImagePng;
        Console.WriteLine($"   MimeType.Value: {mimeType.Value}");
        Console.WriteLine($"   MimeType.Type: {mimeType.Type}");
        Console.WriteLine($"   MimeType.Subtype: {mimeType.Subtype}");
        Console.WriteLine($"   MimeType.Extension: {mimeType.Extension}");
        Console.WriteLine($"   MimeType.IsImage: {mimeType.IsImage}");
        Console.WriteLine();

        // ----------------------------------------
        // 8. Real-world scenario: File upload simulation
        // ----------------------------------------
        Console.WriteLine("8. Real-world scenario - File upload simulation:");
        
        // Symulacja uploadu pliku
        byte[] uploadedData = "User uploaded content here..."u8.ToArray();
        string uploadedFileName = "user_document.docx";
        
        Asset uploadedAsset = new Asset(uploadedData, uploadedFileName);
        
        Console.WriteLine($"   Received file: {uploadedAsset.OriginalName}");
        Console.WriteLine($"   Will be saved as: {uploadedAsset.UniqueName}");
        Console.WriteLine($"   Size: {uploadedAsset.Size}");
        Console.WriteLine($"   Hash for deduplication: {uploadedAsset.Hash[..20]}...");
        Console.WriteLine($"   Created at: {uploadedAsset.CreatedAt:O}");
        
        // Sprawdzenie limitu rozmiaru
        var maxSize = FileSize.FromMegabytes(10);
        if (uploadedAsset.Size > maxSize)
        {
            Console.WriteLine($"   ERROR: File too large! Max: {maxSize}");
        }
        else
        {
            Console.WriteLine($"   OK: File size within limit ({maxSize})");
        }
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
    }
}
