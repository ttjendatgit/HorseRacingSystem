using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceResultRepository
{
    /// <summary>
    /// Lấy kết quả thi đấu chính thức của một cuộc đua theo mã cuộc đua.
    /// </summary>
    Task<RaceResult?> GetByRaceIdAsync(Guid raceId);

    /// <summary>
    /// Thêm mới một kết quả thi đấu vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(RaceResult raceResult);

    /// <summary>
    /// Cập nhật kết quả thi đấu và bảng xếp hạng trong cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(RaceResult raceResult);
}
