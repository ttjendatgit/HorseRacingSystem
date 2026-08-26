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
[Route("api/management")]
public class ManagementController : ControllerBase
{
    private readonly IPrizeService _prize;
    private readonly IProtestService _protest;
    private readonly IHorseTransferService _transfer;
    private readonly IContractService _contract;
    private readonly IInjuryRecordService _injury;

    public ManagementController(IPrizeService prize, IProtestService protest, IHorseTransferService transfer, IContractService contract, IInjuryRecordService injury)
    {
        _prize = prize; _protest = protest; _transfer = transfer; _contract = contract; _injury = injury;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Prizes (Admin only) ──

    [HttpPost("prizes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreatePrize(CreatePrizeRequest r)
        => OkR(await _prize.CreateAsync(r));

    [HttpGet("prizes")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPrizes()
        => OkR(await _prize.GetAllAsync());

    [HttpGet("prizes/tournament/{tid:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPrizesByTournament(Guid tid)
        => OkR(await _prize.GetByTournamentAsync(tid));

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
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetProtests()
        => OkR(await _protest.GetAllAsync());

    [HttpGet("protests/pending")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> GetPendingProtests()
        => OkR(await _protest.GetPendingAsync());

    [HttpPost("protests/{id:guid}/rule")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RuleProtest(Guid id, RuleProtestRequest r)
        => OkR(await _protest.RuleAsync(id, r, GetUserId()));

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
