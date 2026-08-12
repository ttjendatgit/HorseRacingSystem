using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/races")]
public class RacesController : ControllerBase
{
    private readonly IRaceService _raceService;
    private readonly IRaceManagementService _raceManagementService;
    private readonly IRefereeService _refereeService;

    public RacesController(IRaceService raceService, IRaceManagementService raceManagementService, IRefereeService refereeService)
    {
        _raceService = raceService;
        _raceManagementService = raceManagementService;
        _refereeService = refereeService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaces()
    {
        var result = await _raceService.GetRacesAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRace(Guid id)
    {
        var result = await _raceService.GetRaceAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{id:guid}/result")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceResult(Guid id)
    {
        var result = await _raceService.GetRaceResultAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{id:guid}/horses/{horseId:guid}/release")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ReleaseHorse(Guid id, Guid horseId)
    {
        var result = await _raceService.ReleaseHorseAsync(id, horseId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateRace(Guid id, [FromBody] UpdateRaceRequest request)
    {
        var result = await _raceManagementService.UpdateRaceAsync(id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteRace(Guid id)
    {
        var result = await _raceManagementService.DeleteRaceAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{id:guid}/referees")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AssignReferee(Guid id, [FromBody] AssignRefereeRequest request)
    {
        request.RaceId = id;
        var result = await _refereeService.AssignRefereeToRaceAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

}
