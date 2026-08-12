using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
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

public interface ICloudStorageService
{
    Task<string> UploadAsync(IFormFile file, string folder = "general");
    Task<bool> DeleteAsync(string publicId);
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

    public async Task<bool> DeleteAsync(string publicId)
    {
        try
        {
            var result = await _cloudinary.DeleteResourcesAsync(publicId);
            return result.Deleted?.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cloudinary delete failed: {Error}", ex.Message);
            return false;
        }
    }
}
