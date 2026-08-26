using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Task C1 §1: DeleteHorseAsync must hard-delete a Horse with zero participation history, and
/// archive (Horse.IsArchived = true, row preserved) a Horse that has any — proven directly against
/// the 9-table HasParticipationHistoryAsync check in HorseService, reusing
/// RaceLifecycleTests.LifecycleFixture like the rest of this suite.
/// Another-owner-cannot-delete/archive is already covered by
/// HorseOwnerAuthorizationTests.AnotherOwner_DeleteHorse_Rejected (unchanged 404 ownership gate).
/// </summary>
public class HorseArchiveTests
{
    private static async Task<(Guid ownerId, Guid userId, Guid horseId)> CreateApprovedOwnerHorseAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag)
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = $"owner-{tag}@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = userId, OwnerCode = $"OWN-{tag}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse-{tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        f.Db.AddRange(user, owner, horse);
        await f.Db.SaveChangesAsync();
        return (owner.Id, userId, horse.Id);
    }

    private static async Task<Guid> CreateTournamentAsync(RaceLifecycleTests.LifecycleFixture f, TournamentStatus status, DateTime start)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = 10, MinParticipants = 3,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task<Guid> RegisterAsync(RaceLifecycleTests.LifecycleFixture f, Guid tournamentId, Guid horseId, Guid ownerId, RegistrationStatus status)
    {
        var registration = new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, HorseId = horseId, OwnerId = ownerId,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(registration);
        await f.Db.SaveChangesAsync();
        return registration.Id;
    }

    private static async Task<Guid> CreateTrackAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var track = new Track { Id = Guid.NewGuid(), Name = $"Track-{Guid.NewGuid():N}", Capacity = 10, CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();
        return track.Id;
    }

    /// <summary>An Admin User with deliberately NO Owner row — proves the isAdmin path never resolves ownership.</summary>
    private static async Task<Guid> CreateAdminUserAsync(RaceLifecycleTests.LifecycleFixture f, string tag = "admin")
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = $"{tag}@test.com", PasswordHash = "x", FullName = "Admin", Role = UserRole.Admin };
        f.Db.Add(user);
        await f.Db.SaveChangesAsync();
        return userId;
    }

    /// <summary>Draft Tournament + Round + Race, ready for AssignHorseToRaceAsync (Draft-only gate).</summary>
    private static async Task<(Guid tournamentId, Guid raceId)> CreateDraftTournamentWithRaceAsync(RaceLifecycleTests.LifecycleFixture f)
    {
        var start = DateTime.UtcNow.AddDays(10);
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Draft, start);
        var round = new Round { Id = Guid.NewGuid(), Name = "R1", TournamentId = tournamentId, RoundNumber = 1, ScheduledStartDate = start, ScheduledEndDate = start.AddDays(1) };
        f.Db.Add(round);
        await f.Db.SaveChangesAsync();
        var track = await CreateTrackAsync(f);
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race", TournamentId = tournamentId, RoundId = round.Id,
            ScheduledAt = start, ScheduledEndAt = start.AddHours(1), TrackId = track, MaxParticipants = 10
        });
        return (tournamentId, race.Result.Data!.Id);
    }

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUserNotificationsAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter) => throw new NotSupportedException();
        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId) => throw new NotSupportedException();
        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto) => throw new NotSupportedException();
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId) => throw new NotSupportedException();
        public Task ProcessUnsentNotificationsAsync() => throw new NotSupportedException();
    }

    private static HorseService BuildHorseService(RaceLifecycleTests.LifecycleFixture f)
    {
        return new HorseService(
            new HorseRepository(f.Db), new OwnerRepository(f.Db), new JockeyRepository(f.Db),
            f.RaceRepo, f.EntryRepo, new JockeyInvitationRepository(f.Db), f.UnitOfWork,
            new ThrowingNotificationService(), f.Db);
    }

    private static TournamentRegistrationsController BuildRegistrationController(RaceLifecycleTests.LifecycleFixture f, Guid userId)
    {
        var controller = new TournamentRegistrationsController(f.Db, f.UnitOfWork);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static (int status, string? message) Unwrap(ActionResult result)
    {
        if (result is ObjectResult obj)
            return (obj.StatusCode ?? 200, obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value) as string);
        throw new InvalidOperationException($"Unexpected result type {result.GetType()}");
    }

    [Fact]
    public async Task DeleteHorse_NoHistory_HardDeleted()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(userId, horseId);
        Assert.True(result.Result.Success, result.Result.Message);

        Assert.False(await f.Db.Horses.AnyAsync(h => h.Id == horseId));
    }

    [Fact]
    public async Task DeleteHorse_HasTournamentRegistration_Archived()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(userId, horseId);
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Contains("lưu trữ", result.Result.Data ?? "");

        var horse = await f.Db.Horses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == horseId);
        Assert.NotNull(horse);
        Assert.True(horse!.IsArchived);
        // The registration itself must survive untouched — archiving must never cascade-delete history.
        Assert.True(await f.Db.TournamentHorseRegistrations.AnyAsync(r => r.HorseId == horseId));
    }

    [Fact]
    public async Task DeleteHorse_HasRaceEntry_Archived()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (tournamentId, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Approved);
        var assign = await f.RaceManagement.AssignHorseToRaceAsync(raceId, new AssignHorseToRaceRequest { HorseId = horseId });
        Assert.True(assign.Result.Success, assign.Result.Message);

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(userId, horseId);
        Assert.True(result.Result.Success, result.Result.Message);

        var horse = await f.Db.Horses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == horseId);
        Assert.NotNull(horse);
        Assert.True(horse!.IsArchived);
        Assert.True(await f.Db.RaceEntries.AnyAsync(e => e.HorseId == horseId));
    }

    [Fact]
    public async Task DeleteHorse_HasRaceResultWin_ArchivedWithoutException()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, raceId) = await CreateDraftTournamentWithRaceAsync(f);
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        f.Db.Add(new RaceResult { Id = Guid.NewGuid(), RaceId = raceId, WinningHorseId = horseId, RecordedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        var service = BuildHorseService(f);
        // Must not throw an FK-violation exception — the archive path is chosen up front via the
        // history check, never by letting the DB reject a hard delete.
        var result = await service.DeleteHorseAsync(userId, horseId);
        Assert.True(result.Result.Success, result.Result.Message);

        var horse = await f.Db.Horses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == horseId);
        Assert.NotNull(horse);
        Assert.True(horse!.IsArchived);
    }

    [Fact]
    public async Task ArchivedHorse_CannotRegisterForNewTournament()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var oldTournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(1));
        await RegisterAsync(f, oldTournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var service = BuildHorseService(f);
        var deleteResult = await service.DeleteHorseAsync(userId, horseId);
        Assert.True(deleteResult.Result.Success, deleteResult.Result.Message);
        Assert.True((await f.Db.Horses.AsNoTracking().FirstAsync(h => h.Id == horseId)).IsArchived);

        var newTournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(30));
        var controller = BuildRegistrationController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = newTournamentId, HorseId = horseId }));
        Assert.Equal(400, status);
        Assert.Contains("lưu trữ", message ?? "");
    }

    // ── PRE-MIGRATION CORRECTION §1: Admin Horse CRUD regression check ──────────────────────
    // Declarative HorseOwner+Admin access on Create/Update/Delete was never stripped (still
    // "HorseOwner,Admin" on all three) — the actual gap was that the service body always resolved
    // ownership via GetOwnerProfileAsync, which 404s for an Admin (no Owner row). isAdmin=true now
    // bypasses that resolution entirely instead of requiring one. Owner-can/another-Owner-cannot/
    // Jockey-cannot are already covered by DeleteHorse_NoHistory_HardDeleted (above) and the
    // preserved HorseOwnerAuthorizationTests.AnotherOwner_DeleteHorse_Rejected /
    // Jockey_HorseManagementActions_Rejected.

    [Fact]
    public void DeclaredAuthorization_CreateUpdateDelete_HorseOwnerAndAdmin()
    {
        foreach (var methodName in new[]
                 {
                     nameof(HorsesController.CreateHorse), nameof(HorsesController.UpdateHorse), nameof(HorsesController.DeleteHorse)
                 })
        {
            var method = typeof(HorsesController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            var attr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().FirstOrDefault();
            Assert.NotNull(attr);
            var roles = (attr!.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Contains("HorseOwner", roles);
            Assert.Contains("Admin", roles);
        }
    }

    [Fact]
    public async Task DeleteHorse_Admin_NoHistory_HardDeletesAnyHorseWithoutOwnerRow()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var adminUserId = await CreateAdminUserAsync(f);

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(adminUserId, horseId, isAdmin: true);
        Assert.True(result.Result.Success, result.Result.Message);
        Assert.False(await f.Db.Horses.AnyAsync(h => h.Id == horseId));
    }

    [Fact]
    public async Task DeleteHorse_Admin_HasHistory_ArchivesAnyHorseWithoutOwnerRow()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);
        var adminUserId = await CreateAdminUserAsync(f);

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(adminUserId, horseId, isAdmin: true);
        Assert.True(result.Result.Success, result.Result.Message);

        var horse = await f.Db.Horses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == horseId);
        Assert.NotNull(horse);
        Assert.True(horse!.IsArchived);
    }

    [Fact]
    public async Task UpdateHorse_Admin_NoOwnerRow_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var adminUserId = await CreateAdminUserAsync(f);

        var service = BuildHorseService(f);
        var result = await service.UpdateHorseAsync(adminUserId, horseId, new HorseUpdateRequest { Name = "Renamed by admin", Weight = 480, Height = 165 }, isAdmin: true);
        Assert.True(result.Result.Success, result.Result.Message);

        var horse = await f.Db.Horses.AsNoTracking().FirstAsync(h => h.Id == horseId);
        Assert.Equal("Renamed by admin", horse.Name);
    }

    // ── PRE-MIGRATION CORRECTION §2: archived Horse UX / participation gates ────────────────

    [Fact]
    public async Task UpdateHorse_ArchivedHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var service = BuildHorseService(f);
        var deleteResult = await service.DeleteHorseAsync(userId, horseId);
        Assert.True((await f.Db.Horses.AsNoTracking().FirstAsync(h => h.Id == horseId)).IsArchived, deleteResult.Result.Message);

        var updateResult = await service.UpdateHorseAsync(userId, horseId, new HorseUpdateRequest { Name = "Should not apply" });
        Assert.False(updateResult.Result.Success);
        Assert.Equal(400, updateResult.StatusCode);
        Assert.Contains("lưu trữ", updateResult.Result.Message);

        var horse = await f.Db.Horses.AsNoTracking().FirstAsync(h => h.Id == horseId);
        Assert.NotEqual("Should not apply", horse.Name);
    }

    [Fact]
    public async Task UpdateHorse_Admin_ArchivedHorse_AlsoRejected()
    {
        // The archived-edit gate applies uniformly to Admin too — no separate admin bypass was
        // introduced for it (smallest consistent behavior, per the correction instructions).
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);
        var adminUserId = await CreateAdminUserAsync(f);

        var service = BuildHorseService(f);
        await service.DeleteHorseAsync(userId, horseId);

        var updateResult = await service.UpdateHorseAsync(adminUserId, horseId, new HorseUpdateRequest { Name = "Should not apply" }, isAdmin: true);
        Assert.False(updateResult.Result.Success);
        Assert.Equal(400, updateResult.StatusCode);
    }

    [Fact]
    public async Task InviteJockey_ArchivedHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (ownerId, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var tournamentId = await CreateTournamentAsync(f, TournamentStatus.Published, DateTime.UtcNow.AddDays(10));
        await RegisterAsync(f, tournamentId, horseId, ownerId, RegistrationStatus.Pending);

        var service = BuildHorseService(f);
        await service.DeleteHorseAsync(userId, horseId);
        Assert.True((await f.Db.Horses.AsNoTracking().FirstAsync(h => h.Id == horseId)).IsArchived);

        var jockeyUserId = Guid.NewGuid();
        var jockeyUser = new User { Id = jockeyUserId, Email = "jockey-invite@test.com", PasswordHash = "x", FullName = "Jockey", Role = UserRole.Jockey };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUserId, LicenseNumber = "LIC-1" };
        f.Db.AddRange(jockeyUser, jockey);
        await f.Db.SaveChangesAsync();

        var result = await service.InviteJockeyAsync(userId, horseId, new JockeyInvitationCreateRequest { JockeyId = jockey.Id, RaceId = Guid.NewGuid() });
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("lưu trữ", result.Result.Message);
    }
}
