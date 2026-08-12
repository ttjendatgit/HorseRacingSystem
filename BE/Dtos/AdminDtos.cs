using System;

namespace HorseRacing.Dtos;

// User Registration DTOs
public class SubmitRegistrationRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RequestedRole { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentUrl { get; set; }
}

public class ApproveRegistrationRequest
{
    public Guid RegistrationId { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}

public class RejectRegistrationRequest
{
    public Guid RegistrationId { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}

public class UserRegistrationResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RequestedRole { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? RejectionReason { get; set; }
    public string? AdminNotes { get; set; }
}

// Admin Dashboard DTOs
public class AdminDashboardResponse
{
    public int TotalUsers { get; set; }
    public int TotalReferees { get; set; }
    public int ActiveTournaments { get; set; }
    public int UpcomingRaces { get; set; }
    public int PendingRegistrations { get; set; }
    public int OngoingRaces { get; set; }
    public List<ActiveRaceItem> ActiveRaces { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public class ActiveRaceItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public DateTime ScheduledAt { get; set; }
}

public class RecentActivityItem
{
    public string Action { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UserManagementResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public int HorseCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminHorseResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Height { get; set; }
    public string? Color { get; set; }
    public string? ImageUrl { get; set; }
    public int TotalRaces { get; set; }
    public int TotalWins { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ApprovalNote { get; set; }
    public Guid OwnerId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public Guid? AssignedJockeyId { get; set; }
    public string? AssignedJockeyName { get; set; }
    public string? JockeyAssignmentStatus { get; set; }
    public Guid[] AssignedJockeyIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateHorseApprovalStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class RejectJockeyRequest
{
    public string? Reason { get; set; }
}

public class RejectResultRequest
{
    public string Reason { get; set; } = string.Empty;
}

// Live Race Result DTOs
public class LiveRaceResultResponse
{
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ActualStartTime { get; set; }
    public int TotalParticipants { get; set; }
    public int FinishedCount { get; set; }
    public RaceTimingData? TimingData { get; set; }
    public CurrentPositionData[]? CurrentPositions { get; set; }
}

public class RaceTimingData
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? Duration { get; set; }
}

public class CurrentPositionData
{
    public int Position { get; set; }
    public Guid HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public Guid? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public string Status { get; set; } = string.Empty; // Finished, Running, Disqualified
    public double? TimeTaken { get; set; }
}

public class RaceRankingResponse
{
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public RankingEntry[]? Rankings { get; set; }
}

public class RankingEntry
{
    public int Position { get; set; }
    public Guid HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public Guid? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public double? TimeTaken { get; set; }
    public bool Won { get; set; }
}
