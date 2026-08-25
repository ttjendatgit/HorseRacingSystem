using System;

namespace HorseRacing.Dtos;

// ── Prize (PRIZE-V1.2) ────────────────────────────────────────────────────────────────────
// Tournament.PrizePool = total prize budget; Prize rows = allocation of that budget by FINAL
// Tournament ranking Position. Config/display only — no wallet payout, no recipient, no
// distribution workflow exists in this contract. RaceId/IsDistributed/DistributedAt remain on
// the Prize entity (BE/Models/Prize.cs) for backward compatibility but are intentionally NOT
// part of this contract: RaceId is always null for V1 writes (a Prize row is a Tournament
// allocation, never Race-specific), and IsDistributed/DistributedAt are never set by this
// workflow (no payout mechanism exists) and are not exposed to keep the API from implying a
// distribution feature that doesn't exist.
//
// PRIZE-V1.2: Admin configures PercentageOfPool, never Amount directly — Amount is entirely
// backend-derived (see PrizeAmountCalculator) from PercentageOfPool * Tournament.PrizePool / 100.
// Amount is deliberately absent from both write DTOs below (not merely ignored-if-present) —
// there is no compatibility need to keep it, since PRIZE-V1/V1.1 were never committed to a
// released API consumer outside this same codebase (see PRIZE-V1.2 report §6).
public class CreatePrizeRequest
{
    public Guid? TournamentId { get; set; }
    public int Position { get; set; } = 1;
    public decimal PercentageOfPool { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SponsorName { get; set; }
}

// TournamentId is deliberately absent — immutable after creation (Part 9). To move a Prize row
// to a different Tournament, delete it while the original Tournament is still Draft and create a
// new one under the target Tournament.
public class UpdatePrizeRequest
{
    public int Position { get; set; }
    public decimal PercentageOfPool { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SponsorName { get; set; }
}

public class PrizeResponse
{
    public Guid Id { get; set; }
    public Guid? TournamentId { get; set; }
    public int Position { get; set; }
    public decimal PercentageOfPool { get; set; }
    /// <summary>Backend-derived (PercentageOfPool * Tournament.PrizePool / 100, VND-rounded) —
    /// never client-controlled. See PrizeAmountCalculator.</summary>
    public decimal Amount { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SponsorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Protest ──
public class CreateProtestRequest
{
    public Guid RaceId { get; set; }
    public Guid AgainstEntryId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
}

public class RuleProtestRequest
{
    public string Ruling { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string? AdminNotes { get; set; }
}

public class ProtestResponse
{
    public Guid Id { get; set; }
    public Guid RaceId { get; set; }
    public string? RaceName { get; set; }
    public Guid FiledByUserId { get; set; }
    public string? FiledByName { get; set; }
    public Guid AgainstEntryId { get; set; }
    public string? AgainstHorseName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Ruling { get; set; }
    public Guid? RuledByUserId { get; set; }
    public string? Resolution { get; set; }
    public DateTime FiledAt { get; set; }
    public DateTime? RuledAt { get; set; }
}

// ── HorseTransfer ──
public class CreateHorseTransferRequest
{
    public Guid HorseId { get; set; }
    public Guid ToOwnerId { get; set; }
    public string TransferType { get; set; } = "Sale";
    public decimal? Price { get; set; }
    public string? Reason { get; set; }
}

public class ApproveHorseTransferRequest
{
    public string? AdminNotes { get; set; }
}

public class HorseTransferResponse
{
    public Guid Id { get; set; }
    public Guid HorseId { get; set; }
    public string? HorseName { get; set; }
    public Guid FromOwnerId { get; set; }
    public string? FromOwnerName { get; set; }
    public Guid ToOwnerId { get; set; }
    public string? ToOwnerName { get; set; }
    public string TransferType { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ── Contract ──
public class CreateContractRequest
{
    public Guid OwnerId { get; set; }
    public Guid JockeyId { get; set; }
    public Guid? HorseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? BaseFee { get; set; }
    public decimal? WinBonusPercent { get; set; }
    public decimal? PerRaceFee { get; set; }
    public string? TermsAndConditions { get; set; }
}

public class ContractResponse
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public Guid JockeyId { get; set; }
    public string? JockeyName { get; set; }
    public Guid? HorseId { get; set; }
    public string? HorseName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? BaseFee { get; set; }
    public decimal? WinBonusPercent { get; set; }
    public decimal? PerRaceFee { get; set; }
    public string? TermsAndConditions { get; set; }
    public DateTime? SignedByOwnerAt { get; set; }
    public DateTime? SignedByJockeyAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── InjuryRecord ──
public class CreateInjuryRecordRequest
{
    public Guid HorseId { get; set; }
    public string InjuryType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Minor";
    public string? BodyPart { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public string? VeterinarianName { get; set; }
    public DateTime? ExpectedRecoveryDate { get; set; }
    public bool RequiresSurgery { get; set; }
}

public class InjuryRecordResponse
{
    public Guid Id { get; set; }
    public Guid HorseId { get; set; }
    public string? HorseName { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InjuryType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? BodyPart { get; set; }
    public string? Treatment { get; set; }
    public string? VeterinarianName { get; set; }
    public DateTime DiagnosedAt { get; set; }
    public DateTime? ExpectedRecoveryDate { get; set; }
    public DateTime? RecoveredAt { get; set; }
    public bool ClearedToRace { get; set; }
    public DateTime? ClearedAt { get; set; }
}
