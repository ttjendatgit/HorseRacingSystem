using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IBankAccountRepository
{
    /// <summary>
    /// Lấy danh sách các tài khoản ngân hàng thụ hưởng đã liên kết của người dùng.
    /// </summary>
    Task<List<BankAccount>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Lấy thông tin tài khoản ngân hàng theo mã định danh tài khoản.
    /// </summary>
    Task<BankAccount?> GetByIdAsync(Guid id);

    /// <summary>
    /// Thêm một tài khoản ngân hàng thụ hưởng mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(BankAccount account);
}
