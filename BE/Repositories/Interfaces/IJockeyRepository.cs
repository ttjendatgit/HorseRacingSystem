using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IJockeyRepository
{
    /// <summary>
    /// Kiểm tra kỵ thủ có tồn tại trong hệ thống theo mã định danh hay không.
    /// </summary>
    Task<bool> ExistsAsync(Guid jockeyId);

    /// <summary>
    /// Lấy danh sách tất cả các kỵ thủ trong cơ sở dữ liệu.
    /// </summary>
    Task<List<Jockey>> GetAllAsync();

    /// <summary>
    /// Lấy danh sách các kỵ thủ sẵn sàng nhận lời mời tham gia thi đấu.
    /// </summary>
    Task<List<Jockey>> GetAvailableAsync();

    /// <summary>
    /// Lấy thông tin kỵ thủ theo mã định danh kỵ thủ.
    /// </summary>
    Task<Jockey?> GetByIdAsync(Guid jockeyId);

    /// <summary>
    /// Lấy thông tin kỵ thủ theo mã định danh tài khoản người dùng (User ID).
    /// </summary>
    Task<Jockey?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Thêm mới một kỵ thủ vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Jockey jockey);

    /// <summary>
    /// Cập nhật thông tin kỵ thủ trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Jockey jockey);
}
