using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý hồ sơ ngựa đua, đăng ký thi đấu và phân công kỵ sĩ.
/// </summary>
public interface IHorseService
{
    /// <summary>Lấy thông tin chi tiết của một con ngựa đua theo mã GUID.</summary>
    Task<ServiceResult<object>> GetHorseAsync(Guid ownerId, Guid horseId);

    /// <summary>Lấy danh sách tất cả các con ngựa thuộc sở hữu của một chủ ngựa.</summary>
    Task<ServiceResult<object>> GetMyHorsesAsync(Guid ownerId);

    /// <summary>Lấy danh sách tất cả các con ngựa đã được Admin phê duyệt thi đấu.</summary>
    Task<ServiceResult<object>> GetAllApprovedHorsesAsync();

    /// <summary>Đăng ký thêm một con ngựa đua mới vào hệ thống.</summary>
    Task<ServiceResult<object>> CreateHorseAsync(Guid ownerId, HorseCreateRequest request);

    /// <summary>Cập nhật thông tin và chỉ số cá nhân của con ngựa đua.</summary>
    Task<ServiceResult<object>> UpdateHorseAsync(Guid ownerId, Guid horseId, HorseUpdateRequest request, bool isAdmin = false);

    /// <summary>Xóa hoặc lưu trữ thông tin con ngựa khỏi hệ thống.</summary>
    Task<ServiceResult<string>> DeleteHorseAsync(Guid ownerId, Guid horseId, bool isAdmin = false);

    /// <summary>Gửi lời mời kỵ sĩ (Jockey) điều khiển con ngựa trong trận đua.</summary>
    Task<ServiceResult<object>> InviteJockeyAsync(Guid ownerId, Guid horseId, JockeyInvitationCreateRequest request);

    /// <summary>Hủy phân công kỵ sĩ khỏi lượt đua của con ngựa.</summary>
    Task<ServiceResult<string>> RemoveJockeyAsync(Guid ownerId, Guid horseId, Guid raceId, JockeyRemovalRequest request);

    /// <summary>Chủ ngựa xác nhận tham gia lượt đua chính thức.</summary>
    Task<ServiceResult<object>> ConfirmOwnerAsync(Guid ownerId, Guid raceId, Guid entryId);

    /// <summary>Chủ ngựa chốt chọn chính thức 1 kỵ sĩ đã chấp nhận lời mời để lái ngựa đua.</summary>
    Task<ServiceResult<object>> FinalConfirmJockeyAsync(Guid ownerId, Guid horseId, Guid raceId, OwnerFinalConfirmJockeyRequest request);
}
