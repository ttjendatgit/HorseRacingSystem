using System;
using HorseRacing.Models;

namespace HorseRacing.Dtos;

// Request DTOs
public class CreateAuditLogDto
{
    public Guid AdminId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? UserId { get; set; }
}

public class AuditLogFilterDto
{
    public Guid? AdminId { get; set; }
    public string? EntityType { get; set; }
    public AuditAction? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? UserId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// Response DTOs
public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid AdminId { get; set; }
    public string? AdminEmail { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangesSummary { get; set; }
    public Guid? UserId { get; set; }
}

public class AuditLogDetailDto
{
    public Guid Id { get; set; }
    public Guid AdminId { get; set; }
    public string? AdminEmail { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangesSummary { get; set; }
    public Guid? UserId { get; set; }
    public string? AffectedUserEmail { get; set; }
}

public class AuditLogStatsDto
{
    public int TotalAuditLogs { get; set; }
    public DateTime EarliestLog { get; set; }
    public DateTime LatestLog { get; set; }
    public Dictionary<string, int> ByAction { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ByEntityType { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ByAdmin { get; set; } = new Dictionary<string, int>();
    public int LastWeekCount { get; set; }
    public int LastMonthCount { get; set; }
}

public class AuditExportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? EntityType { get; set; }
    public string Format { get; set; } = "json"; // json or csv
}
