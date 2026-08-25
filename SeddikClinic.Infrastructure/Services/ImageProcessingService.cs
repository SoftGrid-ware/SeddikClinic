using SeddikClinic.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SeddikClinic.Infrastructure.Services;

public class ImageProcessingService : IImageProcessingService
{
    private const int MaxDimension = 2048; // الحد الأقصى للأبعاد
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 ميجابايت كحد أقصى للمرفق

    public async Task<(byte[] ProcessedData, string ContentType)> CompressAndOptimizeImageAsync(
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
            await inputStream.CopyToAsync(pdfMs);
            return (pdfMs.ToArray(), "application/pdf");
        }

        try
        {
            inputStream.Position = 0;
            using var image = await Image.LoadAsync(inputStream);

            // تصغير الأبعاد في حال كانت الصورة فائقة الضخامة
            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxDimension, MaxDimension),
                    Mode = ResizeMode.Max
                }));
            }

            using var outputStream = new MemoryStream();
            
            // ضغط بجودة ممتازة (82%) وتوفير كبير في المساحة
            var encoder = new WebpEncoder
            {
                Quality = 82
            };
            await image.SaveAsync(outputStream, encoder);

            return (outputStream.ToArray(), "image/webp");
        }
        catch
        {
            // في حال فشل قراءة الصورة كصورة قياسية، يتم إرجاع الملف كما هو
            inputStream.Position = 0;
            using var rawMs = new MemoryStream();
            await inputStream.CopyToAsync(rawMs);
            return (rawMs.ToArray(), contentType);
        }
    }

    public async Task<byte[]> GenerateThumbnailAsync(Stream inputStream, int width = 200, int height = 200)
    {
        try
        {
            inputStream.Position = 0;
            using var image = await Image.LoadAsync(inputStream);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop
            }));

            using var outputStream = new MemoryStream();
            var encoder = new WebpEncoder { Quality = 75 };
            await image.SaveAsync(outputStream, encoder);

            return outputStream.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
