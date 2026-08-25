using SeddikClinic.Core.Interfaces;
using SkiaSharp;

namespace SeddikClinic.Infrastructure.Services;

public class ImageProcessingService : IImageProcessingService
{
    private const int MaxDimension = 2048; // الحد الأقصى للأبعاد
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 ميجابايت كحد أقصى للمرفق

    public Task<(byte[] ProcessedData, string ContentType)> CompressAndOptimizeImageAsync(
        Stream inputStream, 
        string originalFileName, 
        string contentType)
    {
        if (inputStream.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("حجم الملف يتجاوز الحد الأقصى المسموح به (10 ميجابايت).");
        }

        // إذا كان الملف PDF أو مستند غير صوري، يُترك كما هو
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            originalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var pdfMs = new MemoryStream();
            inputStream.CopyTo(pdfMs);
            return Task.FromResult((pdfMs.ToArray(), "application/pdf"));
        }

        try
        {
            inputStream.Position = 0;
            using var originalBitmap = SKBitmap.Decode(inputStream);
            if (originalBitmap == null)
            {
                inputStream.Position = 0;
                using var rawMs = new MemoryStream();
                inputStream.CopyTo(rawMs);
                return Task.FromResult((rawMs.ToArray(), contentType));
            }

            int targetWidth = originalBitmap.Width;
            int targetHeight = originalBitmap.Height;

            // تصغير الأبعاد في حال كانت الصورة فائقة الضخامة
            if (targetWidth > MaxDimension || targetHeight > MaxDimension)
            {
                float ratio = Math.Min((float)MaxDimension / targetWidth, (float)MaxDimension / targetHeight);
                targetWidth = (int)(targetWidth * ratio);
                targetHeight = (int)(targetHeight * ratio);
            }

            using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
            using var image = SKImage.FromBitmap(resizedBitmap ?? originalBitmap);
            
            // ضغط بجودة ممتازة (80%) وتوفير كبير في المساحة
            using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            return Task.FromResult((encodedData.ToArray(), "image/jpeg"));
        }
        catch
        {
            inputStream.Position = 0;
            using var rawMs = new MemoryStream();
            inputStream.CopyTo(rawMs);
            return Task.FromResult((rawMs.ToArray(), contentType));
        }
    }

    public Task<byte[]> GenerateThumbnailAsync(Stream inputStream, int width = 200, int height = 200)
    {
        try
        {
            inputStream.Position = 0;
            using var originalBitmap = SKBitmap.Decode(inputStream);
            if (originalBitmap == null) return Task.FromResult(Array.Empty<byte>());

            using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(width, height), SKFilterQuality.Low);
            using var image = SKImage.FromBitmap(resizedBitmap ?? originalBitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, 70);

            return Task.FromResult(encodedData.ToArray());
        }
        catch
        {
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
