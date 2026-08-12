using System;
using HorseRacing.Models;

namespace HorseRacing.Dtos;

// Request DTOs
public class CreateNotificationDto
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public string? ActionUrl { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

public class NotificationFilterDto
{
    public bool? IsRead { get; set; }
    public NotificationType? Type { get; set; }
    public NotificationCategory? Category { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BulkNotificationDto
{
    public List<Guid> UserIds { get; set; } = new List<Guid>();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public string? ActionUrl { get; set; }
}

// Response DTOs
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public string? ActionUrl { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsSent { get; set; }
}

public class NotificationDetailDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public string? ActionUrl { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsSent { get; set; }
    public int RetryCount { get; set; }
    public string? FailureReason { get; set; }
}

public class NotificationStatsDto
{
    public int TotalNotifications { get; set; }
    public int UnreadCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public Dictionary<string, int> ByCategory { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ByType { get; set; } = new Dictionary<string, int>();
}

public class MarkNotificationsAsReadDto
{
    public List<Guid> NotificationIds { get; set; } = new List<Guid>();
}
