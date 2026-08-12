using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("HorseHealthChecks")]
public class HorseHealthCheck
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid RefereeId { get; set; }

    public Referee? Referee { get; set; }

    public HealthCheckStatus Status { get; set; }

    public DateTime CheckedAt { get; set; }

    [MaxLength(1000)]
    public string? Observations { get; set; }

    [MaxLength(500)]
    public string? Verdict { get; set; }

    public bool ApprovedToRace { get; set; }
}
