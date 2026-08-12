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
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(uid, out var id) ? id : Guid.Empty;
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserNotifications()
    {
        try
        {
            var userId = GetUserId();
            var result = await _notificationService.GetUserNotificationsAsync(userId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting user notifications: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thông báo" });
        }
    }

    /// <summary>
    /// Get unread notifications for the current user
    /// </summary>
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        try
        {
            var userId = GetUserId();
            var result = await _notificationService.GetUnreadNotificationsAsync(userId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting unread notifications: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thông báo" });
        }
    }

    /// <summary>
    /// Get notifications with filter
    /// </summary>
    [HttpPost("filter")]
    public async Task<IActionResult> GetNotificationsWithFilter([FromBody] NotificationFilterDto filter)
    {
        try
        {
            var userId = GetUserId();
            var result = await _notificationService.GetNotificationsWithFilterAsync(userId, filter);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error filtering notifications: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi lọc thông báo" });
        }
    }

    /// <summary>
    /// Get notification by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotificationById(Guid id)
    {
        try
        {
            var result = await _notificationService.GetNotificationByIdAsync(id);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            if (result.StatusCode == 404)
                return NotFound(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting notification: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thông báo" });
        }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPut("{id}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var result = await _notificationService.MarkAsReadAsync(id);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            if (result.StatusCode == 404)
                return NotFound(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi đánh dấu thông báo đã đọc: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi đánh dấu đã đọc" });
        }
    }

    /// <summary>
    /// Mark multiple notifications as read
    /// </summary>
    [HttpPost("mark-multiple-read")]
    public async Task<IActionResult> MarkMultipleAsRead([FromBody] MarkNotificationsAsReadDto dto)
    {
        try
        {
            var result = await _notificationService.MarkMultipleAsReadAsync(dto);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi đánh dấu thông báo đã đọc: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi đánh dấu đã đọc" });
        }
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllNotifications()
    {
        try
        {
            var result = await _notificationService.DeleteAllNotificationsAsync(GetUserId());
            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xóa tất cả thông báo");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi xóa tất cả thông báo" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        try
        {
            var result = await _notificationService.DeleteNotificationAsync(id);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            if (result.StatusCode == 404)
                return NotFound(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi xóa thông báo: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi xóa thông báo" });
        }
    }

    /// <summary>
    /// Get unread notification count for current user
    /// </summary>
    [HttpGet("count/unread")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = GetUserId();
            var result = await _notificationService.GetUnreadCountAsync(userId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting unread count: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi lấy số lượng chưa đọc" });
        }
    }

    /// <summary>
    /// Get notification statistics for current user
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetNotificationStats()
    {
        try
        {
            var userId = GetUserId();
            var result = await _notificationService.GetNotificationStatsAsync(userId);

            if (result.StatusCode == 200)
                return Ok(result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting notification stats: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi truy xuất thống kê" });
        }
    }

    /// <summary>
    /// Admin endpoint - Create notification (Admin only)
    /// </summary>
    [HttpPost("create")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto dto)
    {
        try
        {
            var result = await _notificationService.CreateNotificationAsync(dto);

            if (result.StatusCode == 201)
                return StatusCode(201, result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi tạo thông báo: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi tạo thông báo" });
        }
    }

    /// <summary>
    /// Admin endpoint - Send bulk notifications
    /// </summary>
    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendBulkNotifications([FromBody] BulkNotificationDto dto)
    {
        try
        {
            var result = await _notificationService.SendBulkNotificationsAsync(dto);

            if (result.StatusCode == 201)
                return StatusCode(201, result.Result);

            return StatusCode(result.StatusCode, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Lỗi gửi thông báo hàng loạt: {ex.Message}");
            return StatusCode(500, new ApiResult<object> { Success = false, Message = "Lỗi gửi thông báo hàng loạt" });
        }
    }
}
