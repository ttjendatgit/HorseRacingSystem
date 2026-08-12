using System;
using System.Threading.Tasks;

namespace HorseRacing.Services.Interfaces;

public interface IWalletService
{
    Task<ServiceResult<object>> GetBalanceAsync(Guid userId);
    /// <summary>Nạp VNĐ → quy đổi thành điểm</summary>
    Task<ServiceResult<object>> AddFundsAsync(Guid userId, decimal amountVnd, string reference);
    /// <summary>Cộng điểm trực tiếp (thắng cược, hoàn tiền)</summary>
    Task<ServiceResult<object>> AddPointsAsync(Guid userId, decimal points, string reference);
    /// <summary>Trừ điểm (đặt cược)</summary>
    Task<ServiceResult<object>> DeductFundsAsync(Guid userId, decimal points, string reference);
    /// <summary>Đổi điểm → VNĐ</summary>
    Task<ServiceResult<decimal>> ConvertPointsToVndAsync(Guid userId, decimal points);
    decimal GetPointsPerVnd();
}
