using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("InjuryRecords")]
public class InjuryRecord
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    public InjurySeverity Severity { get; set; } = InjurySeverity.Minor;

    public InjuryStatus Status { get; set; } = InjuryStatus.Active;

    [Required]
    [MaxLength(200)]
    public string InjuryType { get; set; } = string.Empty; // Fracture, Tendon, Respiratory, etc.

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BodyPart { get; set; } // Left Foreleg, Right Hind, etc.

    [MaxLength(1000)]
    public string? Treatment { get; set; }

    [MaxLength(500)]
    public string? Medication { get; set; }

    [MaxLength(200)]
    public string? VeterinarianName { get; set; }

    public DateTime DiagnosedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpectedRecoveryDate { get; set; }

    public DateTime? RecoveredAt { get; set; }

    public bool RequiresSurgery { get; set; } = false;

    public bool ClearedToRace { get; set; } = false;

    public DateTime? ClearedAt { get; set; }

    public Guid? ReportedByUserId { get; set; }

    public User? ReportedByUser { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
