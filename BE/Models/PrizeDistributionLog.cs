using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

/// <summary>
/// PRIZE-V2 (Phase 4): append-only audit trail of successful PrizeService.DistributeAsync payouts —
/// one row per Prize actually credited to an Owner's wallet. Never written for a Skipped/Errors
/// outcome. This is the source of truth for "Owner xem lịch sử nhận thưởng" (WalletController's
/// my-prize-history), since Prize itself carries no OwnerId to query back from.
/// </summary>
[Table("PrizeDistributionLogs")]
public class PrizeDistributionLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid PrizeId { get; set; }

    public Prize? Prize { get; set; }

    [Required]
    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public int Position { get; set; }

    /// <summary>Owner.Id (Owners table PK) — display/report identity.</summary>
    [Required]
    public Guid OwnerId { get; set; }

    /// <summary>Owner's User.Id — the wallet identity actually credited, and what
    /// WalletController.GetMyPrizeHistory filters on against the caller's own JWT.</summary>
    [Required]
    public Guid OwnerUserId { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    [MaxLength(200)]
    public string HorseName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "VND";

    public DateTime DistributedAt { get; set; }
}
