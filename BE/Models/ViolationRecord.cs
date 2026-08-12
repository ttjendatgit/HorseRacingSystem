using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("ViolationRecords")]
public class ViolationRecord
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid RaceEntryId { get; set; }

    public RaceEntry? RaceEntry { get; set; }

    [Required]
    public Guid RefereeId { get; set; }

    public Referee? Referee { get; set; }

    [Required]
    public ViolationType ViolationType { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; }

    [MaxLength(1000)]
    public string? Evidence { get; set; }

    [MaxLength(500)]
    public string? Penalty { get; set; }
}
