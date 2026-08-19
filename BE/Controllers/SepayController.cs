using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/sepay")]
public class SepayController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly IConfiguration _config;
    private readonly ILogger<SepayController> _logger;

    private const string SignatureHeader = "X-SePay-Signature";
    private const string TimestampHeader = "X-SePay-Timestamp";

    public SepayController(
        ITransactionService transactionService,
        IConfiguration config,
        ILogger<SepayController> logger)
    {
        _transactionService = transactionService;
        _config = config;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("deposit")]
    public async Task<ActionResult> CreateDeposit([FromBody] DepositRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _transactionService.CreatePendingAsync(userId, request.Amount);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpGet("check")]
    public async Task<ActionResult> CheckTransaction([FromQuery] Guid transactionId)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();

        var result = await _transactionService.CheckTransactionAsync(userId, transactionId);
        if (result.StatusCode == 404) return NotFound();
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<ActionResult> GetHistory()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _transactionService.GetHistoryAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("webhook/test")]
    [Authorize(Roles = "Admin")]
    public ActionResult WebhookTest()
    {
        var apiKey = _config["Sepay:ApiKey"];
        return Ok(new
        {
            status = "ok",
            configured = !string.IsNullOrEmpty(apiKey),
            apiKeyMasked = string.IsNullOrEmpty(apiKey) ? "NOT SET" : apiKey[..Math.Min(8, apiKey.Length)] + "...",
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }

    [HttpPost("webhook")]
    public async Task<ActionResult> Webhook()
    {
        _logger.LogInformation("Sepay webhook received from {IP}", HttpContext.Connection.RemoteIpAddress);

        // ── Read raw body ──
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        _logger.LogInformation("Sepay webhook headers: Auth={Auth}, Signature={Sig}, Timestamp={Ts}",
            Request.Headers["Authorization"].ToString(),
            Request.Headers[SignatureHeader].ToString(),
            Request.Headers[TimestampHeader].ToString());
        _logger.LogInformation("Sepay webhook body: {Body}", rawBody[..Math.Min(rawBody.Length, 500)]);

        // ── Verify request authenticity ──
        var apiKey = _config["Sepay:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Sepay:ApiKey not configured — webhook verification failed");
            return StatusCode(500, new { message = "Webhook chưa được cấu hình" });
        }

        if (!VerifyRequest(rawBody, apiKey))
        {
            _logger.LogWarning("Sepay webhook verification failed");
            return Unauthorized(new { message = "Xác minh thất bại" });
        }

        // ── Parse JSON ──
        SepayWebhookRequest request;
        try
        {
            request = JsonSerializer.Deserialize<SepayWebhookRequest>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Sepay webhook body");
            return BadRequest(new { message = "JSON không hợp lệ" });
        }

        if (request == null)
        {
            return BadRequest(new { message = "Nội dung rỗng" });
        }

        var result = await _transactionService.HandleWebhookAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Hỗ trợ cả 2 phương thức: API Key và HMAC-SHA256.
    /// Tự động phát hiện dựa trên header gửi đến.
    /// </summary>
    private bool VerifyRequest(string rawBody, string secretKey)
    {
        // ── Method 1: API Key — Authorization: Apikey <key> ──
        var authHeader = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            var key = authHeader[7..]; // Remove "Apikey "
            return string.Equals(key, secretKey, StringComparison.Ordinal);
        }

        // ── Method 2: HMAC-SHA256 — X-SePay-Signature + X-SePay-Timestamp ──
        var sigHeader = Request.Headers[SignatureHeader].ToString();
        var tsHeader = Request.Headers[TimestampHeader].ToString();

        if (!string.IsNullOrEmpty(sigHeader) && !string.IsNullOrEmpty(tsHeader))
        {
            return VerifyHmacSha256(rawBody, secretKey, sigHeader, tsHeader);
        }

        _logger.LogWarning("No recognizable auth header found");
        return false;
    }

    private bool VerifyHmacSha256(string rawBody, string secretKey, string signatureHeader, string timestampHeader)
    {
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var actualSignature = signatureHeader[7..];

        // Anti-replay: reject if timestamp > 5 minutes old
        if (!long.TryParse(timestampHeader, out var timestampSeconds))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
        {
            _logger.LogWarning("Sepay webhook timestamp expired: {Timestamp}", requestTime);
            return false;
        }

        var payload = $"{timestampHeader}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var expectedHash = HMACSHA256.HashData(keyBytes, payloadBytes);
        var expectedSignature = Convert.ToHexString(expectedHash).ToLowerInvariant();

        return string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase);
    }
}
