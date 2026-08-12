using System;
using System.ComponentModel.DataAnnotations;

namespace HorseRacing.Dtos;

public class PredictionCreateRequest
{
    [Required]
    public Guid RaceId { get; set; }

    [Required]
    public Guid PredictedHorseId { get; set; }

    public decimal BetAmount { get; set; } = 0;
}
