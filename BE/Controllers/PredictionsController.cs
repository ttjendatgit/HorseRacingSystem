using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

/// <summary>
/// Quản lý các thao tác tạo và truy vấn dự đoán cuộc đua dành cho khán giả (Spectator).
/// </summary>
[ApiController]
[Route("api/predictions")]
[Authorize(Roles = "Spectator")]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;

    public PredictionsController(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    /// <summary>
    /// Tạo dự đoán mới cho một cuộc đua sắp diễn ra.
    /// </summary>
    /// <param name="request">Thông tin cược bao gồm RaceId, PredictedHorseId và BetAmount.</param>
    /// <returns>Kết quả tạo dự đoán bao gồm thông tin đặt cược thành công.</returns>
    [HttpPost]
    public async Task<ActionResult> CreatePrediction(PredictionCreateRequest request)
    {
        var userId = GetUserId();
        var result = await _predictionService.CreatePredictionAsync(userId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách lịch sử tất cả các dự đoán của người dùng hiện tại.
    /// </summary>
    /// <returns>Danh sách phiếu dự đoán kèm theo kết quả và điểm thưởng (nếu có).</returns>
    [HttpGet("mine")]
    public async Task<ActionResult> GetMyPredictions()
    {
        var userId = GetUserId();
        var result = await _predictionService.GetMyPredictionsAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Trích xuất mã định danh UserId từ ClaimTypes.NameIdentifier của token xác thực.
    /// </summary>
    /// <returns>Guid của người dùng hoặc Guid.Empty nếu không tìm thấy.</returns>
    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return value == null ? Guid.Empty : Guid.Parse(value);
    }
}
