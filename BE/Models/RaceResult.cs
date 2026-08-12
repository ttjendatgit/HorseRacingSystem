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

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    [MaxLength(1000)]
    public string? RejectedReason { get; set; }

    public bool IsDisputed { get; set; } = false;

    public bool IsOfficial { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WinnerPurse { get; set; }

    [MaxLength(4000)]
    public string? RankingsJson { get; set; } // JSON: [{position, horseId, horseName, jockeyName, time, margin}]

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
