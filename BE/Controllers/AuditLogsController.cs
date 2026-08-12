using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Controllers;

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
    /// Get audit log by ID
    /// </summary>
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
            _logger.LogError($"Error getting audit log: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Get audit logs by admin
    /// </summary>
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
            _logger.LogError($"Error getting audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Get audit logs by entity
    /// </summary>
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
            _logger.LogError($"Error getting audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Get audit logs with filters
    /// </summary>
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
            _logger.LogError($"Error filtering audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi lọc nhật ký" });
        }
    }

    /// <summary>
    /// Get audit logs by date range
    /// </summary>
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
            _logger.LogError($"Error getting audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Get audit logs by user
    /// </summary>
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
            _logger.LogError($"Error getting audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Get audit statistics
    /// </summary>
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
            _logger.LogError($"Error getting audit stats: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thống kê" });
        }
    }

    /// <summary>
    /// Get latest audit logs
    /// </summary>
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
            _logger.LogError($"Error getting latest audit logs: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất nhật ký" });
        }
    }

    /// <summary>
    /// Export audit logs
    /// </summary>
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
    /// Delete old audit logs
    /// </summary>
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
