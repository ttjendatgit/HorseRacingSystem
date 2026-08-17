using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("RaceResults")]
public class RaceResult
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid WinningHorseId { get; set; }

    public Horse? WinningHorse { get; set; }

    public int TotalParticipants { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? WinnerFinishTime { get; set; } // seconds

    public DateTime RecordedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Authoritative result lifecycle (Phase2B). Business logic must read/write
    /// this field, not ApprovalStatus/IsOfficial below.
    /// </summary>
    public RaceResultStatus Status { get; set; } = RaceResultStatus.Provisional;

    [MaxLength(1000)]
    public string? RejectedReason { get; set; }

    public bool IsDisputed { get; set; } = false;

    /// <summary>
    /// Legacy pre-Phase2B field, retained for rollback safety only. No longer
    /// written by normal application flow — use Status == Official instead.
    /// Candidate for removal once Phase2 cleanup confirms no reader depends on it.
    /// </summary>
    [Obsolete("Superseded by Status. Kept for rollback safety only; do not write in new code.")]
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Legacy pre-Phase2B field, retained for rollback safety only. No longer
    /// written by normal application flow — use Status == Official instead.
    /// Candidate for removal once Phase2 cleanup confirms no reader depends on it.
    /// </summary>
    [Obsolete("Superseded by Status == RaceResultStatus.Official. Kept for rollback safety only; do not write in new code.")]
    public bool IsOfficial { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WinnerPurse { get; set; }

    [MaxLength(4000)]
    public string? RankingsJson { get; set; } // JSON: [{position, horseId, horseName, jockeyName, time, margin}]

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
