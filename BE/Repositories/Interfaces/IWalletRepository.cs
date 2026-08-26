using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IWalletRepository
{
    /// <summary>
    /// Lấy thông tin ví của người dùng theo mã định danh tài khoản.
    /// </summary>
    Task<Wallet?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Thêm mới một ví giao dịch cho người dùng.
    /// </summary>
    Task AddAsync(Wallet wallet);

    /// <summary>
    /// Cập nhật thông tin ví trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Wallet wallet);

    /// <summary>
    /// Cộng điểm trực tiếp vào số dư ví của người dùng.
    /// </summary>
    Task<bool> AddBalanceAsync(Guid userId, decimal amount);

    /// <summary>
    /// Trừ điểm từ số dư ví của người dùng.
    /// </summary>
    Task<bool> DeductBalanceAsync(Guid userId, decimal amount);
}
