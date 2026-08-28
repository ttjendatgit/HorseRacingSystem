using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/races/management")]
[Authorize(Roles = "Admin")]
public class RacesManagementController : ControllerBase
{
    private readonly IRaceManagementService _raceService;

    public RacesManagementController(IRaceManagementService raceService)
    {
        _raceService = raceService;
    }

    // Race CRUD
    [HttpPost]
    public async Task<ActionResult> CreateRace([FromBody] CreateRaceRequest request)
    {
        var result = await _raceService.CreateRaceAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{raceId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceDetails(Guid raceId)
    {
        var result = await _raceService.GetRaceDetailsAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("{raceId:guid}")]
    public async Task<ActionResult> UpdateRace(Guid raceId, [FromBody] UpdateRaceRequest request)
    {
        var result = await _raceService.UpdateRaceAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{raceId:guid}")]
    public async Task<ActionResult> DeleteRace(Guid raceId)
    {
        var result = await _raceService.DeleteRaceAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("tournament/{tournamentId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRacesByTournament(Guid tournamentId)
    {
        var result = await _raceService.GetRacesByTournamentAsync(tournamentId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("round/{roundId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRacesByRound(Guid roundId)
    {
        var result = await _raceService.GetRacesByRoundAsync(roundId);
        return StatusCode(result.StatusCode, result.Result);
    }

    // Horse Assignment
    [HttpGet("busy-horses")]
    [AllowAnonymous]
    public async Task<ActionResult> GetBusyHorseIds()
    {
        var result = await _raceService.GetBusyHorseIdsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/assign-horse")]
    public async Task<ActionResult> AssignHorseToRace(Guid raceId, [FromBody] AssignHorseToRaceRequest request)
    {
        var result = await _raceService.AssignHorseToRaceAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/bulk-assign-horses")]
    public async Task<ActionResult> BulkAssignHorsesToRace(Guid raceId, [FromBody] BulkAssignHorsesToRaceRequest request)
    {
        var result = await _raceService.BulkAssignHorsesToRaceAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{raceId:guid}/remove-horse/{horseId:guid}")]
    public async Task<ActionResult> RemoveHorseFromRace(Guid raceId, Guid horseId)
    {
        var result = await _raceService.RemoveHorseFromRaceAsync(raceId, horseId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("{raceId:guid}/entries/{horseId:guid}/odds")]
    public async Task<ActionResult> UpdateOdds(Guid raceId, Guid horseId, [FromBody] UpdateOddsRequest request)
    {
        var result = await _raceService.UpdateOddsAsync(raceId, horseId, request.Odds);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/recalculate-odds")]
    public async Task<ActionResult> RecalculateOdds(Guid raceId)
    {
        var result = await _raceService.RecalculateOddsForRaceAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    // Race Control
    [HttpPost("{raceId:guid}/open-registration")]
    public async Task<ActionResult> OpenRegistration(Guid raceId)
    {
        var result = await _raceService.OpenRegistrationAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/close-registration")]
    public async Task<ActionResult> CloseRegistration(Guid raceId)
    {
        var result = await _raceService.CloseRegistrationAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/start")]
    public async Task<ActionResult> StartRace(Guid raceId)
    {
        var result = await _raceService.StartRaceAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/end")]
    public async Task<ActionResult> EndRace(Guid raceId)
    {
        var result = await _raceService.EndRaceAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{raceId:guid}/cancel")]
    public async Task<ActionResult> CancelRace(Guid raceId)
    {
        var result = await _raceService.CancelRaceAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }
}
