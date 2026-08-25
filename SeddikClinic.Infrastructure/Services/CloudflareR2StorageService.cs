using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Infrastructure.Services;

public class CloudflareR2StorageService : IFileStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudflareR2StorageService> _logger;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IAmazonS3? _s3Client;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;
    private readonly bool _useLocalFallback;
    private readonly string _localUploadsPath;

    public CloudflareR2StorageService(
        IConfiguration configuration,
        ILogger<CloudflareR2StorageService> logger,
        IImageProcessingService imageProcessingService)
    {
        _configuration = configuration;
        _logger = logger;
        _imageProcessingService = imageProcessingService;

        var serviceUrl = _configuration["CloudStorage:ServiceUrl"]; // e.g. https://<ACCOUNT_ID>.r2.cloudflarestorage.com
        var accessKey = _configuration["CloudStorage:AccessKeyId"];
        var secretKey = _configuration["CloudStorage:SecretAccessKey"];
        _bucketName = _configuration["CloudStorage:BucketName"] ?? "seddik-clinic-files";
        _publicBaseUrl = _configuration["CloudStorage:PublicBaseUrl"] ?? "";

        _localUploadsPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads");

        if (!string.IsNullOrEmpty(serviceUrl) && !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            };
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
            _useLocalFallback = false;
        }
        else
        {
            _useLocalFallback = true;
            if (!Directory.Exists(_localUploadsPath))
            {
                Directory.CreateDirectory(_localUploadsPath);
            }
            _logger.LogInformation("Cloud Storage credentials not configured. Using local fallback directory: {Path}", _localUploadsPath);
        }
    }

    public async Task<FileUploadResultDto> UploadFileAsync(
        Stream fileStream, 
        string fileName, 
        string contentType, 
        string folder = "attachments")
    {
        try
        {
            // معالجة وضغط الصورة قبل الرفع لتقليل استهلاك المساحة السحابية المجانية
            var (processedBytes, finalContentType) = await _imageProcessingService.CompressAndOptimizeImageAsync(
                fileStream, fileName, contentType);

            var extension = finalContentType == "image/webp" ? ".webp" : Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var objectKey = $"{folder}/{DateTime.UtcNow:yyyy/MM}/{uniqueFileName}";

            string fileUrl;
            string? thumbUrl = null;

            if (_useLocalFallback || _s3Client == null)
            {
                var folderPath = Path.Combine(_localUploadsPath, folder, DateTime.UtcNow.ToString("yyyy/MM"));
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                var localFilePath = Path.Combine(folderPath, uniqueFileName);
                await File.WriteAllBytesAsync(localFilePath, processedBytes);
                fileUrl = $"/uploads/{folder}/{DateTime.UtcNow:yyyy/MM}/{uniqueFileName}";
            }
            else
            {
                using var uploadStream = new MemoryStream(processedBytes);
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = uploadStream,
                    ContentType = finalContentType,
                    DisablePayloadSigning = true
                };

                await _s3Client.PutObjectAsync(putRequest);

                fileUrl = !string.IsNullOrEmpty(_publicBaseUrl)
                    ? $"{_publicBaseUrl.TrimEnd('/')}/{objectKey}"
                    : $"https://{_bucketName}.r2.cloudflarestorage.com/{objectKey}";
            }

            return new FileUploadResultDto
            {
                FileName = uniqueFileName,
                OriginalFileName = fileName,
                FileUrl = fileUrl,
                ThumbnailUrl = thumbUrl,
                ContentType = finalContentType,
                FileSizeBytes = processedBytes.Length,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to storage", fileName);
            return new FileUploadResultDto
            {
                OriginalFileName = fileName,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileUrl)
    {
        if (_useLocalFallback || _s3Client == null)
        {
            var relativePath = fileUrl.TrimStart('/');
            var fullPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
            if (File.Exists(fullPath))
            {
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }
            throw new FileNotFoundException("Local file not found", fullPath);
        }

        var uri = new Uri(fileUrl);
        var key = uri.AbsolutePath.TrimStart('/');

        var response = await _s3Client.GetObjectAsync(_bucketName, key);
        return response.ResponseStream;
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            if (_useLocalFallback || _s3Client == null)
            {
                var relativePath = fileUrl.TrimStart('/');
                var fullPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }

            var uri = new Uri(fileUrl);
            var key = uri.AbsolutePath.TrimStart('/');
            await _s3Client.DeleteObjectAsync(_bucketName, key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {Url}", fileUrl);
            return false;
        }
    }

    public async Task<CloudStorageQuotaSummaryDto> GetStorageUsageSummaryAsync()
    {
        long totalBytes = 0;

        if (_useLocalFallback || _s3Client == null)
        {
            if (Directory.Exists(_localUploadsPath))
            {
                var dirInfo = new DirectoryInfo(_localUploadsPath);
                totalBytes = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
        }
        else
        {
            try
            {
                var request = new ListObjectsV2Request { BucketName = _bucketName };
                ListObjectsV2Response response;
                do
                {
                    response = await _s3Client.ListObjectsV2Async(request);
                    totalBytes += response.S3Objects.Sum(o => o.Size);
                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch remote bucket size, using approximation");
            }
        }

        return new CloudStorageQuotaSummaryDto
        {
            TotalUsedBytes = totalBytes
        };
    }
}
