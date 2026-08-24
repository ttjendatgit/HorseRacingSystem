using System;
using System.Collections.Generic;

namespace HorseRacing.Dtos;

public class RaceSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;

    // Phase3B additions
    public Guid RoundId { get; set; }
    public int? RoundNumber { get; set; }
    public string? RoundName { get; set; }
    public Guid? TrackId { get; set; }
    public string? TrackName { get; set; }
    public int? QualificationSlots { get; set; }
    /// <summary>"Provisional" / "Official", or null when no RaceResult exists yet.</summary>
    public string? ResultStatus { get; set; }
}

// Additional Race DTOs for BE2
public class CreateRaceRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public Guid? RoundId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public Guid? TrackId { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int MaxParticipants { get; set; } = 12;
    public int Distance { get; set; } = 2000;
    public string? RoundNames { get; set; }
    public int? QualificationSlots { get; set; }
}

public class UpdateRaceRequest
{
    public string? Name { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public Guid? TrackId { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int? MaxParticipants { get; set; }
    public int? Distance { get; set; }
    public string? RoundNames { get; set; }
    public int? QualificationSlots { get; set; }
}

public class RaceDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    /// <summary>Non-nullable: Race.RoundId is required/DB NOT NULL since Phase1.</summary>
    public Guid RoundId { get; set; }
    public int? RoundNumber { get; set; }
    public string? RoundName { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public Guid? TrackId { get; set; }
    public string? TrackName { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>"Provisional" / "Official", or null when no RaceResult exists yet.</summary>
    public string? ResultStatus { get; set; }
    /// <summary>Set only when the most recent submission was rejected and awaits resubmission.</summary>
    public string? RejectedReason { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int MaxParticipants { get; set; }
    public int? QualificationSlots { get; set; }
    public int Distance { get; set; }
    public int EntriesCount { get; set; }
    public int ActiveRefereesCount { get; set; }
    public string? RoundNames { get; set; }
}

/// <summary>
/// Phase2B: replaces the raw RaceResult entity previously returned by
/// GET /api/races/{id}/result. Exposes ResultStatus explicitly so callers
/// never have to infer Official-ness from Race.Status.
/// </summary>
public class RaceResultResponse
{
    public Guid RaceId { get; set; }
    public Guid WinningHorseId { get; set; }
    public string? WinningHorseName { get; set; }
    public int TotalParticipants { get; set; }
    public decimal? WinnerFinishTime { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    /// <summary>"Provisional" or "Official" — the single source of truth for officiating state.</summary>
    public string ResultStatus { get; set; } = string.Empty;
    /// <summary>Convenience flag, always equivalent to ResultStatus == "Official".</summary>
    public bool IsOfficial { get; set; }
    public string? RejectedReason { get; set; }
    public bool IsDisputed { get; set; }
    public decimal? WinnerPurse { get; set; }
    /// <summary>Raw stored value, kept for compatibility. Prefer <see cref="Rankings"/>.</summary>
    public string? RankingsJson { get; set; }
    /// <summary>
    /// R0: bounded, ordered ranking (Position ascending). Null when the
    /// result predates R0 or its RankingsJson could not be parsed — callers
    /// must degrade to WinningHorseId/WinningHorseName in that case, never
    /// fabricate positions (see R0 §12).
    /// </summary>
    public List<RaceResultRankingItemResponse>? Rankings { get; set; }
    public string? Notes { get; set; }
}

public class RaceResultRankingItemResponse
{
    public int Position { get; set; }
    public Guid HorseId { get; set; }
    public string? HorseName { get; set; }
}

public class JockeyAssignedRaceResponse
{
    public Guid Id { get; set; }
    public Guid RaceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool OwnerConfirmed { get; set; }
    public bool JockeyConfirmed { get; set; }
    public JockeyAssignedRaceDetailResponse Race { get; set; } = new();
    public JockeyAssignedHorseResponse Horse { get; set; } = new();
}

public class JockeyAssignedRaceDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Description { get; set; }
    public int MaxParticipants { get; set; }
    public int Distance { get; set; }
    public JockeyAssignedTournamentResponse? Tournament { get; set; }
}

public class JockeyAssignedTournamentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class JockeyAssignedHorseResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public string? Gender { get; set; }
    public int Age { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Height { get; set; }
    public string? Color { get; set; }
    public int TotalRaces { get; set; }
    public int TotalWins { get; set; }
}

public class AssignHorseToRaceRequest
{
    public Guid HorseId { get; set; }
    public Guid? JockeyId { get; set; }
}

public class BulkAssignHorsesToRaceRequest
{
    public Guid[] HorseIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateOddsRequest
{
    public decimal Odds { get; set; }
}

public class AssignGateNumberRequest
{
    public int GateNumber { get; set; }
}
