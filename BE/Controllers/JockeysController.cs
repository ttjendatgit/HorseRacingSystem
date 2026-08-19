using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/jockeys")]
[Authorize]
public class JockeysController : ControllerBase
{
    private readonly IJockeyService _jockeyService;

    public JockeysController(IJockeyService jockeyService)
    {
        _jockeyService = jockeyService;
    }

    [HttpGet]
    [Authorize(Roles = "HorseOwner,Jockey,Admin")]
    public async Task<ActionResult> GetAvailableJockeys()
    {
        var includeUnapproved = User.IsInRole("Admin");
        var result = await _jockeyService.GetAvailableJockeysAsync(GetUserId(), includeUnapproved);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("invitations")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> GetInvitations()
    {
        var userId = GetUserId();
        var result = await _jockeyService.GetInvitationsAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("invitations/{id:guid}/respond")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> RespondInvitation(Guid id, JockeyInvitationRespondRequest request)
    {
        var userId = GetUserId();
        var result = await _jockeyService.RespondInvitationAsync(userId, id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("invitations/{id:guid}/withdraw")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> WithdrawInvitation(Guid id, JockeyInvitationWithdrawRequest request)
    {
        var userId = GetUserId();
        var result = await _jockeyService.WithdrawInvitationAsync(userId, id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("races")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> GetAssignedRaces()
    {
        var userId = GetUserId();
        var result = await _jockeyService.GetAssignedRacesAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> GetMyProfile()
    {
        var userId = GetUserId();
        var result = await _jockeyService.GetMyProfileAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("entries/pending")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> GetPendingEntries()
    {
        var userId = GetUserId();
        var result = await _jockeyService.GetPendingRaceEntriesAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("entries/{entryId:guid}/confirm")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> ConfirmEntry(Guid entryId)
    {
        var userId = GetUserId();
        var result = await _jockeyService.ConfirmRaceEntryAsync(userId, entryId);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("entries/{entryId:guid}/decline")]
    [Authorize(Roles = "Jockey")]
    public async Task<ActionResult> DeclineEntry(Guid entryId)
    {
        var userId = GetUserId();
        var result = await _jockeyService.DeclineRaceEntryAsync(userId, entryId);
        return StatusCode(result.StatusCode, result.Result);
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdValue == null ? Guid.Empty : Guid.Parse(userIdValue);
    }
}
