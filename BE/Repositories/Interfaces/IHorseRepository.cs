using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IHorseRepository
{
    /// <summary>
    /// Lấy thông tin chi tiết của một con ngựa theo mã định danh.
    /// </summary>
    Task<Horse?> GetByIdAsync(Guid horseId);

    /// <summary>
    /// Lấy danh sách tất cả các con ngựa thuộc sở hữu của một chủ ngựa cụ thể.
    /// </summary>
    Task<List<Horse>> GetByOwnerAsync(Guid ownerId);

    /// <summary>
    /// Lấy thông tin một con ngựa cụ thể và xác minh quyền sở hữu của chủ ngựa.
    /// </summary>
    Task<Horse?> GetOwnedHorseAsync(Guid horseId, Guid ownerId);

    /// <summary>
    /// Lấy danh sách tất cả các con ngựa đã được phê duyệt (Approved) trong hệ thống.
    /// </summary>
    Task<List<Horse>> GetAllApprovedAsync();

    /// <summary>
    /// Thêm mới một con ngựa vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Horse horse);

    /// <summary>
    /// Cập nhật thông tin con ngựa trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Horse horse);

    /// <summary>
    /// Xóa một con ngựa khỏi cơ sở dữ liệu.
    /// </summary>
    Task RemoveAsync(Horse horse);
}
