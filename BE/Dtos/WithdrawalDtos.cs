using System;
using System.ComponentModel.DataAnnotations;

namespace HorseRacing.Dtos;

public class BankAccountRequest
{
    [Required]
    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AccountHolder { get; set; } = string.Empty;
}

public class WithdrawalRequestDto
{
    [Required]
    public Guid BankAccountId { get; set; }

    // Amount tính bằng điểm (1.000đ = 1 điểm)
    [Required]
    [Range(1, 100_000_000)]
    public decimal Amount { get; set; }
}

public class AdminProcessWithdrawalRequest
{
    [Required]
    public Guid WithdrawalId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "completed"; // completed or rejected

    public string? Note { get; set; }
}
