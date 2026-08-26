using System;
using System.Threading.Tasks;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý ví tiền, quy đổi tỷ giá và nạp/trừ điểm số dư của người dùng.
/// </summary>
public interface IWalletService
{
    /// <summary>
    /// Lấy số dư điểm khả dụng hiện tại của người dùng theo mã GUID.
    /// </summary>
    Task<ServiceResult<object>> GetBalanceAsync(Guid userId);

    /// <summary>
    /// Nạp số tiền VNĐ và quy đổi thành điểm số dư khả dụng vào ví người dùng.
    /// </summary>
    Task<ServiceResult<object>> AddFundsAsync(Guid userId, decimal amountVnd, string reference);

    /// <summary>
    /// Cộng điểm trực tiếp vào ví người dùng (khi thắng cược hoặc nhận tiền thưởng/hoàn tiền).
    /// </summary>
    Task<ServiceResult<object>> AddPointsAsync(Guid userId, decimal points, string reference);

    /// <summary>
    /// Trừ điểm trong ví khi người dùng thực hiện đặt cược dự đoán cuộc đua.
    /// </summary>
    Task<ServiceResult<object>> DeductFundsAsync(Guid userId, decimal points, string reference);

    /// <summary>
    /// Quy đổi điểm số dư ví sang số tiền VNĐ tương ứng.
    /// </summary>
    Task<ServiceResult<decimal>> ConvertPointsToVndAsync(Guid userId, decimal points);

    /// <summary>
    /// Lấy tỷ lệ quy đổi số điểm trên 1 VNĐ hiện tại của hệ thống.
    /// </summary>
    decimal GetPointsPerVnd();
}
