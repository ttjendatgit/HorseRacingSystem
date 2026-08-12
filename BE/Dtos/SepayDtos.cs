using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HorseRacing.Dtos;

public class SepayWebhookRequest
{
    /// <summary>Sepay transaction ID — dùng để idempotency.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Ngân hàng gửi (gateway).</summary>
    [JsonPropertyName("gateway")]
    [MaxLength(50)]
    public string? Gateway { get; set; }

    /// <summary>Ngày giao dịch từ ngân hàng.</summary>
    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    /// <summary>Số tài khoản người gửi.</summary>
    [JsonPropertyName("accountNumber")]
    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    /// <summary>Tài khoản phụ (nếu có).</summary>
    [JsonPropertyName("subAccount")]
    [MaxLength(50)]
    public string? SubAccount { get; set; }

    /// <summary>Mã code từ ngân hàng.</summary>
    [JsonPropertyName("code")]
    [MaxLength(100)]
    public string? Code { get; set; }

    /// <summary>Nội dung chuyển khoản — chứa mã reference của chúng ta.</summary>
    [JsonPropertyName("content")]
    [MaxLength(500)]
    public string? Content { get; set; }

    /// <summary>Loại giao dịch: in / out.</summary>
    [JsonPropertyName("transferType")]
    [MaxLength(10)]
    public string? TransferType { get; set; }

    /// <summary>Mô tả giao dịch.</summary>
    [JsonPropertyName("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Số tiền chuyển.</summary>
    [JsonPropertyName("transferAmount")]
    public decimal? TransferAmount { get; set; }

    /// <summary>Số dư tích lũy.</summary>
    [JsonPropertyName("accumulated")]
    public decimal? Accumulated { get; set; }

    /// <summary>Mã tham chiếu ngân hàng.</summary>
    [JsonPropertyName("referenceCode")]
    [MaxLength(100)]
    public string? ReferenceCode { get; set; }
}

public class DepositRequest
{
    public decimal Amount { get; set; }
}

public class DepositHistoryItem
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
