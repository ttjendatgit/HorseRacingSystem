using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IPredictionRepository
{
    /// <summary>
    /// Kiểm tra xem khán giả đã từng đặt dự đoán cho cuộc đua này chưa (chống cược trùng).
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <param name="spectatorUserId">Mã định danh khán giả.</param>
    /// <returns>True nếu khán giả đã từng đặt cược.</returns>
    Task<bool> ExistsAsync(Guid raceId, Guid spectatorUserId);

    /// <summary>
    /// Thêm một lượt đặt dự đoán mới vào cơ sở dữ liệu.
    /// </summary>
    /// <param name="prediction">Thực thể lượt đặt dự đoán.</param>
    Task AddAsync(Prediction prediction);

    /// <summary>
    /// Lấy tất cả lịch sử dự đoán của một khán giả theo ID.
    /// </summary>
    /// <param name="spectatorUserId">Mã định danh khán giả.</param>
    /// <returns>Danh sách lượt dự đoán của khán giả.</returns>
    Task<List<Prediction>> GetByUserAsync(Guid spectatorUserId);

    /// <summary>
    /// Lấy tất cả lượt dự đoán thuộc về một cuộc đua cụ thể.
    /// </summary>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <returns>Danh sách lượt dự đoán của cuộc đua.</returns>
    Task<List<Prediction>> GetByRaceAsync(Guid raceId);

    /// <summary>
    /// Lấy toàn bộ danh sách lượt dự đoán trong hệ thống.
    /// </summary>
    /// <returns>Danh sách lượt dự đoán.</returns>
    Task<List<Prediction>> GetAllAsync();

    /// <summary>
    /// Cập nhật nguyên tử trạng thái Thua (Lost) cho tất cả lượt dự đoán Pending không chọn con ngựa thắng.
    /// </summary>
    Task<int> ExecuteUpdateLosersAsync(Guid raceId, Guid winningHorseId);

    /// <summary>
    /// Cập nhật nguyên tử trạng thái Thắng (Won) cho tất cả lượt dự đoán Pending chọn đúng con ngựa thắng.
    /// </summary>
    Task<int> ExecuteUpdateWinnersAsync(Guid raceId, Guid winningHorseId);

    /// <summary>
    /// Lấy danh sách ID của các lượt dự đoán đang Pending của con ngựa thắng trước khi thực hiện chuyển trạng thái.
    /// </summary>
    Task<List<Guid>> GetPendingWinnerIdsAsync(Guid raceId, Guid winningHorseId);

    /// <summary>
    /// Truy xuất danh sách thực thể lượt dự đoán theo danh sách ID truyền vào.
    /// </summary>
    Task<List<Prediction>> GetByIdsAsync(IReadOnlyCollection<Guid> predictionIds);

    /// <summary>
    /// Đảo ngược trạng thái của lượt cược thắng (Won) bị lỗi cộng điểm ví về lại trạng thái chờ (Pending) để xử lý lại.
    /// </summary>
    Task<bool> RevertWonToPendingAsync(Guid predictionId);

    /// <summary>
    /// Xóa một lượt đặt dự đoán theo mã định danh.
    /// </summary>
    Task DeleteAsync(Guid id);
}
