using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Races")]
public class Race
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public Guid? RoundId { get; set; }

    public Round? Round { get; set; }

    public DateTime ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    public RaceStatus Status { get; set; }

    public Guid? TrackId { get; set; }

    public Track? Track { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? RoundNames { get; set; }

    public int MaxParticipants { get; set; } = 12;

    public int Distance { get; set; } = 2000; // meters

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RaceEntry> Entries { get; set; } = new List<RaceEntry>();
    public RaceResult? Result { get; set; }
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<RefereeAssignment> RefereeAssignments { get; set; } = new List<RefereeAssignment>();
    public ICollection<HorseHealthCheck> HealthChecks { get; set; } = new List<HorseHealthCheck>();
    public ICollection<ViolationRecord> Violations { get; set; } = new List<ViolationRecord>();
    public ICollection<RaceReport> Reports { get; set; } = new List<RaceReport>();
}
