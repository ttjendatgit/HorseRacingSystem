using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IJockeyInvitationRepository
{
    /// <summary>
    /// Lấy danh sách tất cả các lời mời thi đấu gửi tới một kỵ thủ.
    /// </summary>
    Task<List<JockeyInvitation>> GetByJockeyAsync(Guid jockeyId);

    /// <summary>
    /// Lấy thông tin chi tiết lời mời thi đấu theo ID lời mời và ID kỵ thủ.
    /// </summary>
    Task<JockeyInvitation?> GetByIdAsync(Guid invitationId, Guid jockeyId);

    /// <summary>
    /// Lấy lời mời thi đấu đang có hiệu lực (Active/Accepted) của một con ngựa.
    /// </summary>
    Task<JockeyInvitation?> GetActiveByHorseAsync(Guid horseId);

    /// <summary>
    /// Lấy lời mời thi đấu giữa một con ngựa và một kỵ thủ cụ thể.
    /// </summary>
    Task<JockeyInvitation?> GetByHorseAndJockeyAsync(Guid horseId, Guid jockeyId);

    /// <summary>
    /// Thêm một lời mời thi đấu mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(JockeyInvitation invitation);
}
