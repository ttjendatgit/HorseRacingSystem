using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

/// <summary>
/// Thực thể đại diện cho một Giải đấu đua ngựa (Tournament) trong hệ thống.
/// </summary>
[Table("Tournaments")]
public class Tournament
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; } // Grade 1, Grade 2, Listed, etc.

    [MaxLength(200)]
    public string? Venue { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    public SurfaceType? SurfaceType { get; set; }

    public int MaxRounds { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PrizePool { get; set; } = 0;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;

    public DateTime? RegistrationDeadline { get; set; }

    public int? MinParticipants { get; set; }

    public int? MaxParticipants { get; set; }

    public Guid? CancelledBy { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    // PRIZE-V2: set once, atomically, at the exact moment a Tournament transitions to Finished
    // via walkover (RaceManagementService's eligible==1 branch) — null for the ordinary case
    // (Final Race actually played to >=1 Completed horse). "Walkover" is the only value written
    // today; kept as free text (not an enum) so a future distinct reason never needs a schema
    // change. RaceService.GetFinalStandingsAsync reads this first, before attempting to re-derive
    // standings from Round/Race data, because a walkover never has a real Final Race to read from.
    [MaxLength(500)]
    public string? FinishReason { get; set; }

    // PRIZE-V2: the Tournament's Position-1 Horse for a walkover finish — the only durable record
    // of who the champion is, since no Final Race is ever played in that case (nothing in
    // Round/Race/RaceResult can be re-derived from later). Only ever set together with
    // FinishReason == "Walkover".
    public Guid? ChampionHorseId { get; set; }
    public Horse? ChampionHorse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<Race> Races { get; set; } = new List<Race>();
}
