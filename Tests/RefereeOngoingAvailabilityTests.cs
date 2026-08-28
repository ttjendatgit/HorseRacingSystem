using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Starting a tournament (Published -> Ongoing) must lock every referee who has a Confirmed
/// assignment anywhere in that tournament: they disappear from GetActiveRefereesAsync (admin
/// assign picker) and AssignRefereeToRaceAsync rejects them. Availability is derived from
/// Ongoing + Confirmed — Referee.IsActive is not mutated.
/// </summary>
public class RefereeOngoingAvailabilityTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly IRefereeService _service;

    public RefereeOngoingAvailabilityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        _service = new RefereeService(
            new RefereeRepository(_db),
            new RefereeAssignmentRepository(_db),
            new HealthCheckRepository(_db),
            new ViolationRecordRepository(_db),
            new RaceRepository(_db),
            new UnitOfWork(_db));
    }

    private async Task<(Referee Referee, Race Race, Tournament Tournament)> SeedRefereeWithRaceAsync(
        TournamentStatus tournamentStatus, RefereeAssignmentStatus assignmentStatus)
    {
        var refereeUser = new User
        {
            Id = Guid.NewGuid(), Email = $"ref-{Guid.NewGuid()}@test.com", PasswordHash = "x",
            FullName = "Referee", Role = UserRole.Referee
        };
        var referee = new Referee
        {
            Id = Guid.NewGuid(),
            UserId = refereeUser.Id,
            LicenseNumber = $"LIC-{Guid.NewGuid()}",
            IsActive = true,
            LicenseExpiryDate = DateTime.UtcNow.AddYears(1)
        };
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Giải đấu test",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = tournamentStatus,
            IsActive = tournamentStatus == TournamentStatus.Ongoing
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = "Vòng loại",
            TournamentId = tournament.Id,
            RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow,
            ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = "Cuộc đua 1",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            Status = RaceStatus.Scheduled
        };
        var assignment = new RefereeAssignment
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            RefereeId = referee.Id,
            Role = "Chief Referee",
            Status = assignmentStatus,
            AssignedAt = DateTime.UtcNow,
            ConfirmedAt = assignmentStatus == RefereeAssignmentStatus.Confirmed ? DateTime.UtcNow : null
        };

        _db.AddRange(refereeUser, referee, tournament, round, race, assignment);
        await _db.SaveChangesAsync();
        return (referee, race, tournament);
    }

    [Fact]
    public async Task GetActive_ConfirmedInOngoingTournament_HidesReferee()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Ongoing, RefereeAssignmentStatus.Confirmed);

        var result = await _service.GetActiveRefereesAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Result.Data!, r => r.Id == referee.Id);
    }

    [Fact]
    public async Task GetActive_ConfirmedInPublishedTournament_StillShowsReferee()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Published, RefereeAssignmentStatus.Confirmed);

        var result = await _service.GetActiveRefereesAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Result.Data!, r => r.Id == referee.Id);
    }

    [Fact]
    public async Task GetActive_AssignedButNotConfirmedInOngoing_StillShowsReferee()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Ongoing, RefereeAssignmentStatus.Assigned);

        var result = await _service.GetActiveRefereesAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Result.Data!, r => r.Id == referee.Id);
    }

    [Fact]
    public async Task GetActive_ConfirmedInFinishedTournament_StillShowsReferee()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Finished, RefereeAssignmentStatus.Confirmed);

        var result = await _service.GetActiveRefereesAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Result.Data!, r => r.Id == referee.Id);
    }

    [Fact]
    public async Task Assign_ConfirmedInOngoingTournament_Rejected()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Ongoing, RefereeAssignmentStatus.Confirmed);

        var other = await SeedOtherDraftRaceAsync();

        var result = await _service.AssignRefereeToRaceAsync(new AssignRefereeRequest
        {
            RaceId = other.Id,
            RefereeId = referee.Id,
            Role = "Chief Referee"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Assign_ConfirmedInPublishedTournament_Succeeds()
    {
        var (referee, _, _) = await SeedRefereeWithRaceAsync(
            TournamentStatus.Published, RefereeAssignmentStatus.Confirmed);

        var other = await SeedOtherDraftRaceAsync();

        var result = await _service.AssignRefereeToRaceAsync(new AssignRefereeRequest
        {
            RaceId = other.Id,
            RefereeId = referee.Id,
            Role = "Assistant"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
    }

    private async Task<Race> SeedOtherDraftRaceAsync()
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Giải khác",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = TournamentStatus.Draft
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = "Vòng 1",
            TournamentId = tournament.Id,
            RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow,
            ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = "Cuộc đua khác",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            Status = RaceStatus.Scheduled
        };
        _db.AddRange(tournament, round, race);
        await _db.SaveChangesAsync();
        return race;
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
