using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;
using HorseRacing.Dtos;

namespace HorseRacing.Repositories.Interfaces;

public interface INotificationRepository
{
    /// <summary>
    /// Lấy thông tin chi tiết một thông báo theo mã định danh.
    /// </summary>
    Task<Notification> GetByIdAsync(Guid id);

    /// <summary>
    /// Lấy danh sách tất cả thông báo của một người dùng theo User ID.
    /// </summary>
    Task<List<Notification>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy danh sách các thông báo chưa đọc của người dùng.
    /// </summary>
    Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy danh sách thông báo của người dùng theo bộ lọc tìm kiếm.
    /// </summary>
    Task<List<Notification>> GetByUserIdWithFilterAsync(Guid userId, NotificationFilterDto filter);

    /// <summary>
    /// Lấy danh sách các thông báo chưa gửi thành công trong hệ thống.
    /// </summary>
    Task<List<Notification>> GetUnsentNotificationsAsync();

    /// <summary>
    /// Lấy toàn bộ danh sách thông báo trong cơ sở dữ liệu.
    /// </summary>
    Task<List<Notification>> GetAllAsync();

    /// <summary>
    /// Lấy danh sách thông báo liên quan đến một đối tượng thực thể cụ thể.
    /// </summary>
    Task<List<Notification>> GetByEntityAsync(string entityType, Guid entityId);

    /// <summary>
    /// Thêm một thông báo mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Notification notification);

    /// <summary>
    /// Cập nhật thông tin thông báo trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Notification notification);

    /// <summary>
    /// Xóa một thông báo theo mã định danh.
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Xóa tất cả các thông báo thuộc về một người dùng cụ thể.
    /// </summary>
    Task DeleteAllByUserIdAsync(Guid userId);

    /// <summary>
    /// Đánh dấu một thông báo là đã đọc.
    /// </summary>
    Task MarkAsReadAsync(Guid id);

    /// <summary>
    /// Đánh dấu hàng loạt thông báo là đã đọc theo danh sách ID.
    /// </summary>
    Task MarkMultipleAsReadAsync(List<Guid> ids);

    /// <summary>
    /// Đánh dấu thông báo là đã gửi thành công.
    /// </summary>
    Task MarkAsSentAsync(Guid id);

    /// <summary>
    /// Đếm tổng số lượng thông báo chưa đọc của người dùng.
    /// </summary>
    Task<int> GetUnreadCountByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy danh sách thông báo được tạo trong khoảng thời gian chỉ định.
    /// </summary>
    Task<List<Notification>> GetNotificationsByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Xóa các thông báo cũ hơn số ngày chỉ định.
    /// </summary>
    Task DeleteOldNotificationsAsync(int daysBefore);

    /// <summary>
    /// Kiểm tra xem thông báo có tồn tại trong hệ thống hay không.
    /// </summary>
    Task<bool> ExistsAsync(Guid id);
}
