using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/referees")]
public class RaceReportsController : ControllerBase
{
    private readonly IRaceReportService _service;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;

    public RaceReportsController(
        IRaceReportService service,
        IRefereeRepository refereeRepo,
        IRefereeAssignmentRepository assignmentRepo)
    {
        _service = service;
        _refereeRepo = refereeRepo;
        _assignmentRepo = assignmentRepo;
    }

    [HttpPost("reports")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Create([FromBody] CreateRaceReportRequest r)
    {
        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        var assignments = await _assignmentRepo.GetByRefereeAsync(referee.Id);
        var isAssigned = assignments.Any(a => a.RaceId == r.RaceId);
        if (!isAssigned)
            return Forbid();

        r.RefereeId = referee.Id;
        return OkR(await _service.CreateReportAsync(r));
    }

    [HttpGet("reports/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> Get(Guid id)
        => OkR(await _service.GetReportAsync(id));

    [HttpGet("race/{raceId:guid}/report")]
    [AllowAnonymous]
    public async Task<ActionResult> GetByRace(Guid raceId)
        => OkR(await _service.GetRaceReportAsync(raceId));

    [HttpGet("{refereeId:guid}/reports")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> GetByReferee(Guid refereeId)
        => OkR(await _service.GetRefereeReportsAsync(refereeId));

    [HttpPut("reports/{id:guid}")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Update(Guid id, [FromBody] CreateRaceReportRequest r)
    {
        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ trọng tài" });

        // Kéo dữ liệu báo cáo cũ lên để kiểm tra
        var existingReportResult = await _service.GetReportAsync(id);
        if (existingReportResult.StatusCode != 200 || existingReportResult.Result.Data == null)
            return NotFound(new { message = "Không tìm thấy báo cáo" });

        var report = existingReportResult.Result.Data;

        // Cấm sửa báo cáo của Trọng tài khác
        if (report.RefereeId != referee.Id)
            return StatusCode(403, new { message = "Từ chối truy cập: Không thể sửa đổi báo cáo của trọng tài khác." });

        // Đã Official thì khóa cứng vĩnh viễn
        if (report.IsOfficialReport)
            return BadRequest(new { message = "Báo cáo này đã được duyệt chính thức, không thể thay đổi nội dung." });

        return OkR(await _service.UpdateReportAsync(id, r));
    }

    [HttpPost("reports/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Publish(Guid id)
        => OkR(await _service.PublishReportAsync(id));

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}
