using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("WithdrawalRequests")]
public class WithdrawalRequest
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public Guid BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, completed, rejected

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public Guid? ProcessedByUserId { get; set; }

    public User? ProcessedByUser { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
