using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface ITournamentRepository
{
    /// <summary>
    /// Lấy thông tin giải đấu theo mã định danh.
    /// </summary>
    Task<Tournament?> GetByIdAsync(Guid id);

    /// <summary>
    /// Lấy danh sách tất cả các giải đấu kèm danh sách các cuộc đua trực thuộc.
    /// </summary>
    Task<List<Tournament>> GetAllWithRacesAsync();

    /// <summary>
    /// Lấy danh sách tất cả các giải đấu trong cơ sở dữ liệu.
    /// </summary>
    Task<IEnumerable<Tournament>> GetAllAsync();

    /// <summary>
    /// Lấy danh sách các giải đấu đang diễn ra hoặc đang mở đăng ký (Active).
    /// </summary>
    Task<IEnumerable<Tournament>> GetActiveAsync();

    /// <summary>
    /// Thêm một giải đấu mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Tournament tournament);

    /// <summary>
    /// Cập nhật thông tin giải đấu trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Tournament tournament);

    /// <summary>
    /// Xóa một giải đấu theo mã định danh.
    /// </summary>
    Task DeleteAsync(Guid id);
}
