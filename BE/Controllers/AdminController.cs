using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

/// <summary>
/// Quản lý toàn bộ các chức năng quản trị hệ thống (Admin): Quản lý người dùng, duyệt hồ sơ, duyệt ngựa đua, duyệt kết quả thi đấu và quyết toán tiền cược.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IRaceEntryService _raceEntryService;

    public AdminController(IAdminService adminService, IRaceEntryRepository entryRepo, IRaceEntryService raceEntryService)
    {
        _adminService = adminService;
        _entryRepo = entryRepo;
        _raceEntryService = raceEntryService;
    }

    /// <summary>
    /// Lấy tổng quan các chỉ số Dashboard dành cho Admin (Tổng số người dùng, giải đấu, doanh thu, đơn nạp tiền).
    /// </summary>
    /// <returns>Dữ liệu chỉ số tổng quan hệ thống.</returns>
    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard()
    {
        var result = await _adminService.GetDashboardAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ tài khoản người dùng trong hệ thống (Chủ ngựa, Kỵ sĩ, Trọng tài, Khán giả).
    /// </summary>
    /// <returns>Danh sách tài khoản người dùng.</returns>
    [HttpGet("users")]
    public async Task<ActionResult> GetAllUsers()
    {
        var result = await _adminService.GetAllUsersAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một người dùng theo mã GUID.
    /// </summary>
    /// <param name="userId">Mã GUID người dùng.</param>
    /// <returns>Thông tin chi tiết người dùng.</returns>
    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult> GetUser(Guid userId)
    {
        var result = await _adminService.GetUserAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Vô hiệu hóa (Khóa) tài khoản của một người dùng trong hệ thống.
    /// </summary>
    /// <param name="userId">Mã GUID người dùng cần khóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện.</returns>
    [HttpPost("users/{userId:guid}/deactivate")]
    public async Task<ActionResult> DeactivateUser(Guid userId)
    {
        var result = await _adminService.DeactivateUserAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Kích hoạt lại tài khoản người dùng đã bị khóa trước đó.
    /// </summary>
    /// <param name="userId">Mã GUID người dùng cần mở khóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện.</returns>
    [HttpPost("users/{userId:guid}/reactivate")]
    public async Task<ActionResult> ReactivateUser(Guid userId)
    {
        var result = await _adminService.ReactivateUserAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy tất cả các đơn đăng ký tài khoản mới trong hệ thống.
    /// </summary>
    /// <returns>Danh sách đơn đăng ký tài khoản.</returns>
    [HttpGet("registrations")]
    public async Task<ActionResult> GetAllRegistrations()
    {
        var result = await _adminService.GetAllRegistrationsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách các đơn đăng ký tài khoản đang ở trạng thái Đang chờ duyệt (Pending).
    /// </summary>
    /// <returns>Danh sách đơn đăng ký chờ duyệt.</returns>
    [HttpGet("registrations/pending")]
    public async Task<ActionResult> GetPendingRegistrations()
    {
        var result = await _adminService.GetPendingRegistrationsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy chi tiết một đơn đăng ký tài khoản theo ID.
    /// </summary>
    /// <param name="id">Mã GUID đơn đăng ký.</param>
    /// <returns>Thông tin chi tiết đơn đăng ký.</returns>
    [HttpGet("registrations/{id:guid}")]
    public async Task<ActionResult> GetRegistration(Guid id)
    {
        var result = await _adminService.GetRegistrationAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Phê duyệt đơn đăng ký tài khoản người dùng mới.
    /// </summary>
    /// <param name="id">Mã GUID đơn đăng ký.</param>
    /// <param name="request">Yêu cầu phê duyệt.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả phê duyệt.</returns>
    [HttpPost("registrations/{id:guid}/approve")]
    public async Task<ActionResult> ApproveRegistration(Guid id, [FromBody] ApproveRegistrationRequest request)
    {
        request.RegistrationId = id;
        var result = await _adminService.ApproveRegistrationAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Từ chối đơn đăng ký tài khoản người dùng kèm theo lý do cụ thể.
    /// </summary>
    /// <param name="id">Mã GUID đơn đăng ký.</param>
    /// <param name="request">Thông tin từ chối và lý do.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả từ chối.</returns>
    [HttpPost("registrations/{id:guid}/reject")]
    public async Task<ActionResult> RejectRegistration(Guid id, [FromBody] RejectRegistrationRequest request)
    {
        request.RegistrationId = id;
        var result = await _adminService.RejectRegistrationAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách các con ngựa thuộc sở hữu của một chủ ngựa cụ thể.
    /// </summary>
    /// <param name="userId">Mã GUID của chủ ngựa.</param>
    /// <returns>Danh sách ngựa đua của chủ sở hữu.</returns>
    [HttpGet("users/{userId:guid}/horses")]
    public async Task<ActionResult> GetOwnerHorses(Guid userId)
    {
        var result = await _adminService.GetOwnerHorsesAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một con ngựa của một chủ ngựa cụ thể.
    /// </summary>
    /// <param name="userId">Mã GUID chủ ngựa.</param>
    /// <param name="horseId">Mã GUID con ngựa.</param>
    /// <returns>Thông tin chi tiết con ngựa.</returns>
    [HttpGet("users/{userId:guid}/horses/{horseId:guid}")]
    public async Task<ActionResult> GetOwnerHorse(Guid userId, Guid horseId)
    {
        var result = await _adminService.GetOwnerHorseAsync(userId, horseId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật trạng thái xét duyệt ngựa đua (Approved / Rejected / Pending) kèm lý do.
    /// </summary>
    /// <param name="userId">Mã GUID chủ ngựa.</param>
    /// <param name="horseId">Mã GUID con ngựa.</param>
    /// <param name="request">Thông tin cập nhật trạng thái xét duyệt.</param>
    /// <returns>Kết quả cập nhật trạng thái duyệt ngựa.</returns>
    [HttpPut("users/{userId:guid}/horses/{horseId:guid}/status")]
    public async Task<ActionResult> UpdateOwnerHorseStatus(
        Guid userId,
        Guid horseId,
        [FromBody] UpdateHorseApprovalStatusRequest request)
    {
        var result = await _adminService.UpdateOwnerHorseStatusAsync(userId, horseId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin hồ sơ chi tiết và chứng chỉ hành nghề của một kỵ sĩ (Jockey).
    /// </summary>
    /// <param name="jockeyId">Mã GUID của kỵ sĩ.</param>
    /// <returns>Hồ sơ chi tiết kỵ sĩ.</returns>
    [HttpGet("jockeys/{jockeyId:guid}")]
    public async Task<ActionResult> GetJockeyDetail(Guid jockeyId)
    {
        var result = await _adminService.GetJockeyDetailAsync(jockeyId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Phê duyệt chứng chỉ và cấp phép cho kỵ sĩ (Jockey) tham gia thi đấu chính thức.
    /// </summary>
    /// <param name="jockeyId">Mã GUID kỵ sĩ.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả phê duyệt kỵ sĩ.</returns>
    [HttpPost("jockeys/{jockeyId:guid}/approve")]
    public async Task<ActionResult> ApproveJockey(Guid jockeyId)
    {
        var result = await _adminService.ApproveJockeyAsync(jockeyId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Từ chối chứng chỉ hành nghề của kỵ sĩ (Jockey) kèm lý do giải thích cụ thể.
    /// </summary>
    /// <param name="jockeyId">Mã GUID kỵ sĩ.</param>
    /// <param name="request">Yêu cầu từ chối chứa lý do bắt buộc.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả từ chối kỵ sĩ.</returns>
    [HttpPost("jockeys/{jockeyId:guid}/reject")]
    public async Task<ActionResult> RejectJockey(Guid jockeyId, [FromBody] RejectJockeyRequest request)
    {
        var result = await _adminService.RejectJockeyAsync(jockeyId, request?.Reason);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Admin công nhận và phê duyệt kết quả thi đấu do Trọng tài báo cáo để tiến hành cộng điểm thưởng và công bố chính thức.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả duyệt thứ hạng trận đua.</returns>
    [HttpPost("races/{raceId:guid}/approve-result")]
    public async Task<ActionResult> ApproveRaceResult(Guid raceId)
    {
        var result = await _adminService.ApproveRaceResultAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Từ chối kết quả thi đấu do Trọng tài gửi và yêu cầu xem xét/kiểm tra lại.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="request">Yêu cầu từ chối kết quả kèm lý do cụ thể.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả từ chối.</returns>
    [HttpPost("races/{raceId:guid}/reject-result")]
    public async Task<ActionResult> RejectRaceResult(Guid raceId, [FromBody] RejectResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { message = "Cần nhập lý do." });
        var result = await _adminService.RejectRaceResultAsync(raceId, request.Reason);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Xử lý và áp dụng hình phạt đối với vi phạm thi đấu do Trọng tài báo cáo.
    /// </summary>
    /// <param name="violationId">Mã GUID biên bản vi phạm.</param>
    /// <param name="request">Thông tin hình phạt áp dụng.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả xử lý vi phạm.</returns>
    [HttpPost("violations/{violationId:guid}/resolve")]
    public async Task<ActionResult> ResolveViolation(Guid violationId, [FromBody] ResolveViolationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Penalty))
            return BadRequest(new { message = "Cần nhập hình phạt." });
        var result = await _adminService.ResolveViolationAsync(violationId, request.Penalty.Trim());
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Thử lại việc quyết toán và trả thưởng điểm cược cho các phiếu dự đoán còn sót lại của trận đua.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả quyết toán điểm thưởng.</returns>
    [HttpPost("races/{raceId:guid}/settle-predictions")]
    public async Task<ActionResult> SettlePredictions(Guid raceId)
    {
        var result = await _adminService.SettlePredictionsAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách tất cả các phiếu dự đoán cược toàn hệ thống dành cho Admin kiểm tra đối soát.
    /// </summary>
    /// <returns>Danh sách phiếu dự đoán cược.</returns>
    [HttpGet("predictions")]
    public async Task<ActionResult> GetPredictions()
    {
        var result = await _adminService.GetPredictionsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách các lượt đăng ký tham gia trận đua của ngựa đang chờ Admin duyệt.
    /// </summary>
    /// <returns>Danh sách các lượt đăng ký thi đấu chờ duyệt.</returns>
    [HttpGet("race-entries/pending")]
    public async Task<ActionResult> GetPendingRaceEntries()
    {
        var entries = await _entryRepo.GetPendingWithDetailsAsync();
        var result = entries.Select(e => new
        {
            EntryId = e.Id,
            RaceId = e.RaceId,
            RaceName = e.Race?.Name ?? "",
            TournamentName = e.Race?.Tournament?.Name ?? "",
            HorseId = e.HorseId,
            HorseName = e.Horse?.Name ?? "",
            OwnerName = e.Horse?.Owner?.User?.FullName ?? "",
            JockeyName = e.Jockey?.User?.FullName,
            Status = e.Status.ToString(),
            OwnerConfirmed = e.OwnerConfirmed,
            JockeyConfirmed = e.JockeyConfirmed
        });
        return Ok(ApiResult<object>.Ok(result));
    }

    /// <summary>
    /// Phê duyệt lượt đăng ký thi đấu của ngựa vào trận đua.
    /// </summary>
    /// <param name="entryId">Mã GUID lượt đăng ký thi đấu.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả phê duyệt.</returns>
    [HttpPost("race-entries/{entryId:guid}/approve")]
    public async Task<ActionResult> ApproveRaceEntry(Guid entryId)
    {
        var result = await _raceEntryService.ApproveAsync(entryId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Từ chối lượt đăng ký thi đấu của ngựa kèm theo lý do cụ thể.
    /// </summary>
    /// <param name="entryId">Mã GUID lượt đăng ký thi đấu.</param>
    /// <param name="request">Thông tin từ chối và lý do.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả từ chối.</returns>
    [HttpPost("race-entries/{entryId:guid}/reject")]
    public async Task<ActionResult> RejectRaceEntry(Guid entryId, [FromBody] EntryRejectRequest request)
    {
        var result = await _raceEntryService.RejectAsync(entryId, request?.Reason);
        return StatusCode(result.StatusCode, result.Result);
    }
}

/// <summary>
/// DTO chứa thông tin lý do từ chối lượt đăng ký thi đấu.
/// </summary>
public class EntryRejectRequest { public string? Reason { get; set; } }
