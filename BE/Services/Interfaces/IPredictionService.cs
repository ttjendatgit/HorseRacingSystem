using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý tạo cược dự đoán, tra cứu lịch sử cược và quyết toán trả thưởng cho khán giả.
/// </summary>
public interface IPredictionService
{
    /// <summary>Khán giả tạo phiếu cược dự đoán cho một con ngựa trong trận đua.</summary>
    Task<ServiceResult<object>> CreatePredictionAsync(Guid userId, PredictionCreateRequest request);

    /// <summary>Lấy danh sách tất cả các phiếu dự đoán cược cá nhân của khán giả đang đăng nhập.</summary>
    Task<ServiceResult<object>> GetMyPredictionsAsync(Guid userId);

    /// <summary>Quyết toán và tự động trả thưởng tiền cược cho các phiếu dự đoán thắng theo thứ hạng chính thức.</summary>
    Task<ServiceResult<object>> SettlePredictionAsync(Guid raceId, Guid winningHorseId);
}
