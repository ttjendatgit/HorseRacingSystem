using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IAuditLogService
{
    Task<ServiceResult<AuditLogDto>> LogActionAsync(CreateAuditLogDto dto);
    Task<ServiceResult<AuditLogDetailDto>> GetAuditLogByIdAsync(Guid id);
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByAdminAsync(Guid adminId);
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByEntityAsync(string entityType, Guid entityId);
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsWithFilterAsync(AuditLogFilterDto filter);
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByUserAsync(Guid userId);
    Task<ServiceResult<AuditLogStatsDto>> GetAuditStatsAsync();
    Task<ServiceResult<bool>> DeleteOldAuditLogsAsync(int daysOlder);
    Task<ServiceResult<string>> ExportAuditLogsAsync(AuditExportDto dto);
    Task<ServiceResult<List<AuditLogDto>>> GetLatestLogsAsync(int count);
}
