using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/management")]
public class ManagementController : ControllerBase
{
    private readonly IPrizeService _prize;
    private readonly IProtestService _protest;
    private readonly IRaceComplaintService _raceComplaint;
    private readonly IHorseTransferService _transfer;
    private readonly IContractService _contract;
    private readonly IInjuryRecordService _injury;
    private readonly ITournamentService _tournament;

    public ManagementController(IPrizeService prize, IProtestService protest, IRaceComplaintService raceComplaint, IHorseTransferService transfer, IContractService contract, IInjuryRecordService injury, ITournamentService tournament)
    {
        _prize = prize; _protest = protest; _raceComplaint = raceComplaint; _transfer = transfer; _contract = contract; _injury = injury; _tournament = tournament;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsAdminCaller() => User?.IsInRole("Admin") == true;

    /// <summary>Mirrors TournamentsController.IsDraftTournament — same Draft-hiding semantics,
    /// checked here too since Prize breakdown is Tournament-scoped and must follow the same
    /// visibility policy as the Tournament itself (PRIZE-V1.1 Part 6).</summary>
    private static bool IsDraftTournament(TournamentResponse? tournament) =>
        tournament?.Status == TournamentStatus.Draft ||
        string.Equals(tournament?.StatusName, nameof(TournamentStatus.Draft), StringComparison.OrdinalIgnoreCase);

    // ── Prizes (write: Admin only; read: Draft hidden from non-Admin) ──

    [HttpPost("prizes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreatePrize(CreatePrizeRequest r)
        => OkR(await _prize.CreateAsync(r));

    [HttpPut("prizes/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdatePrize(Guid id, UpdatePrizeRequest r)
        => OkR(await _prize.UpdateAsync(id, r));

    // Cross-tournament listing has no legitimate anonymous use case (it would otherwise leak Draft
    // Prize rows for every Tournament at once) and nothing in the current frontend calls it —
    // Admin-only, matching the write endpoints above.
    [HttpGet("prizes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetPrizes()
        => OkR(await _prize.GetAllAsync());

    [HttpGet("prizes/tournament/{tid:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPrizesByTournament(Guid tid)
    {
        if (!IsAdminCaller())
        {
            var tournament = await _tournament.GetTournamentAsync(tid);
            if (tournament.Result.Success && IsDraftTournament(tournament.Result.Data))
                return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));
        }
        return OkR(await _prize.GetByTournamentAsync(tid));
    }

    [HttpGet("prizes/race/{rid:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPrizesByRace(Guid rid)
        => OkR(await _prize.GetByRaceAsync(rid));

    [HttpDelete("prizes/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeletePrize(Guid id)
        => OkR(await _prize.DeleteAsync(id));

    // ── Protests (Owner/Jockey file, Admin rule) ──

    [HttpPost("protests")]
    [Authorize(Roles = "HorseOwner,Jockey,Admin")]
    public async Task<ActionResult> FileProtest(CreateProtestRequest r)
        => OkR(await _protest.FileAsync(r, GetUserId()));

    [HttpGet("protests")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetProtests()
        => OkR(await _protest.GetAllAsync());

    [HttpGet("protests/pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetPendingProtests()
        => OkR(await _protest.GetPendingAsync());

    [HttpGet("protests/mine")]
    [Authorize(Roles = "HorseOwner,Jockey,Admin")]
    public async Task<ActionResult> GetMyProtests()
        => OkR(await _protest.GetByFiledByUserAsync(GetUserId()));

    [HttpPost("protests/{id:guid}/under-review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> MarkProtestUnderReview(Guid id)
        => OkR(await _protest.MarkUnderReviewAsync(id, GetUserId()));

    [HttpPost("protests/{id:guid}/rule")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RuleProtest(Guid id, RuleProtestRequest r)
        => OkR(await _protest.RuleAsync(id, r, GetUserId()));

    [HttpPost("protests/{id:guid}/withdraw")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> WithdrawProtest(Guid id)
        => OkR(await _protest.WithdrawAsync(id, GetUserId()));

    // ── Race Complaints (Owner/Jockey file, Admin routes/rules, Referee explains) ──

    [HttpPost("race-complaints")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> FileRaceComplaint(CreateRaceComplaintRequest r)
        => OkR(await _raceComplaint.FileAsync(r, GetUserId()));

    [HttpGet("race-complaints")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetRaceComplaints([FromQuery] RaceComplaintStatus? status)
        => OkR(await _raceComplaint.GetAllAsync(status));

    [HttpGet("race-complaints/mine")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> GetMyRaceComplaints()
        => OkR(await _raceComplaint.GetByFiledByUserAsync(GetUserId()));

    [HttpGet("race-complaints/referee")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> GetRefereeRaceComplaints()
        => OkR(await _raceComplaint.GetForRefereeAsync(GetUserId()));

    [HttpGet("race-complaints/eligible-races")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> GetEligibleRaceComplaintRaces()
        => OkR(await _raceComplaint.GetEligibleRacesAsync(GetUserId()));

    [HttpPost("race-complaints/{id:guid}/route")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RouteRaceComplaint(Guid id, RouteRaceComplaintRequest r)
        => OkR(await _raceComplaint.RouteAsync(id, r, GetUserId()));

    [HttpPost("race-complaints/{id:guid}/respond")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult> RespondRaceComplaint(Guid id, RespondRaceComplaintRequest r)
        => OkR(await _raceComplaint.RespondAsync(id, r, GetUserId()));

    [HttpPost("race-complaints/{id:guid}/rule")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RuleRaceComplaint(Guid id, RuleRaceComplaintRequest r)
        => OkR(await _raceComplaint.RuleAsync(id, r, GetUserId()));

    [HttpPost("race-complaints/{id:guid}/withdraw")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> WithdrawRaceComplaint(Guid id)
        => OkR(await _raceComplaint.WithdrawAsync(id, GetUserId()));

    // ── Horse Transfers (Owner creates, Admin approves) ──

    [HttpPost("transfers")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> CreateTransfer(CreateHorseTransferRequest r)
        => OkR(await _transfer.CreateAsync(r, GetUserId()));

    [HttpGet("transfers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetTransfers()
        => OkR(await _transfer.GetAllAsync());

    [HttpGet("transfers/pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetPendingTransfers()
        => OkR(await _transfer.GetPendingAsync());

    [HttpPost("transfers/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ApproveTransfer(Guid id, ApproveHorseTransferRequest r)
        => OkR(await _transfer.ApproveAsync(id, r, GetUserId()));

    [HttpPost("transfers/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RejectTransfer(Guid id, [FromBody] RejectRequest r)
        => OkR(await _transfer.RejectAsync(id, r.Reason ?? "Đã từ chối", GetUserId()));

    // ── Contracts (Owner creates, Owner/Jockey sign) ──

    [HttpPost("contracts")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> CreateContract(CreateContractRequest r)
        => OkR(await _contract.CreateAsync(r));

    [HttpGet("contracts")]
    [Authorize(Roles = "Admin,HorseOwner,Jockey")]
    public async Task<ActionResult> GetContracts()
        => OkR(await _contract.GetAllAsync());

    [HttpPost("contracts/{id:guid}/sign-owner")]
    [Authorize(Roles = "HorseOwner")]
    public async Task<ActionResult> SignContractOwner(Guid id)
        => OkR(await _contract.SignByOwnerAsync(id, GetUserId()));

    [HttpPost("contracts/{id:guid}/sign-jockey")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> SignContractJockey(Guid id)
        => OkR(await _contract.SignByJockeyAsync(id, GetUserId()));

    // ── Injury Records (Referee/Admin manages) ──

    [HttpPost("injuries")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> CreateInjury(CreateInjuryRecordRequest r)
        => OkR(await _injury.CreateAsync(r, GetUserId()));

    [HttpGet("injuries")]
    [Authorize(Roles = "Admin,Referee,HorseOwner")]
    public async Task<ActionResult> GetInjuries()
        => OkR(await _injury.GetAllAsync());

    [HttpGet("injuries/horse/{hid:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetInjuriesByHorse(Guid hid)
        => OkR(await _injury.GetByHorseAsync(hid));

    [HttpPost("injuries/{id:guid}/recover")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult> MarkRecovered(Guid id)
        => OkR(await _injury.MarkRecoveredAsync(id));

    [HttpPost("injuries/{id:guid}/clear")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ClearToRace(Guid id)
        => OkR(await _injury.ClearToRaceAsync(id));

    private ActionResult OkR<T>(ServiceResult<T> r) => StatusCode(r.StatusCode, r.Result);
}

public class RejectRequest { public string? Reason { get; set; } }
