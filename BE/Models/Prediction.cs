using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Predictions")]
public class Prediction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid SpectatorUserId { get; set; }

    public User? Spectator { get; set; }

    [Required]
    public Guid PredictedHorseId { get; set; }

    public Horse? PredictedHorse { get; set; }

    [MaxLength(200)]
    public string? HorseNameSnapshot { get; set; }

    public PredictionStatus Status { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BetAmount { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal Odds { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PotentialPayout { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PayoutAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SettledAt { get; set; }
}
