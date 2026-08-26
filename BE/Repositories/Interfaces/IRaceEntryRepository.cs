using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceEntryRepository
{
    /// <summary>
    /// Kiểm tra con ngựa đã từng đăng ký vào cuộc đua này chưa (chống đăng ký trùng).
    /// </summary>
    Task<bool> ExistsAsync(Guid raceId, Guid horseId);

    /// <summary>
    /// Kiểm tra xem chủ sở hữu đã có con ngựa nào khác tham gia trong cùng cuộc đua chưa.
    /// </summary>
    Task<bool> OwnerHasHorseInRaceAsync(Guid raceId, Guid ownerId);

    /// <summary>
    /// Lấy thông tin lượt đăng ký thi đấu kèm thông tin con ngựa.
    /// </summary>
    Task<RaceEntry?> GetByIdWithHorseAsync(Guid entryId, Guid raceId);

    /// <summary>
    /// Lấy lượt đăng ký theo cuộc đua và con ngựa.
    /// </summary>
    Task<RaceEntry?> GetByRaceHorseAsync(Guid raceId, Guid horseId);

    /// <summary>
    /// Lấy lượt đăng ký thi đấu theo cuộc đua và con ngựa.
    /// </summary>
    Task<RaceEntry?> GetByRaceAndHorseAsync(Guid raceId, Guid horseId);

    /// <summary>
    /// Lấy tất cả các lượt đăng ký thi đấu thuộc về một kỵ thủ cụ thể.
    /// </summary>
    Task<List<RaceEntry>> GetByJockeyAsync(Guid jockeyId);

    /// <summary>
    /// Lấy danh sách các lượt đăng ký thi đấu đang chờ kỵ thủ xác nhận.
    /// </summary>
    Task<List<RaceEntry>> GetPendingConfirmationsByJockeyAsync(Guid jockeyId);

    /// <summary>
    /// Lấy tất cả các lượt đăng ký thi đấu của một con ngựa.
    /// </summary>
    Task<List<RaceEntry>> GetByHorseAsync(Guid horseId);

    /// <summary>
    /// Lấy tất cả các lượt đăng ký thi đấu thuộc về một cuộc đua.
    /// </summary>
    Task<List<RaceEntry>> GetByRaceAsync(Guid raceId);

    /// <summary>
    /// Lấy danh sách ID của các con ngựa đang tham gia các cuộc đua đang diễn ra.
    /// </summary>
    Task<List<Guid>> GetHorseIdsInActiveRacesAsync();

    /// <summary>
    /// Kiểm tra con ngựa có đang tham gia trong một cuộc đua đang hoạt động hay không.
    /// </summary>
    Task<bool> IsHorseInActiveRaceAsync(Guid horseId, Guid? excludeRaceId = null);

    /// <summary>
    /// Kiểm tra xem kỵ thủ có bị trùng lịch thi đấu giữa các cuộc đua không.
    /// </summary>
    Task<bool> HasJockeyScheduleConflictAsync(Guid jockeyId, DateTime scheduledAt, DateTime scheduledEndAt, Guid? excludeEntryId = null);

    /// <summary>
    /// Lấy danh sách phân công thi đấu chính thức của kỵ thủ.
    /// </summary>
    Task<List<RaceEntry>> GetOfficialAssignmentsForJockeyAsync(Guid jockeyId);

    /// <summary>
    /// Lấy phân công thi đấu chính thức của con ngựa trong giải đấu.
    /// </summary>
    Task<RaceEntry?> GetOfficialAssignmentForHorseInTournamentAsync(Guid horseId, Guid tournamentId);

    /// <summary>
    /// Thêm một lượt đăng ký thi đấu mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(RaceEntry entry);

    /// <summary>
    /// Cập nhật thông tin lượt đăng ký thi đấu trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(RaceEntry entry);

    /// <summary>
    /// Cập nhật hàng loạt lượt đăng ký thi đấu trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<RaceEntry> entries);

    /// <summary>
    /// Lấy danh sách các lượt đăng ký thi đấu đang ở trạng thái chờ duyệt (Pending) kèm chi tiết.
    /// </summary>
    Task<List<RaceEntry>> GetPendingWithDetailsAsync();

    /// <summary>
    /// Xóa một lượt đăng ký thi đấu khỏi cơ sở dữ liệu.
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Lấy thông tin lượt đăng ký thi đấu theo mã định danh.
    /// </summary>
    Task<RaceEntry?> GetByIdAsync(Guid id);
}
