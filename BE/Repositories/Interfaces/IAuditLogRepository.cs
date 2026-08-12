using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;
using HorseRacing.Dtos;

namespace HorseRacing.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> GetByIdAsync(Guid id);
    Task<List<AuditLog>> GetByAdminIdAsync(Guid adminId);
    Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId);
    Task<List<AuditLog>> GetByActionAsync(AuditAction action);
    Task<List<AuditLog>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<List<AuditLog>> GetByUserIdAsync(Guid userId);
    Task<List<AuditLog>> GetAllAsync();
    Task<List<AuditLog>> GetWithFilterAsync(AuditLogFilterDto filter);
    Task AddAsync(AuditLog auditLog);
    Task UpdateAsync(AuditLog auditLog);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<int> GetCountAsync();
    Task<int> GetCountByEntityTypeAsync(string entityType);
    Task DeleteOldLogsAsync(int daysOlder);
    Task<List<AuditLog>> GetLatestLogsAsync(int count);
}
