using HorseRacing.Data;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

internal static class RaceDeletionHelper
{
    public static async Task DeleteRaceGraphAsync(ApplicationDbContext db, Guid raceId)
    {
        // These tables all use RESTRICT foreign keys. Records that also reference a
        // race entry must be removed before the entries themselves.
        await db.ViolationRecords.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.Protests.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();

        await db.JockeyInvitations.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.RaceResults.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.RaceReports.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.HorseHealthChecks.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.RefereeAssignments.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.Predictions.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.Prizes.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.RaceEntries.Where(x => x.RaceId == raceId).ExecuteDeleteAsync();
        await db.Races.Where(x => x.Id == raceId).ExecuteDeleteAsync();
    }
}
