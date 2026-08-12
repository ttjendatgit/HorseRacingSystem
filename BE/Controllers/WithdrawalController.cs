using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/withdrawal")]
public class WithdrawalController : ControllerBase
{
    private readonly IWithdrawalService _withdrawalService;

    public WithdrawalController(IWithdrawalService withdrawalService)
    {
        _withdrawalService = withdrawalService;
    }

    [Authorize]
    [HttpPost("bank-account")]
    public async Task<ActionResult> SaveBankAccount(BankAccountRequest request)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var result = await _withdrawalService.SaveBankAccountAsync(uid.Value, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpGet("bank-accounts")]
    public async Task<ActionResult> GetBankAccounts()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var result = await _withdrawalService.GetBankAccountsAsync(uid.Value);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpPost("create")]
    public async Task<ActionResult> CreateWithdrawal(WithdrawalRequestDto request)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var result = await _withdrawalService.CreateWithdrawalAsync(uid.Value, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<ActionResult> GetHistory()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var result = await _withdrawalService.GetHistoryAsync(uid.Value);
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/pending")]
    public async Task<ActionResult> GetPending()
    {
        var result = await _withdrawalService.GetPendingAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/all")]
    public async Task<ActionResult> GetAll()
    {
        var result = await _withdrawalService.GetAllAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/process")]
    public async Task<ActionResult> ProcessWithdrawal(AdminProcessWithdrawalRequest request)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var result = await _withdrawalService.ProcessWithdrawalAsync(uid.Value, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var uid)) return uid;
        return null;
    }
}
