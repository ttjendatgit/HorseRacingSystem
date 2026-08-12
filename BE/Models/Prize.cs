using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Prizes")]
public class Prize
{
    [Key]
    public Guid Id { get; set; }

    public Guid? TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public Guid? RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public int Position { get; set; } = 1; // 1st, 2nd, 3rd, etc.

    /// <summary>
    /// Percentage of total prize pool (e.g., 60 for 1st place, 20 for 2nd, 10 for 3rd)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal PercentageOfPool { get; set; } = 0;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? SponsorName { get; set; }

    public bool IsDistributed { get; set; } = false;

    public DateTime? DistributedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
