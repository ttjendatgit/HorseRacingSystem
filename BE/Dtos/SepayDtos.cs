using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HorseRacing.Dtos;

/// <summary>
/// DTO chứa cấu trúc dữ liệu phản hồi Webhook tự động gửi từ hệ thống cổng thanh toán SePay.
/// </summary>
public class SepayWebhookRequest
{
    /// <summary>Mã ID duy nhất của giao dịch ngân hàng từ SePay — dùng kiểm tra trùng lặp (Idempotency).</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Tên ngân hàng xử lý giao dịch (Ví dụ: MBBank, Vietcombank, TPBank).</summary>
    [JsonPropertyName("gateway")]
    [MaxLength(50)]
    public string? Gateway { get; set; }

    /// <summary>Thời gian ghi nhận giao dịch tại ngân hàng.</summary>
    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    /// <summary>Số tài khoản người gửi tiền.</summary>
    [JsonPropertyName("accountNumber")]
    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    /// <summary>Mã tài khoản phụ / danh ảo (nếu có).</summary>
    [JsonPropertyName("subAccount")]
    [MaxLength(50)]
    public string? SubAccount { get; set; }

    /// <summary>Mã Code giao dịch được ngân hàng cấp.</summary>
    [JsonPropertyName("code")]
    [MaxLength(100)]
    public string? Code { get; set; }

    /// <summary>Nội dung chuyển khoản thực tế — chứa mã tham chiếu nạp tiền (Reference) của người dùng.</summary>
    [JsonPropertyName("content")]
    [MaxLength(500)]
    public string? Content { get; set; }

    /// <summary>Loại biến động số dư: in (Nạp tiền vào) / out (Rút tiền ra).</summary>
    [JsonPropertyName("transferType")]
    [MaxLength(10)]
    public string? TransferType { get; set; }

    /// <summary>Mô tả chi tiết nội dung giao dịch từ biến động ngân hàng.</summary>
    [JsonPropertyName("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Số tiền thực tế được chuyển khoản vào tài khoản (VNĐ).</summary>
    [JsonPropertyName("transferAmount")]
    public decimal? TransferAmount { get; set; }

    /// <summary>Số dư tài khoản ngân hàng sau khi giao dịch hoàn tất.</summary>
    [JsonPropertyName("accumulated")]
    public decimal? Accumulated { get; set; }

    /// <summary>Mã tham chiếu đối soát duy nhất của hệ thống ngân hàng (FT/Ref No).</summary>
    [JsonPropertyName("referenceCode")]
    [MaxLength(100)]
    public string? ReferenceCode { get; set; }
}

/// <summary>
/// DTO chứa yêu cầu khởi tạo mã nạp tiền QR SePay từ người dùng.
/// </summary>
public class DepositRequest
{
    /// <summary>Số tiền người dùng muốn nạp vào ví (VNĐ).</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// DTO mô tả thông tin chi tiết một lượt nạp tiền trong lịch sử nạp ví.
/// </summary>
public class DepositHistoryItem
{
    /// <summary>Mã GUID định danh của đơn nạp tiền.</summary>
    public Guid Id { get; set; }

    /// <summary>Số tiền nạp (VNĐ).</summary>
    public decimal Amount { get; set; }

    /// <summary>Trạng thái đơn nạp: pending (Đang chờ), completed (Hoàn thành), cancelled (Đã hủy).</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Mã nội dung chuyển khoản duy nhất để người dùng điền khi quét QR.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Ghi chú mô tả đơn nạp tiền.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Thời điểm tạo đơn nạp tiền.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm SePay xác nhận đã nhận tiền thành công.</summary>
    public DateTime? CompletedAt { get; set; }
}
