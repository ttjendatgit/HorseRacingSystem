using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý thông tin kỵ sĩ, phản hồi lời mời điều khiển ngựa và lịch thi đấu.
/// </summary>
public interface IJockeyService
{
    /// <summary>Lấy danh sách các kỵ sĩ khả dụng có thể mời tham gia điều khiển ngựa.</summary>
    Task<ServiceResult<object>> GetAvailableJockeysAsync(Guid currentUserId, bool includeUnapproved = false);

    /// <summary>Lấy danh sách các lời mời điều khiển ngựa dành cho kỵ sĩ đang đăng nhập.</summary>
    Task<ServiceResult<object>> GetInvitationsAsync(Guid userId);

    /// <summary>Kỵ sĩ gửi phản hồi Đồng ý hoặc Từ chối lời mời điều khiển ngựa.</summary>
    Task<ServiceResult<object>> RespondInvitationAsync(Guid userId, Guid invitationId, JockeyInvitationRespondRequest request);

    /// <summary>Chủ ngựa rút lại lời mời kỵ sĩ trước đó.</summary>
    Task<ServiceResult<object>> WithdrawInvitationAsync(Guid userId, Guid invitationId, JockeyInvitationWithdrawRequest request);

    /// <summary>Lấy lịch phân công thi đấu các trận đua dành cho kỵ sĩ.</summary>
    Task<ServiceResult<object>> GetAssignedRacesAsync(Guid userId);

    /// <summary>Lấy danh sách các lượt thi đấu của ngựa đang chờ kỵ sĩ xác nhận.</summary>
    Task<ServiceResult<object>> GetPendingRaceEntriesAsync(Guid userId);

    /// <summary>Kỵ sĩ xác nhận tham gia điều khiển ngựa trong lượt đua.</summary>
    Task<ServiceResult<object>> ConfirmRaceEntryAsync(Guid userId, Guid entryId);

    /// <summary>Kỵ sĩ từ chối lượt thi đấu điều khiển ngựa.</summary>
    Task<ServiceResult<object>> DeclineRaceEntryAsync(Guid userId, Guid entryId);

    /// <summary>Lấy thông tin hồ sơ cá nhân và số liệu thống kê thi đấu của kỵ sĩ.</summary>
    Task<ServiceResult<object>> GetMyProfileAsync(Guid userId);
}
