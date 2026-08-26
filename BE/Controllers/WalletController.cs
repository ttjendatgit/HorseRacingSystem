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

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
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
}
