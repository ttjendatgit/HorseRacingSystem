using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("UserRegistrations")]
public class UserRegistration
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public UserRole RequestedRole { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? DocumentUrl { get; set; }

    public RegistrationStatus Status { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public User? ReviewedByUser { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public Guid? ApprovedUserId { get; set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }
}
