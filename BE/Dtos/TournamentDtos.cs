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

    // Phase3B additions
    public decimal PrizePool { get; set; } = 0;
    public string? Venue { get; set; }
    public string? Country { get; set; }
    public string? Category { get; set; }
    public string? SurfaceType { get; set; }
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
    /// <summary>Property initializer applies when JSON omits this field, so an omitted MaxRounds never becomes 0.</summary>
    public int MaxRounds { get; set; } = 1;
}

public class UpdateTournamentRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    // IsActive removed (Phase4B): server-owned, derived from Status by lifecycle actions only.
    // An old client sending "isActive" in the JSON body is silently ignored by normal model binding.
    public string? ImageUrl { get; set; }

    // Phase3B additions — all nullable; null/omitted leaves the existing DB value untouched
    // (same convention already used above for Description/RegistrationDeadline/ImageUrl).
    // This means these fields cannot be explicitly cleared back to null through this endpoint —
    // a pre-existing limitation of this DTO's "null = don't touch" pattern, not new to Phase3B.
    public decimal? PrizePool { get; set; }
    public string? Venue { get; set; }
    public string? Country { get; set; }
    public string? Category { get; set; }
    public string? SurfaceType { get; set; }
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
    public int? MaxRounds { get; set; }
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

    // Phase3B additions
    public decimal PrizePool { get; set; }
    public string? Venue { get; set; }
    public string? Country { get; set; }
    public string? Category { get; set; }
    /// <summary>Nullable string, e.g. "Dirt"/"Turf"/"Synthetic"/"Sand" — see SurfaceType enum.</summary>
    public string? SurfaceType { get; set; }
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
    // Task C1 capacity gate: count of TournamentHorseRegistration rows with Status == Approved
    // for this Tournament — the Owner registration page needs this to know whether capacity is
    // full without a second round-trip (Pending/Rejected/Withdrawn never count here).
    public int ApprovedRegistrationCount { get; set; }
    public int MaxRounds { get; set; }
    /// <summary>Populated by ChangeStatusAsync's Cancelled transition (Phase4B) with the acting user's Id.</summary>
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

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

/// <summary>
/// Read-only "kết quả chung cuộc" of a Tournament — Position→Horse for the Round that actually
/// decided the Tournament (the Final, or an earlier Round if walkover/void ended it early).
/// Never derived while the Tournament is still Draft/Published/Ongoing.
/// </summary>
public class FinalStandingsDto
{
    public Guid TournamentId { get; set; }
    public bool IsFinal { get; set; }
    public bool IsVoid { get; set; }
    public string? VoidReason { get; set; }
    public bool IsWalkover { get; set; }
    public int? DecidingRoundNumber { get; set; }
    /// <summary>True when the deciding Round has more than 1 Race Finished+Official — no
    /// business rule exists yet for merging multiple parallel Races into one ranking, so
    /// Standings is deliberately left null instead of guessing (see Message for detail).</summary>
    public bool RequiresManualReview { get; set; }
    /// <summary>Human-readable explanation for the "chưa kết thúc" / RequiresManualReview cases.</summary>
    public string? Message { get; set; }
    public List<StandingEntryDto>? Standings { get; set; }
}

public class StandingEntryDto
{
    public int Position { get; set; }
    public Guid HorseId { get; set; }
    public string HorseName { get; set; } = string.Empty;
    public Guid? JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    /// <summary>Owner's User.Id (wallet identity) — distinct from OwnerId (Owner.Id, the Owners
    /// table PK). Needed by prize distribution (IWalletService.AddPointsAsync takes User.Id).</summary>
    public Guid? OwnerUserId { get; set; }
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
    public int? AdvanceCount { get; set; }
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
    public int? AdvanceCount { get; set; }
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
    public int? AdvanceCount { get; set; }
    /// <summary>
    /// Round 1: Tournament.MaxParticipants (null if unset). Round N&gt;1: the AdvanceCount of the
    /// single Round with RoundNumber == N-1 in the same Tournament; null if zero or more-than-one
    /// such predecessor exists (Round sequence uniqueness is not enforced until Phase5) — never guessed.
    /// </summary>
    public int? PlannedParticipants { get; set; }
    /// <summary>
    /// COUNT(DISTINCT RaceEntry.HorseId) across RaceEntries whose Race.RoundId == this round's Id.
    /// No RegistrationStatus filter (matches existing participant-counting behavior elsewhere).
    /// Descriptive/read-only — not persisted, not a validation source.
    /// </summary>
    public int ActualParticipants { get; set; }
}
