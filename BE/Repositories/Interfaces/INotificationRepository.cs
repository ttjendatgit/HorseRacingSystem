using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;
using HorseRacing.Dtos;

namespace HorseRacing.Repositories.Interfaces;

public interface INotificationRepository
{
    Task<Notification> GetByIdAsync(Guid id);
    Task<List<Notification>> GetByUserIdAsync(Guid userId);
    Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId);
    Task<List<Notification>> GetByUserIdWithFilterAsync(Guid userId, NotificationFilterDto filter);
    Task<List<Notification>> GetUnsentNotificationsAsync();
    Task<List<Notification>> GetAllAsync();
    Task<List<Notification>> GetByEntityAsync(string entityType, Guid entityId);
    Task AddAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task DeleteAsync(Guid id);
    Task DeleteAllByUserIdAsync(Guid userId);
    Task MarkAsReadAsync(Guid id);
    Task MarkMultipleAsReadAsync(List<Guid> ids);
    Task MarkAsSentAsync(Guid id);
    Task<int> GetUnreadCountByUserIdAsync(Guid userId);
    Task<List<Notification>> GetNotificationsByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task DeleteOldNotificationsAsync(int daysBefore);
    Task<bool> ExistsAsync(Guid id);
}
