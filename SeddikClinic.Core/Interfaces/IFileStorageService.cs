using SeddikClinic.Core.DTOs.Financial;

namespace SeddikClinic.Core.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResultDto> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "attachments");
    Task<Stream> DownloadFileAsync(string fileUrl);
    Task<bool> DeleteFileAsync(string fileUrl);
    Task<CloudStorageQuotaSummaryDto> GetStorageUsageSummaryAsync();
}

public interface IImageProcessingService
{
    Task<(byte[] ProcessedData, string ContentType)> CompressAndOptimizeImageAsync(Stream inputStream, string originalFileName, string contentType);
    Task<byte[]> GenerateThumbnailAsync(Stream inputStream, int width = 200, int height = 200);
}
