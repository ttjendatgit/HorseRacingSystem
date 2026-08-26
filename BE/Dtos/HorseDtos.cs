using System;
using System.ComponentModel.DataAnnotations;

namespace HorseRacing.Dtos;

public class HorseCreateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Height { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    public int TotalRaces { get; set; } = 0;

    public int TotalWins { get; set; } = 0;

    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
}

public class HorseUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Height { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    public int TotalRaces { get; set; }

    public int TotalWins { get; set; }

    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
}

public class JockeyInvitationCreateRequest
{
    [Required]
    public Guid JockeyId { get; set; }
    [Required]
    public Guid RaceId { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

public class JockeyRemovalRequest
{
    // J2 follow-up: multiple Pending/Accepted invitations can now exist for the same
    // Horse+Race, so the exact invitation being cancelled must be identified explicitly.
    [Required]
    public Guid InvitationId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class RaceRegistrationRequest
{
    public bool OwnerConfirmed { get; set; } = false;
}

// J3: Owner picks exactly one Accepted invitation as the official Jockey for a RaceEntry.
public class OwnerFinalConfirmJockeyRequest
{
    [Required]
    public Guid InvitationId { get; set; }
}
