using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/live-results")]
public class LiveResultsController : ControllerBase
{
    private readonly ILiveResultService _liveResultService;

    public LiveResultsController(ILiveResultService liveResultService)
    {
        _liveResultService = liveResultService;
    }

    // Live Results
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

    // Rankings
    [HttpGet("race/{raceId:guid}/ranking")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceRanking(Guid raceId)
    {
        var result = await _liveResultService.GetRaceRankingAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    // Update Results (Admin only)
    [HttpPost("race/{raceId:guid}/result")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateRaceResult(Guid raceId, [FromBody] RaceResultRequest request)
    {
        var result = await _liveResultService.UpdateRaceResultAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("race/{raceId:guid}/participant/{horseId:guid}/status")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateParticipantStatus(Guid raceId, Guid horseId, [FromBody] dynamic request)
    {
        string status = request?.status?.ToString() ?? "Không xác định";
        var result = await _liveResultService.UpdateParticipantStatusAsync(raceId, horseId, status);
        return StatusCode(result.StatusCode, result.Result);
    }
}
