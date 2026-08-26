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
/// FIX-REFEREE-ASSIGNMENT-SCHEDULE-AND-STATUS-DISPLAY: regression coverage for
/// RefereeService.MapToAssignmentResponse exposing Race.ScheduledAt/ScheduledEndAt
/// (previously omitted from RefereeAssignmentResponse entirely), and for confirming
/// an assignment never touching the Race's own schedule.
/// </summary>
public class RefereeAssignmentScheduleDisplayTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly IRefereeService _service;
    private readonly IRaceRepository _raceRepo;

    public RefereeAssignmentScheduleDisplayTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        var refereeRepo = new RefereeRepository(_db);
        var assignmentRepo = new RefereeAssignmentRepository(_db);
        var healthCheckRepo = new HealthCheckRepository(_db);
        var violationRepo = new ViolationRecordRepository(_db);
        _raceRepo = new RaceRepository(_db);
        var unitOfWork = new UnitOfWork(_db);

        _service = new RefereeService(refereeRepo, assignmentRepo, healthCheckRepo, violationRepo, _raceRepo, unitOfWork);
    }

    private async Task<(Referee Referee, Race Race, RefereeAssignment Assignment)> SeedAssignmentAsync(
        DateTime scheduledAt, DateTime? scheduledEndAt)
    {
        var refereeUser = new User
        {
            Id = Guid.NewGuid(), Email = $"ref-{Guid.NewGuid()}@test.com", PasswordHash = "x",
            FullName = "Referee", Role = UserRole.Referee
        };
        var referee = new Referee
        {
            Id = Guid.NewGuid(), UserId = refereeUser.Id, LicenseNumber = $"LIC-{Guid.NewGuid()}", IsActive = true
        };
        // Race.TournamentId/RoundId are non-nullable FKs, so RaceRepository/RefereeAssignmentRepository's
        // Include(...).ThenInclude(Tournament/Round) compile to INNER JOINs — a Race pointing at a
        // Tournament/Round row that doesn't exist gets silently dropped from every query result.
        var tournament = new Tournament { Id = Guid.NewGuid(), Name = "Giải đấu test", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7) };
        var round = new Round { Id = Guid.NewGuid(), Name = "Vòng loại", TournamentId = tournament.Id, RoundNumber = 1, ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1) };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = "Vòng loại 711",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = scheduledAt,
            ScheduledEndAt = scheduledEndAt,
            Status = RaceStatus.RegistrationOpen
        };
        var assignment = new RefereeAssignment
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            RefereeId = referee.Id,
            Role = "Chief Referee",
            Status = RefereeAssignmentStatus.Assigned,
            AssignedAt = DateTime.UtcNow
        };

        _db.AddRange(refereeUser, referee, tournament, round, race, assignment);
        await _db.SaveChangesAsync();
        return (referee, race, assignment);
    }

    [Fact]
    public async Task GetRefereeAssignmentsAsync_ExposesRaceScheduledAt()
    {
        var scheduledAt = new DateTime(2026, 8, 19, 5, 45, 0, DateTimeKind.Utc);
        var (referee, _, _) = await SeedAssignmentAsync(scheduledAt, null);

        var result = await _service.GetRefereeAssignmentsAsync(referee.Id);

        Assert.True(result.IsSuccess);
        var response = Assert.Single(result.Result.Data!);
        Assert.Equal(scheduledAt, response.ScheduledAt);
    }

    [Fact]
    public async Task GetRefereeAssignmentsAsync_ExposesRaceScheduledEndAt_WhenPresent()
    {
        var scheduledAt = new DateTime(2026, 8, 19, 5, 45, 0, DateTimeKind.Utc);
        var scheduledEndAt = scheduledAt.AddMinutes(30);
        var (referee, _, _) = await SeedAssignmentAsync(scheduledAt, scheduledEndAt);

        var result = await _service.GetRefereeAssignmentsAsync(referee.Id);

        var response = Assert.Single(result.Result.Data!);
        Assert.Equal(scheduledEndAt, response.ScheduledEndAt);
    }

    [Fact]
    public async Task ConfirmAssignmentAsync_DoesNotChangeRaceScheduledAt()
    {
        var scheduledAt = new DateTime(2026, 8, 19, 5, 45, 0, DateTimeKind.Utc);
        var (_, race, assignment) = await SeedAssignmentAsync(scheduledAt, null);

        var result = await _service.ConfirmAssignmentAsync(
            new ConfirmRefereeAssignmentRequest { AssignmentId = assignment.Id });

        Assert.True(result.IsSuccess);
        Assert.Equal(RefereeAssignmentStatus.Confirmed.ToString(), result.Result.Data!.Status);

        var raceAfter = await _raceRepo.GetByIdAsync(race.Id);
        Assert.Equal(scheduledAt, raceAfter!.ScheduledAt);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
