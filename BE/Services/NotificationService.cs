using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notificationRepository, IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Tạo mới và lưu trữ một thông báo hệ thống gửi đến người dùng cụ thể.
    /// </summary>
    /// <param name="dto">Thông tin nội dung thông báo.</param>
    /// <returns>Thông tin chi tiết thông báo đã khởi tạo thành công.</returns>
    public async Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
    {
        try
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = dto.UserId,
                Title = dto.Title,
                Message = dto.Message,
                Type = dto.Type,
                Category = dto.Category,
                ActionUrl = dto.ActionUrl,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                IsSent = false,
                RetryCount = 0
            };

            await _notificationRepository.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<NotificationDto>
            {
                StatusCode = 201,
                Result = new ApiResult<NotificationDto>
                {
                    Success = true,
                    Data = MapToDto(notification),
                    Message = "Đã tạo thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<NotificationDto>
            {
                StatusCode = 500,
                Result = new ApiResult<NotificationDto>
                {
                    Success = false,
                    Message = $"Lỗi tạo thông báo: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Truy xuất tất cả danh sách thông báo hệ thống cá nhân của người dùng.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng.</param>
    /// <returns>Danh sách thông báo cá nhân.</returns>
    public async Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
    {
        try
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId);
            var dtos = notifications.Select(n => MapToDto(n)).ToList();

            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy danh sách thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thông báo: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
    {
        try
        {
            var notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId);
            var dtos = notifications.Select(n => MapToDto(n)).ToList();

            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy thông báo chưa đọc"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thông báo chưa đọc: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
    {
        try
        {
            var notifications = await _notificationRepository.GetByUserIdWithFilterAsync(userId, filter);
            var dtos = notifications.Select(n => MapToDto(n)).ToList();

            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lọc thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thông báo: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null)
            {
                return new ServiceResult<NotificationDetailDto>
                {
                    StatusCode = 404,
                    Result = new ApiResult<NotificationDetailDto>
                    {
                        Success = false,
                        Message = "Không tìm thấy thông báo"
                    }
                };
            }

            return new ServiceResult<NotificationDetailDto>
            {
                StatusCode = 200,
                Result = new ApiResult<NotificationDetailDto>
                {
                    Success = true,
                    Data = MapToDetailDto(notification),
                    Message = "Đã lấy thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<NotificationDetailDto>
            {
                StatusCode = 500,
                Result = new ApiResult<NotificationDetailDto>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thông báo: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                return new ServiceResult<bool>
                {
                    StatusCode = 404,
                    Result = new ApiResult<bool>
                    {
                        Success = false,
                        Message = "Không tìm thấy thông báo"
                    }
                };
            }

            await _notificationRepository.MarkAsReadAsync(notificationId);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 200,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = "Đã đánh dấu thông báo là đã đọc"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<bool>
            {
                StatusCode = 500,
                Result = new ApiResult<bool>
                {
                    Success = false,
                    Message = $"Lỗi đánh dấu thông báo đã đọc: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto)
    {
        try
        {
            await _notificationRepository.MarkMultipleAsReadAsync(dto.NotificationIds);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 200,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = $"{dto.NotificationIds.Count} thông báo đã được đánh dấu là đã đọc"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<bool>
            {
                StatusCode = 500,
                Result = new ApiResult<bool>
                {
                    Success = false,
                    Message = $"Lỗi đánh dấu thông báo đã đọc: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                return new ServiceResult<bool>
                {
                    StatusCode = 404,
                    Result = new ApiResult<bool>
                    {
                        Success = false,
                        Message = "Không tìm thấy thông báo"
                    }
                };
            }

            await _notificationRepository.DeleteAsync(notificationId);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 200,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = "Đã xóa thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<bool>
            {
                StatusCode = 500,
                Result = new ApiResult<bool>
                {
                    Success = false,
                    Message = $"Lỗi xóa thông báo: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId)
    {
        try
        {
            await _notificationRepository.DeleteAllByUserIdAsync(userId);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 200,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = "Đã xóa tất cả thông báo"
                }
            };
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Fail(500, $"Lỗi xóa tất cả thông báo: {ex.Message}");
        }
    }

    public async Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId)
    {
        try
        {
            var count = await _notificationRepository.GetUnreadCountByUserIdAsync(userId);

            return new ServiceResult<int>
            {
                StatusCode = 200,
                Result = new ApiResult<int>
                {
                    Success = true,
                    Data = count,
                    Message = "Đã lấy số lượng chưa đọc"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<int>
            {
                StatusCode = 500,
                Result = new ApiResult<int>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất số lượng chưa đọc: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId)
    {
        try
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId);

            var stats = new NotificationStatsDto
            {
                TotalNotifications = notifications.Count,
                UnreadCount = notifications.Count(n => !n.IsRead),
                SentCount = notifications.Count(n => n.IsSent),
                FailedCount = notifications.Count(n => !n.IsSent && n.RetryCount >= 3),
                ByCategory = notifications
                    .GroupBy(n => n.Category.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByType = notifications
                    .GroupBy(n => n.Type.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return new ServiceResult<NotificationStatsDto>
            {
                StatusCode = 200,
                Result = new ApiResult<NotificationStatsDto>
                {
                    Success = true,
                    Data = stats,
                    Message = "Đã lấy thống kê"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<NotificationStatsDto>
            {
                StatusCode = 500,
                Result = new ApiResult<NotificationStatsDto>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thống kê: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto)
    {
        try
        {
            foreach (var userId in dto.UserIds)
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = dto.Title,
                    Message = dto.Message,
                    Type = dto.Type,
                    Category = dto.Category,
                    ActionUrl = dto.ActionUrl,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    IsSent = false
                };

                await _notificationRepository.AddAsync(notification);
            }

            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 201,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = $"Đã tạo thông báo hàng loạt cho {dto.UserIds.Count} người dùng"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<bool>
            {
                StatusCode = 500,
                Result = new ApiResult<bool>
                {
                    Success = false,
                    Message = $"Lỗi gửi thông báo hàng loạt: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
    {
        try
        {
            var notifications = await _notificationRepository.GetByEntityAsync(entityType, entityId);
            var dtos = notifications.Select(n => MapToDto(n)).ToList();

            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy thông báo đối tượng"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<NotificationDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<NotificationDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thông báo đối tượng: {ex.Message}"
                }
            };
        }
    }

    public async Task ProcessUnsentNotificationsAsync()
    {
        try
        {
            var unsentNotifications = await _notificationRepository.GetUnsentNotificationsAsync();

            // Simulate sending notifications (in production, integrate with actual email/SMS/push service)
            foreach (var notification in unsentNotifications)
            {
                try
                {
                    // TODO: Integrate with actual notification service (Email, SMS, Push)
                    // For now, just mark as sent
                    await _notificationRepository.MarkAsSentAsync(notification.Id);
                }
                catch (Exception ex)
                {
                    notification.RetryCount++;
                    notification.FailureReason = ex.Message;
                    await _notificationRepository.UpdateAsync(notification);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error processing unsent notifications: {ex.Message}");
        }
    }

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            Category = notification.Category,
            ActionUrl = notification.ActionUrl,
            RelatedEntityId = notification.RelatedEntityId,
            RelatedEntityType = notification.RelatedEntityType,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
            IsSent = notification.IsSent
        };
    }

    private NotificationDetailDto MapToDetailDto(Notification notification)
    {
        return new NotificationDetailDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            UserEmail = notification.User?.Email,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            Category = notification.Category,
            ActionUrl = notification.ActionUrl,
            RelatedEntityId = notification.RelatedEntityId,
            RelatedEntityType = notification.RelatedEntityType,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
            IsSent = notification.IsSent,
            RetryCount = notification.RetryCount ?? 0,
            FailureReason = notification.FailureReason
        };
    }
}
