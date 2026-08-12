using System.ComponentModel.DataAnnotations;

namespace HorseRacing.Dtos;

public class JockeyListResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string? Nationality { get; set; }
    public int ExperienceYears { get; set; }
    public int TotalRaces { get; set; }
    public int TotalWins { get; set; }
    public decimal WinRate { get; set; }
    public int? Rank { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ApprovalStatus { get; set; }
    public string ApprovalStatusName { get; set; } = string.Empty;
}

public class JockeyInvitationRespondRequest
{
    [Required]
    public bool Accept { get; set; }
}

public class JockeyInvitationWithdrawRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
