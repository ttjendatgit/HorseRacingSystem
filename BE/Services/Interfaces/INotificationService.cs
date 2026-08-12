using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface INotificationService
{
    Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto);
    Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId);
    Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId);
    Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter);
    Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id);
    Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId);
    Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto);
    Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId);
    Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId);
    Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId);
    Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId);
    Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto);
    Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId);
    Task ProcessUnsentNotificationsAsync();
}
