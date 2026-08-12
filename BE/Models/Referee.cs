using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Referees")]
public class Referee
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Certifications { get; set; }

    public DateTime LicenseExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(3,2)")]
    public decimal Rating { get; set; } = 0; // 0.00 - 5.00

    public int TotalOfficiated { get; set; } = 0;

    [MaxLength(200)]
    public string? Specialization { get; set; } // Chief, Veterinary, Equipment, etc.

    [MaxLength(100)]
    public string? Nationality { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RefereeAssignment> Assignments { get; set; } = new List<RefereeAssignment>();
}
