using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ tạo, gửi, tra cứu và đánh dấu đã đọc thông báo hệ thống.
/// </summary>
public interface INotificationService
{
    /// <summary>Tạo mới một thông báo cho người dùng.</summary>
    Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto);

    /// <summary>Lấy danh sách thông báo của một người dùng.</summary>
    Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId);

    /// <summary>Lấy danh sách các thông báo chưa đọc của người dùng.</summary>
    Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId);

    /// <summary>Lọc danh sách thông báo theo tiêu chí phân loại.</summary>
    Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter);

    /// <summary>Lấy thông tin chi tiết một thông báo theo ID.</summary>
    Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id);

    /// <summary>Đánh dấu một thông báo là Đã đọc.</summary>
    Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId);

    /// <summary>Đánh dấu nhiều thông báo cùng lúc là Đã đọc.</summary>
    Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto);

    /// <summary>Xóa một thông báo theo ID.</summary>
    Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId);

    /// <summary>Xóa toàn bộ tất cả thông báo của người dùng.</summary>
    Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId);

    /// <summary>Lấy số lượng thông báo chưa đọc hiện tại của người dùng.</summary>
    Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId);

    /// <summary>Lấy các chỉ số thống kê thông báo của người dùng.</summary>
    Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId);

    /// <summary>Gửi thông báo hàng loạt đến nhóm người dùng chỉ định.</summary>
    Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto);

    /// <summary>Lấy danh sách thông báo liên quan đến một đối tượng cụ thể.</summary>
    Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId);

    /// <summary>Xử lý gửi lại các thông báo bị nghẽn chưa gửi thành công.</summary>
    Task ProcessUnsentNotificationsAsync();
}
