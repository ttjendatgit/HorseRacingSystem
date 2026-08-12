using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class RaceEntryRepositoryScheduleTests
{
    [Fact]
    public async Task HasJockeyScheduleConflict_ReturnsTrue_WhenIntervalsOverlap()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True(await fixture.Repository.HasJockeyScheduleConflictAsync(
            fixture.JockeyId, fixture.Start.AddMinutes(15), fixture.Start.AddMinutes(45)));
    }

    [Fact]
    public async Task HasJockeyScheduleConflict_ReturnsFalse_WhenIntervalsDoNotOverlap()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.False(await fixture.Repository.HasJockeyScheduleConflictAsync(
            fixture.JockeyId, fixture.Start.AddHours(1), fixture.Start.AddHours(2)));
    }

    [Fact]
    public async Task HasJockeyScheduleConflict_IgnoresRejectedAndExcludedEntry()
    {
        await using var fixture = await Fixture.CreateAsync(RegistrationStatus.Rejected);
        Assert.False(await fixture.Repository.HasJockeyScheduleConflictAsync(
            fixture.JockeyId, fixture.Start, fixture.Start.AddMinutes(30)));

        fixture.Entry.Status = RegistrationStatus.Pending;
        await fixture.Db.SaveChangesAsync();
        Assert.False(await fixture.Repository.HasJockeyScheduleConflictAsync(
            fixture.JockeyId, fixture.Start, fixture.Start.AddMinutes(30), fixture.Entry.Id));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(SqliteConnection connection, ApplicationDbContext db, RaceEntry entry,
            Guid jockeyId, DateTime start)
        {
            Connection = connection; Db = db; Entry = entry; JockeyId = jockeyId; Start = start;
            Repository = new RaceEntryRepository(db);
        }
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Db { get; }
        public RaceEntry Entry { get; }
        public Guid JockeyId { get; }
        public DateTime Start { get; }
        public RaceEntryRepository Repository { get; }

        public static async Task<Fixture> CreateAsync(RegistrationStatus status = RegistrationStatus.Pending)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var start = new DateTime(2026, 8, 3, 8, 0, 0, DateTimeKind.Utc);
            var race = new Race { Id = Guid.NewGuid(), Name = "Race", TournamentId = Guid.NewGuid(),
                ScheduledAt = start, ScheduledEndAt = start.AddMinutes(30), Status = RaceStatus.Scheduled };
            var entry = new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Id, HorseId = Guid.NewGuid(),
                JockeyId = Guid.NewGuid(), Status = status };
            db.AddRange(race, entry);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, entry, entry.JockeyId!.Value, start);
        }

        public async ValueTask DisposeAsync()
        { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
