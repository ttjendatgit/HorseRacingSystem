using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IWithdrawalRepository
{
    /// <summary>
    /// Thêm một yêu cầu rút tiền mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(WithdrawalRequest withdrawal);

    /// <summary>
    /// Lấy danh sách tất cả các đơn yêu cầu rút tiền của người dùng.
    /// </summary>
    Task<List<WithdrawalRequest>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy thông tin chi tiết đơn yêu cầu rút tiền theo mã định danh.
    /// </summary>
    Task<WithdrawalRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Lấy danh sách các đơn yêu cầu rút tiền đang ở trạng thái chờ duyệt (Pending).
    /// </summary>
    Task<List<WithdrawalRequest>> GetPendingAsync();

    /// <summary>
    /// Lấy tất cả các đơn yêu cầu rút tiền trong hệ thống.
    /// </summary>
    Task<List<WithdrawalRequest>> GetAllAsync();

    /// <summary>
    /// Cập nhật trạng thái đơn rút tiền (Đã duyệt / Từ chối) trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(WithdrawalRequest withdrawal);
}
