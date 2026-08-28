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
    private readonly IRaceManagementService _raceManagementService;

    public TournamentsController(
        ITournamentService tournamentService, IRoundService roundService, IRaceManagementService raceManagementService)
    {
        _tournamentService = tournamentService;
        _roundService = roundService;
        _raceManagementService = raceManagementService;
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

    /// <summary>
    /// Tạo một giải đấu mới ở trạng thái Nháp (Draft) với các thông tin cấu hình ban đầu.
    /// </summary>
    /// <param name="request">Thông tin khởi tạo giải đấu gồm tên, mô tả, ngày bắt đầu và kết thúc.</param>
    /// <returns>Mã trạng thái HTTP và thông tin giải đấu đã được tạo thành công.</returns>
    [HttpPost]
    public async Task<ActionResult> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        var result = await _tournamentService.CreateTournamentAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách tất cả các giải đấu trong hệ thống. Tự động ẩn các giải đấu Nháp nếu không phải Admin.
    /// </summary>
    /// <returns>Danh sách các giải đấu người dùng có quyền truy cập.</returns>
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

    /// <summary>
    /// Lấy danh sách các giải đấu đang diễn ra hoặc chuẩn bị mở đăng ký / dự đoán.
    /// </summary>
    /// <returns>Mã trạng thái HTTP và danh sách giải đấu khả dụng.</returns>
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult> GetActiveTournaments()
    {
        var result = await _tournamentService.GetActiveTournamentsAsync();
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một giải đấu theo mã GUID định danh.
    /// </summary>
    /// <param name="id">Mã GUID duy nhất của giải đấu.</param>
    /// <returns>Thông tin chi tiết giải đấu hoặc phản hồi 404 Không tìm thấy.</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTournament(Guid id)
    {
        var result = await _tournamentService.GetTournamentAsync(id);
        if (!IsAdminCaller() && result.Result.Success && IsDraftTournament(result.Result.Data))
            return NotFound(ApiResult<TournamentResponse>.Fail("Không tìm thấy giải đấu"));

        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật thông tin chi tiết giải đấu như tên, thời gian, địa điểm đường đua hoặc mô tả.
    /// </summary>
    /// <param name="id">Mã GUID của giải đấu cần cập nhật.</param>
    /// <param name="request">Dữ liệu thông tin giải đấu mới.</param>
    /// <returns>Mã trạng thái HTTP và dữ liệu giải đấu sau khi cập nhật.</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateTournament(Guid id, [FromBody] UpdateTournamentRequest request)
    {
        var result = await _tournamentService.UpdateTournamentAsync(id, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Xóa một giải đấu ở trạng thái Nháp khỏi hệ thống.
    /// </summary>
    /// <param name="id">Mã GUID của giải đấu cần xóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện xóa.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTournament(Guid id)
    {
        var result = await _tournamentService.DeleteTournamentAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    // State machine endpoints
    /// <summary>
    /// Thay đổi trạng thái vòng đời của giải đấu (Nháp -> Mở đăng ký -> Đang diễn ra -> Hoàn thành).
    /// </summary>
    /// <param name="id">Mã GUID của giải đấu mục tiêu.</param>
    /// <param name="request">Thông tin trạng thái mới cần chuyển đổi.</param>
    /// <returns>Mã trạng thái HTTP và kết quả chuyển đổi trạng thái.</returns>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult> ChangeTournamentStatus(Guid id, [FromBody] ChangeTournamentStatusRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return StatusCode(401, new { success = false, message = "Không thể xác định người dùng thực hiện thao tác." });

        var result = await _tournamentService.ChangeStatusAsync(id, request, actorId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy dữ liệu thống kê tổng quan của giải đấu (số lượng ngựa đăng ký, số vòng thi, số lượt đua và dự đoán).
    /// </summary>
    /// <param name="id">Mã GUID của giải đấu.</param>
    /// <returns>Dữ liệu thống kê tổng quan của giải đấu.</returns>
    [HttpGet("{id:guid}/stats")]
    [AllowAnonymous]
    public async Task<ActionResult> GetTournamentStats(Guid id)
    {
        if (await ShouldHideDraftTournamentAsync(id))
            return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));

        var result = await _tournamentService.GetStatsAsync(id);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy tiến trình thời gian (Timeline) theo thứ tự mốc sự kiện của giải đấu.
    /// </summary>
    /// <param name="id">Mã GUID của giải đấu.</param>
    /// <returns>Danh sách các mốc thời gian sự kiện của giải đấu.</returns>
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
    /// <summary>
    /// Tạo mới một vòng thi đấu trong giải (Ví dụ: Vòng loại 1, Vòng Tứ kết, Vòng Chung kết).
    /// </summary>
    /// <param name="tournamentId">Mã GUID của giải đấu sở hữu.</param>
    /// <param name="request">Thông tin cấu hình vòng thi đấu mới.</param>
    /// <returns>Dữ liệu vòng thi đấu vừa được tạo.</returns>
    [HttpPost("{tournamentId:guid}/rounds")]
    public async Task<ActionResult> CreateRound(Guid tournamentId, [FromBody] CreateRoundRequest request)
    {
        if (request.TournamentId != Guid.Empty && request.TournamentId != tournamentId)
            return BadRequest(new { message = "TournamentId không khớp với route" });
        request.TournamentId = tournamentId;

        var result = await _roundService.CreateRoundAsync(request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy danh sách tất cả các vòng thi đấu được cấu hình thuộc về một giải đấu.
    /// </summary>
    /// <param name="tournamentId">Mã GUID của giải đấu mục tiêu.</param>
    /// <returns>Danh sách các vòng thi đấu thuộc giải.</returns>
    [HttpGet("{tournamentId:guid}/rounds")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRoundsByTournament(Guid tournamentId)
    {
        if (await ShouldHideDraftTournamentAsync(tournamentId))
            return NotFound(ApiResult<object>.Fail("Không tìm thấy giải đấu"));

        var result = await _roundService.GetRoundsByTournamentAsync(tournamentId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một vòng thi đấu theo mã GUID định danh vòng.
    /// </summary>
    /// <param name="roundId">Mã GUID duy nhất của vòng thi đấu.</param>
    /// <returns>Thông tin chi tiết vòng thi đấu.</returns>
    [HttpGet("rounds/{roundId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetRound(Guid roundId)
    {
        var result = await _roundService.GetRoundAsync(roundId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật cấu hình hoặc tên gọi của một vòng thi đấu hiện có.
    /// </summary>
    /// <param name="roundId">Mã GUID duy nhất của vòng thi đấu.</param>
    /// <param name="request">Dữ liệu cập nhật vòng thi đấu.</param>
    /// <returns>Dữ liệu vòng thi đấu sau khi cập nhật.</returns>
    [HttpPut("rounds/{roundId:guid}")]
    public async Task<ActionResult> UpdateRound(Guid roundId, [FromBody] UpdateRoundRequest request)
    {
        var result = await _roundService.UpdateRoundAsync(roundId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Xóa một vòng thi đấu và các trận đua chưa bắt đầu liên quan.
    /// </summary>
    /// <param name="roundId">Mã GUID của vòng thi đấu cần xóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện xóa vòng.</returns>
    [HttpDelete("rounds/{roundId:guid}")]
    public async Task<ActionResult> DeleteRound(Guid roundId)
    {
        var result = await _roundService.DeleteRoundAsync(roundId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Tự động tổng hợp và xếp danh sách các ngựa đủ điều kiện đi tiếp từ vòng trước sang vòng tiếp theo.
    /// Quá trình tính toán hoàn toàn thực hiện tự động ở phía server dựa trên kết quả chính thức.
    /// </summary>
    /// <param name="roundId">Mã GUID của vòng thi đấu nguồn.</param>
    /// <returns>Kết quả danh sách ngựa tham gia lượt đua ở vòng tiếp theo.</returns>
    [HttpPost("rounds/{roundId:guid}/generate-next")]
    public async Task<ActionResult> GenerateNextRoundEntries(Guid roundId, [FromQuery] bool confirmShortfall = false)
    {
        var result = await _raceManagementService.GenerateNextRoundEntriesAsync(roundId, confirmShortfall);
        return StatusCode(result.StatusCode, result.Result);
    }
}
