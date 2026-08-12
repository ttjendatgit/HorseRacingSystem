using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("RaceEntries")]
public class RaceEntry
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    public Guid? JockeyId { get; set; }

    public Jockey? Jockey { get; set; }

    public RegistrationStatus Status { get; set; }

    public bool OwnerConfirmed { get; set; }

    public bool JockeyConfirmed { get; set; }

    public int? GateNumber { get; set; }

    public int? FinishPosition { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? FinishTime { get; set; } // seconds

    public DateTime? ScratchedAt { get; set; }

    [MaxLength(500)]
    public string? ScratchReason { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WeightCarried { get; set; } // kg

    [MaxLength(50)]
    public string? Equipment { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Odds { get; set; } = 1.0m;
}
