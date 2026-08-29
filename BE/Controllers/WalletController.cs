using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

/// <summary>
/// Quản lý số dư và các truy vấn ví của người dùng.
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IPrizeService _prizeService;

    public WalletController(IWalletService walletService, IPrizeService prizeService)
    {
        _walletService = walletService;
        _prizeService = prizeService;
    }

    /// <summary>
    /// Truy vấn số dư ví hiện tại của người dùng đang đăng nhập.
    /// </summary>
    /// <returns>Thông tin số dư khả dụng (balance).</returns>
    [HttpGet("balance")]
    public async Task<ActionResult> GetBalance()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _walletService.GetBalanceAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lịch sử nhận thưởng của Chủ ngựa đang đăng nhập — mới nhất trước.
    /// </summary>
    /// <returns>Danh sách các lần trao thưởng đã nhận thật (không bao gồm Skipped/Errors).</returns>
    [HttpGet("my-prize-history")]
    [Authorize(Roles = "HorseOwner")]
    public async Task<ActionResult> GetMyPrizeHistory()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(uid, out var userId)) return Unauthorized();
        var result = await _prizeService.GetMyPrizeHistoryAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }
}
