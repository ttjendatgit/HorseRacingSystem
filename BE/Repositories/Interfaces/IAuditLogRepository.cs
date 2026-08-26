using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;
using HorseRacing.Dtos;

namespace HorseRacing.Repositories.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>
    /// Lấy thông tin nhật ký thay đổi theo mã định danh.
    /// </summary>
    Task<AuditLog> GetByIdAsync(Guid id);

    /// <summary>
    /// Lấy danh sách nhật ký thay đổi do một Quản trị viên cụ thể thực hiện.
    /// </summary>
    Task<List<AuditLog>> GetByAdminIdAsync(Guid adminId);

    /// <summary>
    /// Lấy lịch sử nhật ký thay đổi liên quan đến một đối tượng thực thể cụ thể.
    /// </summary>
    Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId);

    /// <summary>
    /// Lấy danh sách nhật ký thay đổi theo loại hành động (Thêm, Sửa, Xóa, Phê duyệt).
    /// </summary>
    Task<List<AuditLog>> GetByActionAsync(AuditAction action);

    /// <summary>
    /// Lấy danh sách nhật ký thay đổi trong khoảng thời gian chỉ định.
    /// </summary>
    Task<List<AuditLog>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Lấy danh sách nhật ký thay đổi tác động đến một người dùng.
    /// </summary>
    Task<List<AuditLog>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy toàn bộ danh sách nhật ký thay đổi trong hệ thống.
    /// </summary>
    Task<List<AuditLog>> GetAllAsync();

    /// <summary>
    /// Lấy danh sách nhật ký thay đổi theo bộ lọc nâng cao.
    /// </summary>
    Task<List<AuditLog>> GetWithFilterAsync(AuditLogFilterDto filter);

    /// <summary>
    /// Thêm mới một nhật ký thay đổi vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(AuditLog auditLog);

    /// <summary>
    /// Cập nhật nhật ký thay đổi trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(AuditLog auditLog);

    /// <summary>
    /// Xóa một nhật ký thay đổi theo mã định danh.
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Kiểm tra xem nhật ký thay đổi có tồn tại hay không.
    /// </summary>
    Task<bool> ExistsAsync(Guid id);

    /// <summary>
    /// Lấy tổng số lượng bản ghi nhật ký thay đổi trong hệ thống.
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Lấy tổng số lượng bản ghi nhật ký thay đổi theo loại thực thể.
    /// </summary>
    Task<int> GetCountByEntityTypeAsync(string entityType);

    /// <summary>
    /// Xóa các nhật ký thay đổi cũ hơn số ngày chỉ định.
    /// </summary>
    Task DeleteOldLogsAsync(int daysOlder);

    /// <summary>
    /// Lấy danh sách các nhật ký thay đổi mới nhất theo số lượng chỉ định.
    /// </summary>
    Task<List<AuditLog>> GetLatestLogsAsync(int count);
}
