using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    public NotificationCategory Category { get; set; }

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    public Guid? RelatedEntityId { get; set; }

    [MaxLength(100)]
    public string? RelatedEntityType { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime? ReadAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public bool IsSent { get; set; } = false;

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    public int? RetryCount { get; set; } = 0;

    public bool IsDeleted { get; set; } = false;
}
