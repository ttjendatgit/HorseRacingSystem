using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("RefereeAssignments")]
public class RefereeAssignment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid RefereeId { get; set; }

    public Referee? Referee { get; set; }

    [Required]
    [MaxLength(100)]
    public string Role { get; set; } = string.Empty; // Chief Referee, Assistant, etc.

    public RefereeAssignmentStatus Status { get; set; }

    public DateTime AssignedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
