using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Transactions")]
public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(50)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    /// <summary>Sepay transaction ID — dùng chống xử lý trùng lặp (idempotency).</summary>
    public long? SepayTransactionId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
