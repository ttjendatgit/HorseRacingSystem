using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceRepository
{
    /// <summary>
    /// Kiểm tra cuộc đua có tồn tại trong hệ thống theo mã định danh hay không.
    /// </summary>
    Task<bool> ExistsAsync(Guid raceId);

    /// <summary>
    /// Lấy danh sách tất cả các cuộc đua trong cơ sở dữ liệu.
    /// </summary>
    Task<List<Race>> GetAllAsync();

    /// <summary>
    /// Lấy thông tin chi tiết cuộc đua theo mã định danh.
    /// </summary>
    Task<Race?> GetByIdAsync(Guid raceId);

    /// <summary>
    /// Lấy thông tin cuộc đua kèm danh sách các lượt đăng ký thi đấu của ngựa (Entries).
    /// </summary>
    Task<Race?> GetByIdWithEntriesAsync(Guid raceId);

    /// <summary>
    /// Lấy danh sách các cuộc đua thuộc về một giải đấu cụ thể.
    /// </summary>
    Task<List<Race>> GetByTournamentAsync(Guid tournamentId);

    /// <summary>
    /// Lấy danh sách các cuộc đua thuộc về một vòng đấu cụ thể.
    /// </summary>
    Task<List<Race>> GetByRoundAsync(Guid roundId);

    /// <summary>
    /// Lấy khung thời gian lịch thi đấu tóm tắt (ScheduledAt / ScheduledEndAt) của các giải đấu để kiểm tra trùng lịch.
    /// </summary>
    Task<Dictionary<Guid, List<(DateTime Start, DateTime End)>>> GetScheduleWindowsByTournamentsAsync(IEnumerable<Guid> tournamentIds);

    /// <summary>
    /// Thêm mới một cuộc đua vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Race race);

    /// <summary>
    /// Cập nhật thông tin cuộc đua trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(Race race);

    /// <summary>
    /// Xóa một cuộc đua khỏi cơ sở dữ liệu theo mã định danh.
    /// </summary>
    Task DeleteAsync(Guid raceId);
}
