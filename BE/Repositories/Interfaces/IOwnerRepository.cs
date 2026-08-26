using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IOwnerRepository
{
    /// <summary>
    /// Kiểm tra chủ sở hữu có tồn tại trong hệ thống hay không.
    /// </summary>
    Task<bool> ExistsAsync(Guid ownerId);

    /// <summary>
    /// Lấy thông tin hồ sơ chủ sở hữu theo mã định danh chủ sở hữu.
    /// </summary>
    Task<Owner?> GetByIdAsync(Guid ownerId);

    /// <summary>
    /// Lấy thông tin hồ sơ chủ sở hữu theo mã định danh tài khoản người dùng (User ID).
    /// </summary>
    Task<Owner?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Thêm mới một hồ sơ chủ sở hữu vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Owner owner);

    /// <summary>
    /// Cập nhật thông tin hồ sơ chủ sở hữu trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Owner owner);
}
