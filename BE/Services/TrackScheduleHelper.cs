using HorseRacing.Data;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

internal static class TrackScheduleHelper
{
    /// <summary>
    /// Phase5: true when another Race already occupies the same Track during [start, end)
    /// using the locked overlap formula (existing.Start &lt; candidate.End AND candidate.Start &lt;
    /// existing.End — back-to-back windows do not conflict). Scope convention (spec is silent on
    /// status filtering, see Phase5A audit §L): any Race belonging to `tournamentId` reserves the
    /// Track regardless of that Tournament's own status; Races belonging to OTHER Tournaments only
    /// reserve the Track while those Tournaments are Published or Ongoing — Draft/Cancelled/Finished
    /// Tournaments never reserve a Track globally.
    /// </summary>
    public static Task<bool> HasOverlapAsync(
        ApplicationDbContext db, Guid tournamentId, Guid trackId,
        DateTime start, DateTime end, Guid? excludeRaceId)
    {
        return db.Races
            .Where(r => r.TrackId == trackId)
            .Where(r => excludeRaceId == null || r.Id != excludeRaceId.Value)
            .Where(r => r.TournamentId == tournamentId
                || r.Tournament!.Status == TournamentStatus.Published
                || r.Tournament!.Status == TournamentStatus.Ongoing)
            .AnyAsync(r => r.ScheduledAt < end && start < (r.ScheduledEndAt ?? r.ScheduledAt));
    }
}
