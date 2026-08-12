using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Jockeys")]
public class Jockey
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(100)]
    public string? LicenseNumber { get; set; }

    [MaxLength(500)]
    public string? LicenseFile { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? IdCardNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? Nationality { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? Height { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal? Weight { get; set; }

    public int ExperienceYears { get; set; } = 0;

    public int TotalRaces { get; set; } = 0;

    public int TotalWins { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal WinRate { get; set; } = 0;

    public int? Rank { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Đang hoạt động";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    [MaxLength(1000)]
    public string? ApprovalNote { get; set; }

    public ICollection<RaceEntry> RaceEntries { get; set; } = new List<RaceEntry>();
    public ICollection<JockeyInvitation> Invitations { get; set; } = new List<JockeyInvitation>();
}
