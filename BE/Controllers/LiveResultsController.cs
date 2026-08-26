using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

/// <summary>
/// Quản lý dữ liệu thời gian thực (Live Results) và thứ hạng trực tiếp của các trận đua đang diễn ra.
/// </summary>
[ApiController]
[Route("api/live-results")]
public class LiveResultsController : ControllerBase
{
    private readonly ILiveResultService _liveResultService;

    public LiveResultsController(ILiveResultService liveResultService)
    {
        _liveResultService = liveResultService;
    }

    /// <summary>
    /// Lấy diễn biến thi đấu và kết quả thời gian thực của một trận đua đang diễn ra.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Dữ liệu diễn biến trận đua trực tiếp.</returns>
    [HttpGet("race/{raceId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetLiveRaceResult(Guid raceId)
    {
        var result = await _liveResultService.GetLiveRaceResultAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy vị trí tọa độ thời gian thực của tất cả các ngựa trên đường đua.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Tọa độ và khoảng cách các con ngựa trên đường đua.</returns>
    [HttpGet("race/{raceId:guid}/positions")]
    [AllowAnonymous]
    public async Task<ActionResult> GetCurrentPositions(Guid raceId)
    {
        var result = await _liveResultService.GetCurrentPositionsAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy bảng xếp hạng tạm thời thời gian thực của trận đua đang diễn ra.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <returns>Bảng thứ hạng thời gian thực.</returns>
    [HttpGet("race/{raceId:guid}/ranking")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceRanking(Guid raceId)
    {
        var result = await _liveResultService.GetRaceRankingAsync(raceId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật kết quả về đích thời gian thực do Admin hoặc Trọng tài nhập liệu.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="request">Bảng kết quả thi đấu vừa cập nhật.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả cập nhật.</returns>
    [HttpPost("race/{raceId:guid}/result")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateRaceResult(Guid raceId, [FromBody] RaceResultRequest request)
    {
        var result = await _liveResultService.UpdateRaceResultAsync(raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật trạng thái thi đấu của một con ngựa cụ thể trong trận đua (Đang đua, Bỏ cuộc, Phạm quy, Về đích).
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="horseId">Mã GUID con ngựa thi đấu.</param>
    /// <param name="request">Trạng thái thi đấu mới.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện.</returns>
    [HttpPut("race/{raceId:guid}/participant/{horseId:guid}/status")]
    [Authorize(Roles = "Admin,Referee")]
    public async Task<ActionResult> UpdateParticipantStatus(Guid raceId, Guid horseId, [FromBody] dynamic request)
    {
        string status = request?.status?.ToString() ?? "Không xác định";
        var result = await _liveResultService.UpdateParticipantStatusAsync(raceId, horseId, status);
        return StatusCode(result.StatusCode, result.Result);
    }
}
