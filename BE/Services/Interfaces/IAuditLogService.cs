using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ ghi nhận, tra cứu và quản lý nhật ký kiểm toán hệ thống.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Ghi nhận một hành động thao tác mới vào nhật ký kiểm toán.</summary>
    Task<ServiceResult<AuditLogDto>> LogActionAsync(CreateAuditLogDto dto);

    /// <summary>Lấy chi tiết một nhật ký kiểm toán theo mã GUID.</summary>
    Task<ServiceResult<AuditLogDetailDto>> GetAuditLogByIdAsync(Guid id);

    /// <summary>Lấy danh sách nhật ký thao tác của một Admin cụ thể.</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByAdminAsync(Guid adminId);

    /// <summary>Lấy lịch sử biến động của một đối tượng cụ thể (Giải đấu, Trận đua, Ngựa...).</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByEntityAsync(string entityType, Guid entityId);

    /// <summary>Lọc nhật ký kiểm toán theo nhiều tiêu chí tìm kiếm nâng cao.</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsWithFilterAsync(AuditLogFilterDto filter);

    /// <summary>Lấy nhật ký kiểm toán trong khoảng thời gian chỉ định.</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>Lấy nhật ký liên quan đến một người dùng cụ thể.</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByUserAsync(Guid userId);

    /// <summary>Lấy các chỉ số thống kê về tần suất ghi nhật ký hệ thống.</summary>
    Task<ServiceResult<AuditLogStatsDto>> GetAuditStatsAsync();

    /// <summary>Xóa tự động các nhật ký kiểm toán quá cũ để giảm tải dữ liệu.</summary>
    Task<ServiceResult<bool>> DeleteOldAuditLogsAsync(int daysOlder);

    /// <summary>Xuất danh sách nhật ký kiểm toán ra file báo cáo CSV/JSON.</summary>
    Task<ServiceResult<string>> ExportAuditLogsAsync(AuditExportDto dto);

    /// <summary>Lấy danh sách N bản ghi nhật ký mới nhất vừa phát sinh.</summary>
    Task<ServiceResult<List<AuditLogDto>>> GetLatestLogsAsync(int count);
}
