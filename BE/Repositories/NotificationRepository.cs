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

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> GetByIdAsync(Guid id)
    {
        return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }

    public async Task<List<Notification>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetByUserIdWithFilterAsync(Guid userId, NotificationFilterDto filter)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted);

        if (filter.IsRead.HasValue)
            query = query.Where(n => n.IsRead == filter.IsRead.Value);

        if (filter.Type.HasValue)
            query = query.Where(n => n.Type == filter.Type.Value);

        if (filter.Category.HasValue)
            query = query.Where(n => n.Category == filter.Category.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(n => n.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(n => n.CreatedAt <= filter.ToDate.Value);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetUnsentNotificationsAsync()
    {
        return await _context.Notifications
            .Where(n => !n.IsSent && !n.IsDeleted && n.RetryCount < 3)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetAllAsync()
    {
        return await _context.Notifications
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetByEntityAsync(string entityType, Guid entityId)
    {
        return await _context.Notifications
            .Where(n => n.RelatedEntityType == entityType && n.RelatedEntityId == entityId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
    }

    public async Task UpdateAsync(Notification notification)
    {
        _context.Notifications.Update(notification);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var notification = await GetByIdAsync(id);
        if (notification != null)
        {
            notification.IsDeleted = true;
            _context.Notifications.Update(notification);
        }
    }

    public async Task DeleteAllByUserIdAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsDeleted = true;
        }

        _context.Notifications.UpdateRange(notifications);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await GetByIdAsync(id);
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            _context.Notifications.Update(notification);
        }
    }

    public async Task MarkMultipleAsReadAsync(List<Guid> ids)
    {
        var notifications = await _context.Notifications
            .Where(n => ids.Contains(n.Id) && !n.IsDeleted)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        _context.Notifications.UpdateRange(notifications);
    }

    public async Task MarkAsSentAsync(Guid id)
    {
        var notification = await GetByIdAsync(id);
        if (notification != null)
        {
            notification.IsSent = true;
            notification.SentAt = DateTime.UtcNow;
            _context.Notifications.Update(notification);
        }
    }

    public async Task<int> GetUnreadCountByUserIdAsync(Guid userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted);
    }

    public async Task<List<Notification>> GetNotificationsByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        return await _context.Notifications
            .Where(n => n.CreatedAt >= fromDate && n.CreatedAt <= toDate && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteOldNotificationsAsync(int daysBefore)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBefore);
        var oldNotifications = await _context.Notifications
            .Where(n => n.CreatedAt < cutoffDate && n.IsRead)
            .ToListAsync();

        foreach (var notification in oldNotifications)
        {
            notification.IsDeleted = true;
        }

        _context.Notifications.UpdateRange(oldNotifications);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Notifications.AnyAsync(n => n.Id == id && !n.IsDeleted);
    }
}
