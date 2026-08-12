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
public class HealthChecksController : ControllerBase
{
    private readonly IRefereeHealthCheckService _service;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;

    public HealthChecksController(
        IRefereeHealthCheckService service,
        IRefereeRepository refereeRepo,
        IRefereeAssignmentRepository assignmentRepo)
    {
        _service = service;
        _refereeRepo = refereeRepo;
        _assignmentRepo = assignmentRepo;
    }

    [HttpPost("health-checks")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Create([FromBody] CreateHealthCheckRequest r)
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
        return OkR(await _service.CreateHealthCheckAsync(r));
    }

    [HttpGet("health-checks/{id:guid}")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> Get(Guid id)
        => OkR(await _service.GetHealthCheckAsync(id));

    [HttpPost("health-checks/{id:guid}/complete")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Complete(Guid id, [FromBody] CompleteHealthCheckRequest r)
    {
        r.HealthCheckId = id;
        return OkR(await _service.CompleteHealthCheckAsync(r));
    }

    [HttpGet("race/{raceId:guid}/health-checks")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> GetByRace(Guid raceId)
        => OkR(await _service.GetRaceHealthChecksAsync(raceId));

    [HttpGet("horse/{horseId:guid}/health-history")]
    [AllowAnonymous]
    public async Task<ActionResult> GetHistory(Guid horseId)
        => OkR(await _service.GetHorseHealthCheckHistoryAsync(horseId));

    [HttpPost("health-checks/{id:guid}/approve")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> Approve(Guid id)
        => OkR(await _service.ApproveHorseForRaceAsync(id));

    [HttpPost("health-checks/{id:guid}/reject")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectHealthCheckRequest r)
        => OkR(await _service.RejectHorseForRaceAsync(id, r.Reason ?? "Không đạt kiểm tra sức khỏe"));

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}
