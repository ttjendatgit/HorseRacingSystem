using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/tournaments")]
[Authorize(Roles = "Admin")]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;
    private readonly IRoundService _roundService;

    public TournamentsController(ITournamentService tournamentService, IRoundService roundService)
    {
        _tournamentService = tournamentService;
        _roundService = roundService;
    }

    /// <summary>
    /// Resolves the authenticated actor's user ID from the NameIdentifier claim. Never fabricates
    /// Guid.Empty on failure — returns false so the caller can fail the request safely.
    /// </summary>
    private bool TryGetActorId(out Guid actorId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out actorId);
    }

    private bool IsAdminCaller() => User?.IsInRole("Admin") == true;

    private static bool IsDraftTournament(TournamentResponse? tournament)
    {
        return tournament?.Status == TournamentStatus.Draft ||
               string.Equals(tournament?.StatusName, nameof(TournamentStatus.Draft), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ShouldHideDraftTournamentAsync(Guid tournamentId)
    {
        if (IsAdminCaller()) return false;

        var tournament = await _tournamentService.GetTournamentAsync(tournamentId);
        return tournament.Result.Success && IsDraftTournament(tournament.Result.Data);
    }

    // Tournament CRUD
    [HttpPost]
    public async Task<ActionResult> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        var result = await _tournamentService.CreateTournamentAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetAllTournaments()
    {
        var result = await _tournamentService.GetAllTournamentsAsync();
        if (!IsAdminCaller() && result.Result.Success && result.Result.Data != null)
        {
            result.Result.Data = result.Result.Data
                .Where(tournament => !IsDraftTournament(tournament))
                .ToList();
        }

        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult> GetActiveTournaments()
    {
        var result = await _tournamentService.GetActiveTournamentsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTournament(Guid id)
    {
        var result = await _tournamentService.GetTournamentAsync(id);
        if (!IsAdminCaller() && result.Result.Success && IsDraftTournament(result.Result.Data))
            return NotFound(ApiResult<TournamentResponse>.Fail("Không tìm thấy giải đấu"));

        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateTournament(Guid id, [FromBody] UpdateTournamentRequest request)
    {
        var result = await _tournamentService.UpdateTournamentAsync(id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTournament(Guid id)
    {
        var result = await _tournamentService.DeleteTournamentAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    // State machine endpoints
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult> ChangeTournamentStatus(Guid id, [FromBody] ChangeTournamentStatusRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return StatusCode(401, new { success = false, message = "Không thể xác định người dùng thực hiện thao tác." });

        var result = await _tournamentService.ChangeStatusAsync(id, request, actorId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{id:guid}/stats")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTournamentStats(Guid id)
    {
        if (await ShouldHideDraftTournamentAsync(id))
            return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));

        var result = await _tournamentService.GetStatsAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{id:guid}/timeline")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTournamentTimeline(Guid id)
    {
        if (await ShouldHideDraftTournamentAsync(id))
            return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));

        var result = await _tournamentService.GetTimelineAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    // Rounds Management
    [HttpPost("{tournamentId:guid}/rounds")]
    public async Task<ActionResult> CreateRound(Guid tournamentId, [FromBody] CreateRoundRequest request)
    {
        if (request.TournamentId != Guid.Empty && request.TournamentId != tournamentId)
            return BadRequest(new { message = "TournamentId không khớp với route" });
        request.TournamentId = tournamentId;

        var result = await _roundService.CreateRoundAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("{tournamentId:guid}/rounds")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRoundsByTournament(Guid tournamentId)
    {
        if (await ShouldHideDraftTournamentAsync(tournamentId))
            return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));

        var result = await _roundService.GetRoundsByTournamentAsync(tournamentId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("rounds/{roundId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRound(Guid roundId)
    {
        var result = await _roundService.GetRoundAsync(roundId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("rounds/{roundId:guid}")]
    public async Task<ActionResult> UpdateRound(Guid roundId, [FromBody] UpdateRoundRequest request)
    {
        var result = await _roundService.UpdateRoundAsync(roundId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("rounds/{roundId:guid}")]
    public async Task<ActionResult> DeleteRound(Guid roundId)
    {
        var result = await _roundService.DeleteRoundAsync(roundId);
        return StatusCode(result.StatusCode, result.Result);
    }
}
