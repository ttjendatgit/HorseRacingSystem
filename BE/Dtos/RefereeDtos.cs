using System;

namespace HorseRacing.Dtos;

// Referee DTOs
public class CreateRefereeRequest
{
    public Guid UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Certifications { get; set; }
    public DateTime LicenseExpiryDate { get; set; }
}

public class UpdateRefereeRequest
{
    public string? LicenseNumber { get; set; }
    public string? Certifications { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    public bool? IsActive { get; set; }
}

public class RefereeResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Certifications { get; set; }
    public DateTime LicenseExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public int TotalAssignments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Referee Assignment DTOs
public class AssignRefereeRequest
{
    public Guid RaceId { get; set; }
    public Guid RefereeId { get; set; }
    public string Role { get; set; } = "Assistant"; // Chief Referee, Assistant, etc.
    public string? Notes { get; set; }
}

public class ConfirmRefereeAssignmentRequest
{
    public Guid AssignmentId { get; set; }
    public string? Notes { get; set; }
}

public class RespondToAssignmentRequest
{
    public Guid AssignmentId { get; set; }
    public string Response { get; set; } = string.Empty; // "Accept" or "Reject"
    public string? Notes { get; set; }
}

public class RefereeAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public string RaceStatus { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    /// <summary>"Provisional" / "Official", or null when no RaceResult exists yet.</summary>
    public string? ResultStatus { get; set; }
    /// <summary>Set when the most recent submission was rejected and awaits resubmission.</summary>
    public string? RejectedReason { get; set; }
    public string? RoundName { get; set; }
    public string? TournamentName { get; set; }
    /// <summary>GATE-V1 FINAL CAPACITY CORRECTION: the Race's Track's physical gate capacity —
    /// the actual upper bound for GateNumber (never Race.MaxParticipants). Null if the Race has no
    /// Track assigned yet or the Track's Capacity isn't set (should be unreachable once Published,
    /// since Publish readiness already requires both).</summary>
    public int? TrackCapacity { get; set; }
    public Guid RefereeId { get; set; }
    public string? RefereeName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
}
