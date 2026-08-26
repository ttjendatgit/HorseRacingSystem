using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Controllers;

/// <summary>
/// Quản lý việc truy vấn, lọc, xuất báo cáo và dọn dẹp nhật ký kiểm toán (Audit Logs) của hệ thống.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(IAuditLogService auditLogService, ILogger<AuditLogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bản ghi nhật ký kiểm toán theo mã GUID định danh.
    /// </summary>
    /// <param name="id">Mã GUID bản ghi nhật ký.</param>
    /// <returns>Chi tiết nhật ký kiểm toán.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogByIdAsync(id);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            if (result.StatusCode == 404)
                return NotFound(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy thông tin nhật ký kiểm toán: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả các hành động thao tác hệ thống do một tài khoản Admin cụ thể thực hiện.
    /// </summary>
    /// <param name="adminId">Mã GUID tài khoản Admin.</param>
    /// <returns>Danh sách nhật ký thao tác của Admin.</returns>
    [HttpGet("admin/{adminId}")]
    public async Task<IActionResult> GetAuditLogsByAdmin(Guid adminId)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsByAdminAsync(adminId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy nhật ký của Admin: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Lấy lịch sử biến động nhật ký của một đối tượng cụ thể (Ví dụ: Giải đấu, Trận đua, Ngựa).
    /// </summary>
    /// <param name="entityType">Tên loại đối tượng (Tournament, Race, Horse, User).</param>
    /// <param name="entityId">Mã GUID của đối tượng.</param>
    /// <returns>Lịch sử thay đổi của đối tượng.</returns>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetAuditLogsByEntity(string entityType, Guid entityId)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsByEntityAsync(entityType, entityId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy nhật ký đối tượng: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Tìm kiếm và lọc danh sách nhật ký kiểm toán nâng cao theo nhiều tiêu chí kết hợp.
    /// </summary>
    /// <param name="filter">Bộ lọc chứa từ khóa, loại hành động, thời gian và phân trang.</param>
    /// <returns>Danh sách nhật ký đã được lọc.</returns>
    [HttpPost("filter")]
    public async Task<IActionResult> GetAuditLogsWithFilter([FromBody] AuditLogFilterDto filter)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsWithFilterAsync(filter);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lọc nhật ký kiểm toán: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi lọc nhật ký" });
        }
    }

    /// <summary>
    /// Lấy danh sách nhật ký kiểm toán trong khoảng thời gian từ ngày bắt đầu đến ngày kết thúc.
    /// </summary>
    /// <param name="fromDate">Thời điểm bắt đầu truy vấn.</param>
    /// <param name="toDate">Thời điểm kết thúc truy vấn.</param>
    /// <returns>Danh sách nhật ký trong khoảng thời gian chỉ định.</returns>
    [HttpGet("date-range")]
    public async Task<IActionResult> GetAuditLogsByDateRange([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsByDateRangeAsync(fromDate, toDate);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy nhật ký theo khoảng thời gian: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Lấy toàn bộ lịch sử tác động đến dữ liệu cá nhân của một người dùng.
    /// </summary>
    /// <param name="userId">Mã GUID của người dùng.</param>
    /// <returns>Lịch sử nhật ký người dùng.</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetAuditLogsByUser(Guid userId)
    {
        try
        {
            var result = await _auditLogService.GetAuditLogsByUserAsync(userId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy nhật ký theo người dùng: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Lấy tổng quan các chỉ số thống kê về tần suất ghi nhật ký hệ thống.
    /// </summary>
    /// <returns>Bảng tổng hợp thống kê nhật ký kiểm toán.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetAuditStats()
    {
        try
        {
            var result = await _auditLogService.GetAuditStatsAsync();

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy thống kê nhật ký: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thống kê" });
        }
    }

    /// <summary>
    /// Lấy danh sách N bản ghi nhật ký kiểm toán mới nhất vừa phát sinh trong hệ thống.
    /// </summary>
    /// <param name="count">Số lượng bản ghi tối đa cần lấy (Mặc định 100, tối đa 1000).</param>
    /// <returns>Danh sách nhật ký mới nhất.</returns>
    [HttpGet("latest/{count}")]
    public async Task<IActionResult> GetLatestAuditLogs(int count)
    {
        try
        {
            if (count <= 0 || count > 1000)
                count = 100;

            var result = await _auditLogService.GetLatestLogsAsync(count);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi lấy danh sách nhật ký mới nhất: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Xuất dữ liệu nhật ký kiểm toán ra tập tin báo cáo định dạng CSV hoặc JSON.
    /// </summary>
    /// <param name="dto">Dữ liệu yêu cầu xuất báo cáo (Định dạng CSV/JSON, khoảng thời gian).</param>
    /// <returns>Tập tin nhật ký dạng dữ liệu nhị phân để tải về.</returns>
    [HttpPost("export")]
    public async Task<IActionResult> ExportAuditLogs([FromBody] AuditExportDto dto)
    {
        try
        {
            var result = await _auditLogService.ExportAuditLogsAsync(dto);

            if (result.StatusCode == 200)
            {
                var contentType = dto.Format.ToLower() == "csv" ? "text/csv" : "application/json";
                var filename = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{dto.Format}";
                return File(System.Text.Encoding.UTF8.GetBytes(result.Result.Data), contentType, filename);
            }

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi xuất nhật ký: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi xuất nhật ký" });
        }
    }

    /// <summary>
    /// Xóa tự động các bản ghi nhật ký kiểm toán quá cũ để giải phóng dung lượng cơ sở dữ liệu.
    /// </summary>
    /// <param name="daysOlder">Số ngày tồn tại tối đa của bản ghi nhật ký (Mặc định trên 365 ngày).</param>
    /// <returns>Mã trạng thái HTTP báo số lượng bản ghi đã được dọn dẹp thành công.</returns>
    [HttpDelete("cleanup")]
    public async Task<IActionResult> DeleteOldAuditLogs([FromQuery] int daysOlder = 365)
    {
        try
        {
            if (daysOlder < 1)
                daysOlder = 365;

            var result = await _auditLogService.DeleteOldAuditLogsAsync(daysOlder);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi xóa nhật ký cũ: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi xóa nhật ký" });
        }
    }
}
