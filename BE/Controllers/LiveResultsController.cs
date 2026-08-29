using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/live-results")]
public class LiveResultsController : ControllerBase
{
    private readonly ILiveResultService _liveResultService;
    private readonly IRefereeRepository _refereeRepo;
    private readonly IRefereeAssignmentRepository _assignmentRepo;

    public LiveResultsController(
        ILiveResultService liveResultService,
        IRefereeRepository refereeRepo,
        IRefereeAssignmentRepository assignmentRepo)
    {
        _liveResultService = liveResultService;
        _refereeRepo = refereeRepo;
        _assignmentRepo = assignmentRepo;
    }

    //Kiểm tra xem Trọng tài có đang được phân công vào Race này không
    private async Task<bool> IsAuthorizedForRaceAsync(Guid raceId)
    {
        if (User.IsInRole("Admin")) return true;

        var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId)) return false;

        var referee = await _refereeRepo.GetByUserIdAsync(userId);
        if (referee == null) return false;

        var assignments = await _assignmentRepo.GetByRefereeAsync(referee.Id);
        return assignments.Any(a => a.RaceId == raceId && a.Status == RefereeAssignmentStatus.Confirmed);
    }

    [HttpGet("race/{raceId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetLiveRaceResult(Guid raceId)
    {
        var result = await _liveResultService.GetLiveRaceResultAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("race/{raceId:guid}/positions")]
    [AllowAnonymous]
    public async Task<ActionResult> GetCurrentPositions(Guid raceId)
    {
        var result = await _liveResultService.GetCurrentPositionsAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("race/{raceId:guid}/ranking")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceRanking(Guid raceId)
    {
        var result = await _liveResultService.GetRaceRankingAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("race/{raceId:guid}/result")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateRaceResult(Guid raceId, [FromBody] SubmitRaceResultRequest request)
    {
        if (!await IsAuthorizedForRaceAsync(raceId))
            return StatusCode(403, new { message = "Từ chối truy cập: Bạn không được phân công giám sát cuộc đua này." });

        var result = await _liveResultService.UpdateRaceResultAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("race/{raceId:guid}/participant/{horseId:guid}/status")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateParticipantStatus(Guid raceId, Guid horseId, [FromBody] dynamic request)
    {
        if (!await IsAuthorizedForRaceAsync(raceId))
            return StatusCode(403, new { message = "Từ chối truy cập: Bạn không được phân công giám sát cuộc đua này." });

        string status = request?.status?.ToString();
        string[] validStatuses = { "Completed", "DNF", "DSQ", "InProgress", "Scratched", "Finished" };

        if (string.IsNullOrWhiteSpace(status) || !validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Trạng thái thi đấu không hợp lệ." });

        var result = await _liveResultService.UpdateParticipantStatusAsync(raceId, horseId, status);
        return StatusCode(result.StatusCode, result.Result);
    }
}