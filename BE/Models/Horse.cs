using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Horses")]
public class Horse
{
    [Key]
    [Column("HorseID")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal? Weight { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? Height { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    public int TotalRaces { get; set; } = 0;

    public int TotalWins { get; set; } = 0;

    [MaxLength(2000)]
    public string? ImageUrl { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    [MaxLength(1000)]
    public string? ApprovalNote { get; set; }

    public Owner? Owner { get; set; }

    public ICollection<RaceEntry> RaceEntries { get; set; } = new List<RaceEntry>();
    public ICollection<JockeyInvitation> JockeyInvitations { get; set; } = new List<JockeyInvitation>();
}
