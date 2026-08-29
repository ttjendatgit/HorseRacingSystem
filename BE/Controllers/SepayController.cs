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

    /// <summary>
    /// Khởi tạo một giao dịch nạp tiền chờ xử lý (Pending) cho người dùng đã đăng nhập với mã QR SePay.
    /// </summary>
    /// <param name="request">Yêu cầu nạp tiền chứa số tiền cần nạp.</param>
    /// <returns>Thông tin tham chiếu giao dịch nạp tiền kèm nội dung chuyển khoản QR SePay.</returns>
    [Authorize]
    [HttpPost("deposit")]
    public async Task<ActionResult> CreateDeposit([FromBody] DepositRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _transactionService.CreatePendingAsync(userId, request.Amount);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Kiểm tra trạng thái xử lý tức thời của một đơn nạp tiền (Đang chờ, Hoàn thành, Đã hủy).
    /// </summary>
    /// <param name="transactionId">Mã GUID định danh của giao dịch nạp tiền.</param>
    /// <returns>Dữ liệu trạng thái giao dịch hiện tại.</returns>
    [Authorize]
    [HttpGet("check")]
    public async Task<ActionResult> CheckTransaction([FromQuery] Guid transactionId)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
<<<<<<< HEAD
        var result = await _transactionService.CheckTransactionAsync(userId, transactionId);
=======

        var result = await _transactionService.CheckTransactionAsync(userId, transactionId);
        if (result.StatusCode == 404) return NotFound();
>>>>>>> origin/huyhoang
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách biến động dư tài khoản ví và lịch sử nạp tiền của tài khoản đang đăng nhập.
    /// </summary>
    /// <returns>Danh sách lịch sử các giao dịch nạp/rút/cược của ví.</returns>
    [Authorize]
    [HttpGet("history")]
    public async Task<ActionResult> GetHistory()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _transactionService.GetHistoryAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Endpoint kiểm tra chẩn đoán kết nối và cấu hình API Key cổng thanh toán SePay dành cho Admin.
    /// </summary>
    /// <returns>Đối tượng kết quả trạng thái cấu hình.</returns>
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

    /// <summary>
    /// Endpoint nhận tín hiệu Webhook tự động từ cổng SePay khi có biến động tài khoản ngân hàng.
    /// Thực hiện xác thực chữ ký bảo mật HMAC-SHA256/API Key và tự động cộng dư tiền ví cho người dùng.
    /// </summary>
    /// <returns>Mã trạng thái HTTP phản hồi cho cổng SePay.</returns>
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
