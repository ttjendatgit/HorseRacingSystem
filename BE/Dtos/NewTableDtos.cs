using System;

namespace HorseRacing.Dtos;

// ── Prize ──
public class CreatePrizeRequest
{
    public Guid? TournamentId { get; set; }
    public Guid? RaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int Position { get; set; } = 1;
    public decimal PercentageOfPool { get; set; }
    public string? SponsorName { get; set; }
    public string? Description { get; set; }
}

public class PrizeResponse
{
    public Guid Id { get; set; }
    public Guid? TournamentId { get; set; }
    public Guid? RaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int Position { get; set; }
    public decimal PercentageOfPool { get; set; }
    public string? SponsorName { get; set; }
    public string? Description { get; set; }
    public bool IsDistributed { get; set; }
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
