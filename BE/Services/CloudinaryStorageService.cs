using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HorseRacing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HorseRacing.Services;

public class CloudinaryOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string UploadPreset { get; set; } = "";
}

// COMPLAINT-EVIDENCE-V1.1: UploadMediaAsync used to return just the URL, which was not enough to
// reliably delete the remote asset later — Cloudinary's DeleteResourcesAsync needs both the
// public_id and the correct resource_type (image vs video), and parsing them back out of a
// secure_url is fragile (folder/versioning segments vary). Capturing them at upload time instead.
public record MediaUploadResult(string Url, string PublicId, string ResourceType, long FileSizeBytes);

// COMPLAINT-EVIDENCE-V1.1: single source of truth for which evidence files are accepted — pure and
// network-free so it can be exercised directly in tests, unlike the upload calls around it which
// need live Cloudinary access to fully succeed.
public static class ComplaintEvidenceValidator
{
    public static readonly string[] ImageMediaTypes = { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
    public static readonly string[] VideoMediaTypes = { "video/mp4", "video/quicktime", "video/webm", "video/x-msvideo", "video/3gpp" };
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const long MaxVideoBytes = 50 * 1024 * 1024;

    // Throws ArgumentException (unsupported type, or oversized for its type) on rejection;
    // otherwise returns the classified media type.
    public static ComplaintEvidenceMediaType Validate(string? contentType, long length)
    {
        var ct = (contentType ?? "").ToLowerInvariant();
        var isImage = Array.IndexOf(ImageMediaTypes, ct) >= 0;
        var isVideo = Array.IndexOf(VideoMediaTypes, ct) >= 0;
        if (!isImage && !isVideo)
            throw new ArgumentException($"Unsupported evidence file type: {contentType}");

        var maxBytes = isVideo ? MaxVideoBytes : MaxImageBytes;
        if (length > maxBytes)
            throw new ArgumentException(isVideo ? "Video quá lớn (tối đa 50MB)." : "Ảnh quá lớn (tối đa 10MB).");

        return isVideo ? ComplaintEvidenceMediaType.Video : ComplaintEvidenceMediaType.Image;
    }
}

public interface ICloudStorageService
{
    Task<string> UploadAsync(IFormFile file, string folder = "general");
    // COMPLAINT-EVIDENCE-V1: images + video, kept separate from UploadAsync above so existing
    // callers (Tournament cover images, Jockey license documents) keep their exact current
    // validation/behavior untouched.
    Task<MediaUploadResult> UploadMediaAsync(IFormFile file, string folder = "general");
    Task<bool> DeleteAsync(string publicId, string resourceType = "image");
}

public class CloudinaryStorageService : ICloudStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly string? _uploadPreset;
    private readonly string _cloudName;
    private readonly ILogger<CloudinaryStorageService> _logger;
    private static readonly HttpClient _http = new();

    public CloudinaryStorageService(IOptions<CloudinaryOptions> options, ILogger<CloudinaryStorageService> logger)
    {
        _logger = logger;
        var opts = options.Value;
        var acc = new Account(opts.CloudName, opts.ApiKey, opts.ApiSecret);
        _cloudinary = new Cloudinary(acc);
        _cloudinary.Api.Secure = true;
        _cloudName = opts.CloudName;

        _uploadPreset = string.IsNullOrWhiteSpace(opts.UploadPreset) ? null : opts.UploadPreset;
        if (_uploadPreset == null)
            _uploadPreset = Environment.GetEnvironmentVariable("Cloudinary__UploadPreset")?.Trim();

        _logger.LogInformation("Cloudinary upload mode: {Mode}", _uploadPreset != null ? "unsigned" : "signed");
    }

    public async Task<string> UploadAsync(IFormFile file, string folder = "general")
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty.");

