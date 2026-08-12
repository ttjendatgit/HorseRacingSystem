using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid AdminId { get; set; }

    [ForeignKey("AdminId")]
    public User? Admin { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public Guid EntityId { get; set; }

    [Required]
    public AuditAction Action { get; set; }

    [MaxLength(2000)]
    public string? OldValues { get; set; }

    [MaxLength(2000)]
    public string? NewValues { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ChangesSummary { get; set; }

    public Guid? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? AffectedUser { get; set; }
}
