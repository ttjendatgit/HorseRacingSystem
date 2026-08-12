using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Rounds")]
public class Round
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    [Required]
    public int RoundNumber { get; set; }

    public DateTime ScheduledStartDate { get; set; }
    public DateTime ScheduledEndDate { get; set; }

    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<Race> Races { get; set; } = new List<Race>();
}
