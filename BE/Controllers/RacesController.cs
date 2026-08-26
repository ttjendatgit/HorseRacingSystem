using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/races")]
public class RacesController : ControllerBase
{
    private readonly IRaceService _raceService;
    private readonly IRaceManagementService _raceManagementService;
    private readonly IRefereeService _refereeService;

    public RacesController(IRaceService raceService, IRaceManagementService raceManagementService, IRefereeService refereeService)
    {
        _raceService = raceService;
        _raceManagementService = raceManagementService;
        _refereeService = refereeService;
    }

    /// <summary>
    /// Lấy danh sách tất cả các trận đua ngựa công khai trong hệ thống.
    /// </summary>
    /// <returns>Danh sách các trận đua.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaces()
    {
        var result = await _raceService.GetRacesAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một trận đua theo mã GUID định danh.
    /// </summary>
    /// <param name="id">Mã GUID định danh trận đua.</param>
    /// <returns>Thông tin chi tiết trận đua.</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRace(Guid id)
    {
        var result = await _raceService.GetRaceAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy bảng kết quả thi đấu chính thức của trận đua sau khi hoàn thành.
    /// </summary>
    /// <param name="id">Mã GUID định danh trận đua.</param>
    /// <returns>Bảng thứ hạng và kết quả thi đấu chính thức.</returns>
    [HttpGet("{id:guid}/result")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRaceResult(Guid id)
    {
        var result = await _raceService.GetRaceResultAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Giải phóng (hủy đăng ký) một con ngựa khỏi trận đua cụ thể dành cho Admin.
    /// </summary>
    /// <param name="id">Mã GUID trận đua.</param>
    /// <param name="horseId">Mã GUID con ngựa cần giải phóng.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện.</returns>
    [HttpDelete("{id:guid}/horses/{horseId:guid}/release")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ReleaseHorse(Guid id, Guid horseId)
    {
        var result = await _raceService.ReleaseHorseAsync(id, horseId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật thông tin thi đấu của trận đua (thời gian, cổng xuất phát, trạng thái).
    /// </summary>
    /// <param name="id">Mã GUID trận đua.</param>
    /// <param name="request">Thông tin cập nhật trận đua.</param>
    /// <returns>Dữ liệu trận đua sau khi cập nhật.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateRace(Guid id, [FromBody] UpdateRaceRequest request)
    {
        var result = await _raceManagementService.UpdateRaceAsync(id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Xóa một trận đua chưa diễn ra khỏi hệ thống dành cho Admin.
    /// </summary>
    /// <param name="id">Mã GUID trận đua cần xóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả xóa.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteRace(Guid id)
    {
        var result = await _raceManagementService.DeleteRaceAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Phân công trọng tài chịu trách nhiệm giám sát trận đua cụ thể.
    /// </summary>
    /// <param name="id">Mã GUID trận đua.</param>
    /// <param name="request">Thông tin trọng tài được phân công.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả phân công.</returns>
    [HttpPost("{id:guid}/referees")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AssignReferee(Guid id, [FromBody] AssignRefereeRequest request)
    {
        request.RaceId = id;
        var result = await _refereeService.AssignRefereeToRaceAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

}
