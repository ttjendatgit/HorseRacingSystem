using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/referees")]
public class ViolationsController : ControllerBase
{
    private readonly IViolationRecordService _service;
    public ViolationsController(IViolationRecordService service) => _service = service;

    [HttpPost("violations")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> Record([FromBody] CreateViolationRequest r)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ" });
        return OkR(await _service.RecordViolationAsync(r, userId));
    }

    [HttpGet("violations/{id:guid}")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> Get(Guid id)
        => OkR(await _service.GetViolationAsync(id));

    [HttpGet("race/{raceId:guid}/violations")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> GetByRace(Guid raceId)
        => OkR(await _service.GetRaceViolationsAsync(raceId));

    [HttpGet("horse/{horseId:guid}/violations")]
    [AllowAnonymous]
    public async Task<ActionResult> GetByHorse(Guid horseId)
        => OkR(await _service.GetHorseViolationsAsync(horseId));

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}
