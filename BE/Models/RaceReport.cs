using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("RaceReports")]
public class RaceReport
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid RefereeId { get; set; }

    public Referee? Referee { get; set; }

    public DateTime CompletedAt { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Details { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Incidents { get; set; }

    [MaxLength(1000)]
    public string? RecommendedActions { get; set; }

    public bool IsOfficialReport { get; set; }

    [MaxLength(100)]
    public string? WeatherCondition { get; set; }

    [MaxLength(100)]
    public string? TrackCondition { get; set; } // Fast, Good, Muddy, etc.

    public int? Attendance { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
