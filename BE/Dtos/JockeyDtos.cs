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

public class UpdateJockeyProfileRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ không được để trống.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateTime? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Chiều cao không được để trống.")]
    public decimal? Height { get; set; }

    [Required(ErrorMessage = "Cân nặng không được để trống.")]
    public decimal? Weight { get; set; }

    [Required(ErrorMessage = "Số CCCD / CMND không được để trống.")]
    public string IdCardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số giấy phép thi đấu không được để trống.")]
    public string LicenseNumber { get; set; } = string.Empty;

    public string LicenseFile { get; set; } = string.Empty;
}
