using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Contracts")]
public class Contract
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public Owner? Owner { get; set; }

    [Required]
    public Guid JockeyId { get; set; }

    public Jockey? Jockey { get; set; }

    public Guid? HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BaseFee { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? WinBonusPercent { get; set; } // e.g. 10 = 10% of winnings

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PerRaceFee { get; set; }

    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }

    [MaxLength(500)]
    public string? TerminationReason { get; set; }

    public DateTime? SignedByOwnerAt { get; set; }

    public DateTime? SignedByJockeyAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
