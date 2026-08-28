using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/referees")]
public class RefereesController : ControllerBase
{
    private readonly IRefereeService _refereeService;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly ILiveResultService _liveResultService;
    private readonly IRaceManagementService _raceManagement;
    private readonly IUnitOfWork _unitOfWork;

    public RefereesController(
        IRefereeService refereeService,
        IRefereeRepository refereeRepo,
        IRefereeAssignmentRepository assignmentRepo,
        IRaceEntryRepository entryRepo,
        ILiveResultService liveResultService,
        IRaceManagementService raceManagement,
        IUnitOfWork unitOfWork)
    {
        _refereeService = refereeService;
        _refereeRepo = refereeRepo;
        _assignmentRepo = assignmentRepo;
        _entryRepo = entryRepo;
        _liveResultService = liveResultService;
        _raceManagement = raceManagement;
        _unitOfWork = unitOfWork;
    }

    // ── Referee CRUD ──

    /// <summary>
    /// Tạo mới một hồ sơ trọng tài trong hệ thống dành cho Admin.
    /// </summary>
    /// <param name="r">Yêu cầu tạo hồ sơ trọng tài.</param>
    /// <returns>Thông tin hồ sơ trọng tài vừa tạo.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Create([FromBody] CreateRefereeRequest r)
        => OkR(await _refereeService.CreateRefereeAsync(r));

    /// <summary>
    /// Lấy danh sách tất cả trọng tài trong hệ thống.
    /// </summary>
    /// <returns>Danh sách trọng tài.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetAll()
        => OkR(await _refereeService.GetAllRefereesAsync());

    /// <summary>
    /// Lấy danh sách các trọng tài đang ở trạng thái Hoạt động (Active).
    /// </summary>
    /// <returns>Danh sách trọng tài đang hoạt động.</returns>
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult> GetActive()
        => OkR(await _refereeService.GetActiveRefereesAsync());

    /// <summary>
    /// Lấy thông tin hồ sơ chi tiết của một trọng tài theo mã GUID định danh.
    /// </summary>
    /// <param name="id">Mã GUID của trọng tài.</param>
    /// <returns>Thông tin hồ sơ trọng tài.</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetById(Guid id)
        => OkR(await _refereeService.GetRefereeAsync(id));

    /// <summary>
    /// Cập nhật thông tin chuyên môn hoặc giấy phép của trọng tài.
    /// </summary>
    /// <param name="id">Mã GUID của trọng tài.</param>
    /// <param name="r">Thông tin cập nhật.</param>
    /// <returns>Hồ sơ trọng tài sau khi cập nhật.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRefereeRequest r)
        => OkR(await _refereeService.UpdateRefereeAsync(id, r));

    /// <summary>
    /// Xóa thông tin hồ sơ trọng tài dành cho Admin.
    /// </summary>
    /// <param name="id">Mã GUID trọng tài cần xóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện xóa.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(Guid id)
        => OkR(await _refereeService.DeleteRefereeAsync(id));

    // ── Assignments ──

