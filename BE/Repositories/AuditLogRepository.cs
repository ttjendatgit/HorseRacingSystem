using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Dtos;

namespace HorseRacing.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLog> GetByIdAsync(Guid id)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<AuditLog>> GetByAdminIdAsync(Guid adminId)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .Where(a => a.AdminId == adminId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByEntityAsync(string entityType, Guid entityId)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByActionAsync(AuditAction action)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .Where(a => a.Action == action)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetByUserIdAsync(Guid userId)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetAllAsync()
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetWithFilterAsync(AuditLogFilterDto filter)
    {
        var query = _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .AsQueryable();

        if (filter.AdminId.HasValue)
            query = query.Where(a => a.AdminId == filter.AdminId.Value);

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (filter.Action.HasValue)
            query = query.Where(a => a.Action == filter.Action.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= filter.ToDate.Value);

        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task AddAsync(AuditLog auditLog)
    {
        await _context.AuditLogs.AddAsync(auditLog);
    }

    public async Task UpdateAsync(AuditLog auditLog)
    {
        _context.AuditLogs.Update(auditLog);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await GetByIdAsync(id);
        if (log != null)
        {
            _context.AuditLogs.Remove(log);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.AuditLogs.AnyAsync(a => a.Id == id);
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.AuditLogs.CountAsync();
    }

    public async Task<int> GetCountByEntityTypeAsync(string entityType)
    {
        return await _context.AuditLogs.CountAsync(a => a.EntityType == entityType);
    }

    public async Task DeleteOldLogsAsync(int daysOlder)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOlder);
        var oldLogs = await _context.AuditLogs
            .Where(a => a.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.AuditLogs.RemoveRange(oldLogs);
    }

    public async Task<List<AuditLog>> GetLatestLogsAsync(int count)
    {
        return await _context.AuditLogs
            .Include(a => a.Admin)
            .Include(a => a.AffectedUser)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
