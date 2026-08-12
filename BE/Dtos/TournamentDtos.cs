using System;
using HorseRacing.Models;

namespace HorseRacing.Dtos;

// Tournament DTOs
public class CreateTournamentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateTournamentRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public bool? IsActive { get; set; }
    public string? ImageUrl { get; set; }
}

public class TournamentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int RoundCount { get; set; }
    public int RaceCount { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // New fields for state machine
    public TournamentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? RegistrationDeadline { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Stats
    public TournamentStatsDto? Stats { get; set; }

    // Available transitions
    public List<NextTransitionDto> NextTransitions { get; set; } = new();
}

// New DTOs for Tournament Management
public class ChangeTournamentStatusRequest
{
    public TournamentStatus NewStatus { get; set; }
    public string? Reason { get; set; } // For cancellation
}

public class TournamentStatsDto
{
    public int RaceCount { get; set; }
    public int EntryCount { get; set; }
    public int HorseCount { get; set; }
    public int JockeyCount { get; set; }
    public int? DaysRemaining { get; set; }
}

public class TournamentTimelineDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public TournamentStatus? Status { get; set; }
}

public class NextTransitionDto
{
    public TournamentStatus Status { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

// Round DTOs
public class CreateRoundRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public int RoundNumber { get; set; }
    public DateTime ScheduledStartDate { get; set; }
    public DateTime ScheduledEndDate { get; set; }
    public string? Description { get; set; }
}

public class UpdateRoundRequest
{
    public string? Name { get; set; }
    public int? RoundNumber { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string? Description { get; set; }
}

public class RoundResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public int RoundNumber { get; set; }
    public DateTime ScheduledStartDate { get; set; }
    public DateTime ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string? Description { get; set; }
    public int RaceCount { get; set; }
}