    /// <summary>
    /// Phân công nhiệm vụ trọng tài giám sát một trận đua cụ thể dành cho Admin.
    /// </summary>
    /// <param name="r">Thông tin phân công trọng tài vào trận đua.</param>
    /// <returns>Kết quả phân công trọng tài.</returns>
    [HttpPost("assign")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Assign([FromBody] AssignRefereeRequest r)
        => OkR(await _refereeService.AssignRefereeToRaceAsync(r));

    /// <summary>
    /// Lấy danh sách trọng tài được phân công giám sát trận đua cụ thể.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Danh sách phân công trọng tài theo trận đua.</returns>
    [HttpGet("race/{raceId:guid}/assignments")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetRaceAssignments(Guid raceId)
        => OkR(await _refereeService.GetRaceAssignmentsAsync(raceId));

    /// <summary>
    /// Lấy toàn bộ danh sách phân công trọng tài trong tất cả các trận đua dành cho Admin.
    /// </summary>
    /// <returns>Danh sách tất cả các lịch phân công trọng tài.</returns>
    [HttpGet("assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetAllAssignments()
        => OkR(await _refereeService.GetAllAssignmentsAsync());

    /// <summary>
    /// Lấy danh sách nhiệm vụ phân công của một trọng tài cụ thể theo ID.
    /// </summary>
    /// <param name="refereeId">Mã GUID trọng tài.</param>
    /// <returns>Danh sách trận đua trọng tài được phân công.</returns>
    [HttpGet("{refereeId:guid}/assignments")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetRefereeAssignments(Guid refereeId)
        => OkR(await _refereeService.GetRefereeAssignmentsAsync(refereeId));

    /// <summary>
    /// Trọng tài xác nhận tiếp nhận phân công nhiệm vụ trận đua.
    /// </summary>
    /// <param name="assignmentId">Mã GUID bản ghi phân công.</param>
    /// <param name="r">Yêu cầu xác nhận phân công.</param>
    /// <returns>Kết quả xác nhận phân công.</returns>
    [HttpPost("assignments/{assignmentId:guid}/confirm")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Confirm(Guid assignmentId, [FromBody] ConfirmRefereeAssignmentRequest r)
    {
        r.AssignmentId = assignmentId;
        return OkR(await _refereeService.ConfirmAssignmentAsync(r));
    }

    /// <summary>
    /// Trọng tài lấy danh sách tất cả nhiệm vụ thi đấu cá nhân của mình (Đã xác nhận, Đang chờ, Đã hủy).
    /// </summary>
    /// <param name="status">Lọc theo trạng thái phân công (tùy chọn).</param>
    /// <returns>Danh sách nhiệm vụ phân công cá nhân của trọng tài.</returns>
    [HttpGet("my-assignments")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> GetMyAssignments([FromQuery] string? status)
    {
        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        var result = await _refereeService.GetRefereeAssignmentsAsync(referee.Id);
        if (result.StatusCode != 200)
            return StatusCode(result.StatusCode, result.Result);

        if (!string.IsNullOrEmpty(status)
            && result.Result.Data is System.Collections.Generic.IEnumerable<RefereeAssignmentResponse> all)
        {
            var filtered = all.Where(a =>
                string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
            return Ok(ApiResult<IEnumerable<RefereeAssignmentResponse>>.Ok(filtered));
        }

        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Trọng tài gửi phản hồi Đồng ý (Accept) hoặc Từ chối (Reject) lịch phân công giám sát trận đua.
    /// </summary>
    /// <param name="assignmentId">Mã GUID bản ghi phân công.</param>
    /// <param name="r">Yêu cầu phản hồi chấp nhận hoặc từ chối kèm lý do.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả phản hồi.</returns>
    [HttpPost("assignments/{assignmentId:guid}/respond")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Respond(Guid assignmentId, [FromBody] RespondToAssignmentRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Response) || r.Response is not ("Accept" or "Reject"))
            return BadRequest(new { message = "Phản hồi phải là 'Accept' hoặc 'Reject'." });

        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        var assignment = await _assignmentRepo.GetByIdAsync(assignmentId);
        if (assignment is null)
            return NotFound(new { message = "Không tìm thấy phân công trọng tài." });

        if (assignment.RefereeId != referee.Id)
            return Forbid();

        if (assignment.Status != RefereeAssignmentStatus.Assigned)
            return BadRequest(new { message = "Phân công này đã được xử lý trước đó." });

        if (string.Equals(r.Response, "Accept", StringComparison.OrdinalIgnoreCase))
        {
            assignment.Status = RefereeAssignmentStatus.Confirmed;
            assignment.ConfirmedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(r.Notes)) assignment.Notes = r.Notes;
        }
        else
        {
            assignment.Status = RefereeAssignmentStatus.Cancelled;
            assignment.CompletedAt = DateTime.UtcNow;
            assignment.Notes = r.Notes ?? "Từ chối bởi trọng tài";
        }

        await _assignmentRepo.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync();
        return Ok(new { message = $"Đã {r.Response.ToLowerInvariant()} phân công." });
    }

    /// <summary>
    /// Lấy danh sách các con ngựa & kỵ sĩ đã được duyệt tham gia trận đua kèm theo số cổng xuất phát.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Danh sách lượt đua chính thức.</returns>
    [HttpGet("race/{raceId:guid}/entries")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceEntries(Guid raceId)
    {
        var allEntries = await _entryRepo.GetByRaceAsync(raceId);
        var entries = allEntries.Where(e => e.Status == RegistrationStatus.Approved).ToList();

        if (entries.Count > 0 && entries.All(e => e.Odds == 1.0m))
        {
            OddsCalculator.Recalculate(entries);
            await _entryRepo.UpdateRangeAsync(entries);
            await _unitOfWork.SaveChangesAsync();
        }

        var scores = entries.Select(e =>
        {
            decimal hRate = e.Horse != null && e.Horse.TotalRaces > 0 ? ((decimal)e.Horse.TotalWins / e.Horse.TotalRaces) * 100m : 10.0m;
            decimal jRate = e.Jockey != null && e.Jockey.WinRate > 0 ? e.Jockey.WinRate : 10.0m;
            decimal sc = (hRate * 0.70m) + (jRate * 0.30m);
            return sc <= 0m ? 0.01m : sc;
        }).ToList();
        decimal totalScore = scores.Sum();

        return Ok(entries.Select((e, idx) => new
        {
            EntryId = e.Id,
            HorseId = e.HorseId,
            HorseName = e.Horse?.Name ?? e.HorseId.ToString(),
            OwnerName = e.Horse?.Owner?.OrganizationName ?? e.Horse?.Owner?.User?.FullName,
            HorseWinRate = e.Horse != null && e.Horse.TotalRaces > 0
                ? Math.Round((decimal)e.Horse.TotalWins / e.Horse.TotalRaces * 100, 1)
                : 0,
            HorseTotalRaces = e.Horse?.TotalRaces ?? 0,
            JockeyId = e.JockeyId,
            JockeyName = e.Jockey?.User?.FullName,
            JockeyWinRate = e.Jockey?.WinRate ?? 0,
            Odds = e.Odds,
            ProbabilityPercent = totalScore > 0 ? Math.Round((scores[idx] / totalScore) * 100m, 1) : 0m,
            Status = e.Status.ToString(),
            GateNumber = e.GateNumber
        }));
    }

    /// <summary>
    /// Trọng tài phân công số cổng xuất phát (Gate Number) cho từng lượt đua của con ngựa trước giờ xuất phát.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="entryId">Mã GUID lượt đăng ký thi đấu.</param>
    /// <param name="r">Yêu cầu gán số cổng xuất phát.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả gán cổng xuất phát.</returns>
    [HttpPut("race/{raceId:guid}/entries/{entryId:guid}/gate")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> AssignGateNumber(Guid raceId, Guid entryId, [FromBody] AssignGateNumberRequest r)
    {
        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        var raceAssignments = await _assignmentRepo.GetByRaceAsync(raceId);
        var isConfirmedForThisRace = raceAssignments.Any(a =>
            a.RefereeId == referee.Id && a.Status == RefereeAssignmentStatus.Confirmed);
        if (!isConfirmedForThisRace)
            return Forbid();

        var result = await _raceManagement.AssignGateNumberAsync(raceId, entryId, r.GateNumber);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Trọng tài ghi nhận và gửi kết quả thi đấu chính thức (Thứ hạng 1, 2, 3...) sau khi cuộc đua kết thúc.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="request">Bảng kết quả thứ hạng các con ngựa về đích.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả cập nhật thứ hạng.</returns>
    [HttpPost("race/{raceId:guid}/submit-result")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> SubmitRaceResult(Guid raceId, [FromBody] RaceResultRequest request)
    {
        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        var assignments = await _assignmentRepo.GetByRefereeAsync(referee.Id);
        var isAssigned = assignments.Any(a => a.RaceId == raceId);
        if (!isAssigned)
            return Forbid();

        var result = await _liveResultService.UpdateRaceResultAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}