        var validTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "application/pdf" };
        if (Array.IndexOf(validTypes, file.ContentType.ToLower()) < 0)
            throw new ArgumentException($"Unsupported file type: {file.ContentType}");

        if (_uploadPreset != null)
            return await UnsignedUploadAsync(file, folder);

        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"racemaster/{folder}",
            Transformation = new Transformation().Quality("auto").FetchFormat("auto"),
            Overwrite = true,
        };
        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
        {
            _logger.LogError("Cloudinary upload failed: {Error}", result.Error.Message);
            throw new Exception($"Upload failed: {result.Error.Message}");
        }
        return result.SecureUrl?.AbsoluteUri ?? result.Url?.AbsoluteUri ?? "";
    }

    private async Task<string> UnsignedUploadAsync(IFormFile file, string folder)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        using var content = new MultipartFormDataContent();

        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.TryAddWithoutValidation("Content-Type", file.ContentType);
        content.Add(filePart, "file", file.FileName);

        var presetPart = new StringContent(_uploadPreset!, Encoding.UTF8, "text/plain");
        content.Add(presetPart, "upload_preset");

        _logger.LogInformation("Cloudinary unsigned upload: preset='{Preset}', file='{File}', size={Size}",
            _uploadPreset, file.FileName, fileBytes.Length);

        var response = await _http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cloudinary unsigned upload failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new Exception($"Upload failed: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.GetProperty("secure_url").GetString()
               ?? doc.RootElement.GetProperty("url").GetString()
               ?? "";
        _logger.LogInformation("Uploaded to Cloudinary (unsigned): {Url}", url);
        return url;
    }

    public async Task<MediaUploadResult> UploadMediaAsync(IFormFile file, string folder = "general")
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty.");

        // COMPLAINT-EVIDENCE-V1.1: extracted into ComplaintEvidenceValidator so the MIME/size rules
        // have exactly one implementation — reusable (and unit-testable without any network call)
        // instead of duplicated between this class and its unsigned-upload sibling.
        var isVideo = ComplaintEvidenceValidator.Validate(file.ContentType, file.Length) == ComplaintEvidenceMediaType.Video;

        if (_uploadPreset != null)
            return await UnsignedMediaUploadAsync(file);

        await using var stream = file.OpenReadStream();
        if (isVideo)
        {
            var videoParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"racemaster/{folder}",
                Overwrite = true,
            };
            var videoResult = await _cloudinary.UploadAsync(videoParams);
            if (videoResult.Error != null)
            {
                _logger.LogError("Cloudinary video upload failed: {Error}", videoResult.Error.Message);
                throw new Exception($"Upload failed: {videoResult.Error.Message}");
            }
            var videoUrl = videoResult.SecureUrl?.AbsoluteUri ?? videoResult.Url?.AbsoluteUri ?? "";
            return new MediaUploadResult(videoUrl, videoResult.PublicId ?? "", "video", videoResult.Bytes > 0 ? videoResult.Bytes : file.Length);
        }

        var imageParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"racemaster/{folder}",
            Transformation = new Transformation().Quality("auto").FetchFormat("auto"),
            Overwrite = true,
        };
        var result = await _cloudinary.UploadAsync(imageParams);
        if (result.Error != null)
        {
            _logger.LogError("Cloudinary image upload failed: {Error}", result.Error.Message);
            throw new Exception($"Upload failed: {result.Error.Message}");
        }
        var imageUrl = result.SecureUrl?.AbsoluteUri ?? result.Url?.AbsoluteUri ?? "";
        return new MediaUploadResult(imageUrl, result.PublicId ?? "", "image", result.Bytes > 0 ? result.Bytes : file.Length);
    }

    // Cloudinary's /auto/upload endpoint detects image vs video resource_type server-side, so the
    // unsigned (upload-preset) path never needs to branch the way the signed SDK calls above do.
    private async Task<MediaUploadResult> UnsignedMediaUploadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.TryAddWithoutValidation("Content-Type", file.ContentType);
        content.Add(filePart, "file", file.FileName);
        content.Add(new StringContent(_uploadPreset!, Encoding.UTF8, "text/plain"), "upload_preset");

        var response = await _http.PostAsync($"https://api.cloudinary.com/v1_1/{_cloudName}/auto/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cloudinary unsigned media upload failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new Exception($"Upload failed: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.GetProperty("secure_url").GetString()
            ?? doc.RootElement.GetProperty("url").GetString()
            ?? "";
        var publicId = doc.RootElement.TryGetProperty("public_id", out var pidEl) ? pidEl.GetString() ?? "" : "";
        var resourceType = doc.RootElement.TryGetProperty("resource_type", out var rtEl) ? rtEl.GetString() ?? "image" : "image";
        var bytes = doc.RootElement.TryGetProperty("bytes", out var bytesEl) ? bytesEl.GetInt64() : file.Length;
        return new MediaUploadResult(url, publicId, resourceType, bytes);
    }

    public async Task<bool> DeleteAsync(string publicId, string resourceType = "image")
    {
        try
        {
            var type = string.Equals(resourceType, "video", StringComparison.OrdinalIgnoreCase)
                ? ResourceType.Video
                : ResourceType.Image;
            var result = await _cloudinary.DeleteResourcesAsync(type, publicId);
            return result.Deleted?.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cloudinary delete failed: {Error}", ex.Message);
            return false;
        }
    }
}
