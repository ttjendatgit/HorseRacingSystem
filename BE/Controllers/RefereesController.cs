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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Create([FromBody] CreateRefereeRequest r)
        => OkR(await _refereeService.CreateRefereeAsync(r));

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetAll()
        => OkR(await _refereeService.GetAllRefereesAsync());

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult> GetActive()
        => OkR(await _refereeService.GetActiveRefereesAsync());

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetById(Guid id)
        => OkR(await _refereeService.GetRefereeAsync(id));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRefereeRequest r)
        => OkR(await _refereeService.UpdateRefereeAsync(id, r));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(Guid id)
        => OkR(await _refereeService.DeleteRefereeAsync(id));

    // ── Assignments ──

    [HttpPost("assign")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Assign([FromBody] AssignRefereeRequest r)
        => OkR(await _refereeService.AssignRefereeToRaceAsync(r));

    [HttpGet("race/{raceId:guid}/assignments")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetRaceAssignments(Guid raceId)
        => OkR(await _refereeService.GetRaceAssignmentsAsync(raceId));

    [HttpGet("assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetAllAssignments()
        => OkR(await _refereeService.GetAllAssignmentsAsync());

    [HttpGet("{refereeId:guid}/assignments")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetRefereeAssignments(Guid refereeId)
        => OkR(await _refereeService.GetRefereeAssignmentsAsync(refereeId));

    [HttpPost("assignments/{assignmentId:guid}/confirm")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Confirm(Guid assignmentId, [FromBody] ConfirmRefereeAssignmentRequest r)
    {
        r.AssignmentId = assignmentId;
        return OkR(await _refereeService.ConfirmAssignmentAsync(r));
    }

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

    [HttpGet("race/{raceId:guid}/entries")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceEntries(Guid raceId)
    {
        var allEntries = await _entryRepo.GetByRaceAsync(raceId);
        var entries = allEntries.Where(e => e.Status == RegistrationStatus.Approved).ToList();

        // Auto-recalculate if all odds are at default (1.0)
        if (entries.Count > 0 && entries.All(e => e.Odds == 1.0m))
        {
            OddsCalculator.Recalculate(entries);
            await _entryRepo.UpdateRangeAsync(entries);
            await _unitOfWork.SaveChangesAsync();
        }

        return Ok(entries.Select(e => new
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
            Status = e.Status.ToString(),
            // GATE-V1: gate assignment is public race-schedule info (like Odds above), not sensitive —
            // exposed on this existing shared read endpoint rather than a new Referee-only one.
            GateNumber = e.GateNumber
        }));
    }

    // ── GATE-V1: Referee-only starting gate assignment ──

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

        // Any Confirmed Referee assigned to this exact Race may manage its gates — merely having
        // an assignment record (Assigned/Cancelled/Completed) is not enough, and a Confirmed
        // assignment to a DIFFERENT Race must not authorize this one.
        var raceAssignments = await _assignmentRepo.GetByRaceAsync(raceId);
        var isConfirmedForThisRace = raceAssignments.Any(a =>
            a.RefereeId == referee.Id && a.Status == RefereeAssignmentStatus.Confirmed);
        if (!isConfirmedForThisRace)
            return Forbid();

        var result = await _raceManagement.AssignGateNumberAsync(raceId, entryId, r.GateNumber);
        return StatusCode(result.StatusCode, result.Result);
    }

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

        // Verify referee is assigned to this race
        var assignments = await _assignmentRepo.GetByRefereeAsync(referee.Id);
        var isAssigned = assignments.Any(a => a.RaceId == raceId);
        if (!isAssigned)
            return Forbid();

        var result = await _liveResultService.UpdateRaceResultAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}
