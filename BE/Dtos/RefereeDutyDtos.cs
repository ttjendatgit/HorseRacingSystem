using System;

namespace HorseRacing.Dtos;

// Health Check DTOs
public class RejectHealthCheckRequest
{
    public string? Reason { get; set; }
}

public class CreateHealthCheckRequest
{
    public Guid HorseId { get; set; }
    public Guid RaceId { get; set; }
    public Guid RefereeId { get; set; }
    public string? Observations { get; set; }
    public string HealthCheckStatus { get; set; } = "Passed"; // Passed, Failed, RequiresRecheck
}

public class CompleteHealthCheckRequest
{
    public Guid HealthCheckId { get; set; }
    public string Status { get; set; } = string.Empty; // Passed, Failed, RequiresRecheck
    public string? Verdict { get; set; }
    public bool ApprovedToRace { get; set; }
}

public class HealthCheckResponse
{
    public Guid Id { get; set; }
    public Guid HorseId { get; set; }
    public string? HorseName { get; set; }
    public Guid RaceId { get; set; }
    public string? RaceName { get; set; }
    public Guid RefereeId { get; set; }
    public string? RefereeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public string? Observations { get; set; }
    public string? Verdict { get; set; }
    public bool ApprovedToRace { get; set; }
}

// Violation Record DTOs
public class CreateViolationRequest
{
    public Guid RaceId { get; set; }
    public Guid HorseId { get; set; }
    public int ViolationType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? Penalty { get; set; }
    public string? Severity { get; set; }
}

public class ViolationResponse
{
    public Guid Id { get; set; }
    public Guid RaceId { get; set; }
    public string? RaceName { get; set; }
    public Guid RaceEntryId { get; set; }
    public string? HorseName { get; set; }
    public Guid RefereeId { get; set; }
    public string? RefereeName { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public string? Evidence { get; set; }
    public string? Penalty { get; set; }
}

// Race Report DTOs
public class CreateRaceReportRequest
{
    public Guid RaceId { get; set; }
    public Guid RefereeId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? Incidents { get; set; }
    public string? RecommendedActions { get; set; }
}

public class RaceReportResponse
{
    public Guid Id { get; set; }
    public Guid RaceId { get; set; }
    public string? RaceName { get; set; }
    public Guid RefereeId { get; set; }
    public string? RefereeName { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? Incidents { get; set; }
    public string? RecommendedActions { get; set; }
    public bool IsOfficialReport { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
