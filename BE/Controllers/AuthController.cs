using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using HorseRacing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly IRefereeRepository _refereeRepo;
    private readonly ICloudStorageService _cloudStorage;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserRepository userRepo, IJockeyRepository jockeyRepo, IRefereeRepository refereeRepo, ICloudStorageService cloudStorage, IEmailService emailService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userRepo = userRepo;
        _jockeyRepo = jockeyRepo;
        _refereeRepo = refereeRepo;
        _cloudStorage = cloudStorage;
        _emailService = emailService;
        _logger = logger;
    }

    // Authentication
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize(Roles = "HorseOwner")]
    [HttpGet("me")]
    public async Task<ActionResult<OwnerProfileResponse>> GetCurrentOwner()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.GetOwnerProfileAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult> GetProfile()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return NotFound();
        if (!user.IsActive) return StatusCode(403, new { message = "Tài khoản đã bị vô hiệu hóa" });

        return Ok(user.Role switch
        {
            UserRole.HorseOwner => user.OwnerProfile is not null
                ? new { user.Id, user.Email, user.FullName, Role = "HorseOwner", Type = "HorseOwner", Code = user.OwnerProfile.OwnerCode, Horses = user.OwnerProfile.Horses.Count, user.CreatedAt }
                : new { user.Id, user.Email, user.FullName, Role = "HorseOwner", Type = "HorseOwner", user.CreatedAt },
            UserRole.Jockey => await BuildJockeyProfileAsync(user),
            UserRole.Referee => await BuildRefereeProfileAsync(user),
            UserRole.Admin => new { user.Id, user.Email, user.FullName, Role = "Admin", Type = "Admin", user.CreatedAt },
            _ => new { user.Id, user.Email, user.FullName, Role = "Spectator", Type = "Spectator", user.CreatedAt }
        });
    }

    private async Task<object> BuildJockeyProfileAsync(User user)
    {
        var jockey = await _jockeyRepo.GetByUserIdAsync(user.Id);
        return jockey is null
            ? new { user.Id, user.Email, user.FullName, Role = "Jockey", Type = "Jockey", user.CreatedAt }
            : new { user.Id, user.Email, user.FullName, Role = "Jockey", Type = "Jockey", jockey.LicenseNumber, jockey.ExperienceYears, jockey.TotalRaces, jockey.TotalWins, WinRate = jockey.WinRate, jockey.Rank, jockey.Nationality, jockey.Status, user.CreatedAt };
    }

    private async Task<object> BuildRefereeProfileAsync(User user)
    {
        var referee = await _refereeRepo.GetByUserIdAsync(user.Id);
        return referee is null
            ? new { user.Id, user.Email, user.FullName, Role = "Referee", Type = "Referee", user.CreatedAt }
            : new { user.Id, user.Email, user.FullName, Role = "Referee", Type = "Referee", referee.LicenseNumber, referee.Specialization, referee.Rating, referee.TotalOfficiated, referee.Nationality, IsActive = referee.IsActive, user.CreatedAt };
    }

    // Roles
    [HttpGet("roles")]
    public ActionResult<string[]> GetRoles()
    {
        var roles = Enum.GetNames(typeof(UserRole));

        return Ok(roles);
    }

    // Chỉ trả về các role được phép đăng ký công khai (HorseOwner, Jockey, Spectator)
    [HttpGet("roles/register")]
    public ActionResult<string[]> GetRegisterRoles()
    {
        var roles = Enum.GetNames(typeof(UserRole))
            .Where(role =>
                string.Equals(role, nameof(UserRole.HorseOwner), StringComparison.Ordinal) ||
                string.Equals(role, nameof(UserRole.Jockey), StringComparison.Ordinal) ||
                string.Equals(role, nameof(UserRole.Spectator), StringComparison.Ordinal))
            .ToArray();

        return Ok(roles);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _authService.UpdateProfileAsync(userId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _authService.ChangePasswordAsync(userId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request.Email);
        var data = result.Result?.Data;
        var resetToken = data?.GetType().GetProperty("resetToken")?.GetValue(data) as string;

        if (!string.IsNullOrEmpty(resetToken))
        {
            var safeToken = System.Net.WebUtility.HtmlEncode(resetToken);
            var body = $@"
<h2>RaceMaster - Đặt lại mật khẩu</h2>
<p>Bạn đã yêu cầu đặt lại mật khẩu. Sử dụng mã sau để tạo mật khẩu mới:</p>
<h1 style='color:#8f6420;font-size:32px'>{safeToken}</h1>
<p>Mã này có hiệu lực trong 1 giờ.</p>
<p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
<p>— RaceMaster Team</p>";

            try
            {
                await _emailService.SendAsync(request.Email.Trim(), "RaceMaster - Đặt lại mật khẩu", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email đặt lại mật khẩu thất bại tới {Email}", request.Email.Trim());
                // Email failed — still return success for security
            }
        }

        return Ok(new { message = "Nếu email tồn tại, liên kết đặt lại mật khẩu đã được gửi" });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpPost("upload-document")]
    public async Task<ActionResult> UploadDocument(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Không có file nào được tải lên" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "Dung lượng file không được vượt quá 10MB" });

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { message = "Chỉ cho phép file PDF, JPG và PNG" });

        try
        {
            var url = await _cloudStorage.UploadAsync(file, "documents");
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
