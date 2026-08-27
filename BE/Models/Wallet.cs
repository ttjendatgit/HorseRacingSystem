using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

/// <summary>
/// Thực thể đại diện cho ví điểm giao dịch của người dùng trong hệ thống.
/// </summary>
[Table("Wallets")]
public class Wallet
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
