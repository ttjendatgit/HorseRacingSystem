namespace HorseRacing.Dtos;

public class ResolveViolationRequest
{
    public string PenaltyType { get; set; } = "Warning";
    public int? PenaltyTimeSeconds { get; set; }
}
