using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Kiểm tra địa chỉ Email đã tồn tại trong hệ thống chưa (chống trùng lặp email khi đăng ký).
    /// </summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Tìm kiếm thông tin người dùng theo địa chỉ Email.
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Tìm kiếm thông tin người dùng theo mã định danh tài khoản (User ID).
    /// </summary>
    Task<User?> GetByIdAsync(Guid userId);

    /// <summary>
    /// Tìm kiếm người dùng theo mã băm Refresh Token.
    /// </summary>
    Task<User?> GetByRefreshTokenHashAsync(string hash);

    /// <summary>
    /// Lấy danh sách tất cả các tài khoản người dùng trong cơ sở dữ liệu.
    /// </summary>
    Task<List<User>> GetAllAsync();

    /// <summary>
    /// Thêm một tài khoản người dùng mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(User user);

    /// <summary>
    /// Cập nhật thông tin tài khoản người dùng trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(User user);
}
