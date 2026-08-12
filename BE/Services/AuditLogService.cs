using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;

namespace HorseRacing.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogService(IAuditLogRepository auditLogRepository, IUnitOfWork unitOfWork)
    {
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<AuditLogDto>> LogActionAsync(CreateAuditLogDto dto)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                AdminId = dto.AdminId,
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                Action = dto.Action,
                OldValues = dto.OldValues,
                NewValues = dto.NewValues,
                Description = dto.Description,
                IpAddress = dto.IpAddress,
                UserAgent = dto.UserAgent,
                CreatedAt = DateTime.UtcNow,
                UserId = dto.UserId,
                ChangesSummary = GenerateChangesSummary(dto.OldValues, dto.NewValues)
            };

            await _auditLogRepository.AddAsync(auditLog);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<AuditLogDto>
            {
                StatusCode = 201,
                Result = new ApiResult<AuditLogDto>
                {
                    Success = true,
                    Data = MapToDto(auditLog),
                    Message = "Đã tạo nhật ký"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<AuditLogDto>
            {
                StatusCode = 500,
                Result = new ApiResult<AuditLogDto>
                {
                    Success = false,
                    Message = $"Lỗi tạo nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<AuditLogDetailDto>> GetAuditLogByIdAsync(Guid id)
    {
        try
        {
            var auditLog = await _auditLogRepository.GetByIdAsync(id);
            if (auditLog == null)
            {
                return new ServiceResult<AuditLogDetailDto>
                {
                    StatusCode = 404,
                    Result = new ApiResult<AuditLogDetailDto>
                    {
                        Success = false,
                        Message = "Không tìm thấy nhật ký"
                    }
                };
            }

            return new ServiceResult<AuditLogDetailDto>
            {
                StatusCode = 200,
                Result = new ApiResult<AuditLogDetailDto>
                {
                    Success = true,
                    Data = MapToDetailDto(auditLog),
                    Message = "Đã lấy nhật ký"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<AuditLogDetailDto>
            {
                StatusCode = 500,
                Result = new ApiResult<AuditLogDetailDto>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByAdminAsync(Guid adminId)
    {
        try
        {
            var logs = await _auditLogRepository.GetByAdminIdAsync(adminId);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy danh sách nhật ký"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByEntityAsync(string entityType, Guid entityId)
    {
        try
        {
            var logs = await _auditLogRepository.GetByEntityAsync(entityType, entityId);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy nhật ký đối tượng"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký đối tượng: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsWithFilterAsync(AuditLogFilterDto filter)
    {
        try
        {
            var logs = await _auditLogRepository.GetWithFilterAsync(filter);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lọc nhật ký"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var logs = await _auditLogRepository.GetByDateRangeAsync(fromDate, toDate);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy nhật ký theo khoảng thời gian"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetAuditLogsByUserAsync(Guid userId)
    {
        try
        {
            var logs = await _auditLogRepository.GetByUserIdAsync(userId);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy nhật ký người dùng"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký người dùng: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<AuditLogStatsDto>> GetAuditStatsAsync()
    {
        try
        {
            var allLogs = await _auditLogRepository.GetAllAsync();
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var oneMonthAgo = DateTime.UtcNow.AddDays(-30);

            var stats = new AuditLogStatsDto
            {
                TotalAuditLogs = allLogs.Count,
                EarliestLog = allLogs.OrderBy(l => l.CreatedAt).FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
                LatestLog = allLogs.OrderByDescending(l => l.CreatedAt).FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
                ByAction = allLogs
                    .GroupBy(l => l.Action.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByEntityType = allLogs
                    .GroupBy(l => l.EntityType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByAdmin = allLogs
                    .GroupBy(l => l.Admin?.Email ?? "Không xác định")
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastWeekCount = allLogs.Count(l => l.CreatedAt >= oneWeekAgo),
                LastMonthCount = allLogs.Count(l => l.CreatedAt >= oneMonthAgo)
            };

            return new ServiceResult<AuditLogStatsDto>
            {
                StatusCode = 200,
                Result = new ApiResult<AuditLogStatsDto>
                {
                    Success = true,
                    Data = stats,
                    Message = "Đã lấy thống kê nhật ký"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<AuditLogStatsDto>
            {
                StatusCode = 500,
                Result = new ApiResult<AuditLogStatsDto>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất thống kê nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<bool>> DeleteOldAuditLogsAsync(int daysOlder)
    {
        try
        {
            await _auditLogRepository.DeleteOldLogsAsync(daysOlder);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                StatusCode = 200,
                Result = new ApiResult<bool>
                {
                    Success = true,
                    Data = true,
                    Message = $"Đã xóa nhật ký cũ hơn {daysOlder} ngày"
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
                    Message = $"Lỗi xóa nhật ký cũ: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<string>> ExportAuditLogsAsync(AuditExportDto dto)
    {
        try
        {
            var logs = await _auditLogRepository.GetByDateRangeAsync(dto.FromDate, dto.ToDate);

            if (dto.Format.ToLower() == "csv")
            {
                var csv = ExportToCsv(logs);
                return new ServiceResult<string>
                {
                    StatusCode = 200,
                    Result = new ApiResult<string>
                    {
                        Success = true,
                        Data = csv,
                        Message = "Đã xuất nhật ký dạng CSV"
                    }
                };
            }
            else
            {
                var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
                return new ServiceResult<string>
                {
                    StatusCode = 200,
                    Result = new ApiResult<string>
                    {
                        Success = true,
                        Data = json,
                        Message = "Đã xuất nhật ký dạng JSON"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            return new ServiceResult<string>
            {
                StatusCode = 500,
                Result = new ApiResult<string>
                {
                    Success = false,
                    Message = $"Lỗi xuất nhật ký: {ex.Message}"
                }
            };
        }
    }

    public async Task<ServiceResult<List<AuditLogDto>>> GetLatestLogsAsync(int count)
    {
        try
        {
            var logs = await _auditLogRepository.GetLatestLogsAsync(count);
            var dtos = logs.Select(l => MapToDto(l)).ToList();

            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 200,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = true,
                    Data = dtos,
                    Message = "Đã lấy nhật ký mới nhất"
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceResult<List<AuditLogDto>>
            {
                StatusCode = 500,
                Result = new ApiResult<List<AuditLogDto>>
                {
                    Success = false,
                    Message = $"Lỗi truy xuất nhật ký mới nhất: {ex.Message}"
                }
            };
        }
    }

    private AuditLogDto MapToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            AdminId = log.AdminId,
            AdminEmail = log.Admin?.Email,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Action = log.Action,
            Description = log.Description,
            CreatedAt = log.CreatedAt,
            ChangesSummary = log.ChangesSummary,
            UserId = log.UserId
        };
    }

    private AuditLogDetailDto MapToDetailDto(AuditLog log)
    {
        return new AuditLogDetailDto
        {
            Id = log.Id,
            AdminId = log.AdminId,
            AdminEmail = log.Admin?.Email,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Action = log.Action,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            Description = log.Description,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CreatedAt = log.CreatedAt,
            ChangesSummary = log.ChangesSummary,
            UserId = log.UserId,
            AffectedUserEmail = log.AffectedUser?.Email
        };
    }

    private string? GenerateChangesSummary(string? oldValues, string? newValues)
    {
        if (string.IsNullOrEmpty(oldValues) && string.IsNullOrEmpty(newValues))
            return null;

        var summary = new List<string>();

        if (!string.IsNullOrEmpty(oldValues) && !string.IsNullOrEmpty(newValues))
        {
            try
            {
                var oldObj = JsonSerializer.Deserialize<Dictionary<string, object>>(oldValues);
                var newObj = JsonSerializer.Deserialize<Dictionary<string, object>>(newValues);

                if (oldObj != null && newObj != null)
                {
                    foreach (var key in newObj.Keys)
                    {
                        if (!oldObj.ContainsKey(key))
                        {
                            summary.Add($"{key}: Added = {newObj[key]}");
                        }
                        else if (oldObj[key]?.ToString() != newObj[key]?.ToString())
                        {
                            summary.Add($"{key}: {oldObj[key]} → {newObj[key]}");
                        }
                    }

                    foreach (var key in oldObj.Keys)
                    {
                        if (!newObj.ContainsKey(key))
                        {
                            summary.Add($"{key}: Removed = {oldObj[key]}");
                        }
                    }
                }
            }
            catch
            {
                summary.Add("Giá trị đã thay đổi");
            }
        }

        return summary.Count > 0 ? string.Join("; ", summary) : null;
    }

    private string ExportToCsv(List<AuditLog> logs)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,AdminId,AdminEmail,EntityType,EntityId,Action,Description,CreatedAt,IpAddress,UserAgent");

        foreach (var log in logs)
        {
            csv.AppendLine($"\"{log.Id}\",\"{log.AdminId}\",\"{log.Admin?.Email}\",\"{log.EntityType}\",\"{log.EntityId}\",\"{log.Action}\",\"{log.Description}\",\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{log.IpAddress}\",\"{log.UserAgent}\"");
        }

        return csv.ToString();
    }
}
