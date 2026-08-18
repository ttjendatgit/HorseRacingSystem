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
/// Task B Final Correction — locks the Owner/Jockey business boundary: Owner owns/manages Horses
/// and submits Tournament registrations; Jockey must not create/manage Horses or submit
/// TournamentHorseRegistration. Two kinds of checks here:
///
/// 1. Declarative [Authorize(Roles=...)] checks via reflection — the actual gate lives entirely
///    in ASP.NET Core's authorization middleware (method-level Authorize ANDs with class-level),
///    which never runs when a controller is instantiated directly in-process (the pattern this
///    whole Tests project uses — no WebApplicationFactory/HTTP pipeline exists anywhere in this
///    suite). Reflection is the only way to verify the declared role gate without introducing a
///    new, heavier integration-test harness the codebase doesn't otherwise use.
/// 2. Behavioral checks for logic that DOES run inside the action/service body (ownership
///    verification), reusing the direct-instantiation pattern already established in
///    TournamentRegistrationTests.cs / Phase5StructuralTests.cs.
/// </summary>
public class HorseOwnerAuthorizationTests
{
    // ── Reflection helpers ──────────────────────────────────────────────

    private static AuthorizeAttribute? GetMethodAuthorize(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        return method?.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().FirstOrDefault();
    }

    private static bool HasAllowAnonymous(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        return method?.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Any() ?? false;
    }

    private static string[] RolesOf(AuthorizeAttribute? attr) =>
        (attr?.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ── DECLARATIVE: Jockey excluded from Horse create/manage + Tournament registration ────

    [Theory]
    [InlineData(nameof(HorsesController.CreateHorse))]
    [InlineData(nameof(HorsesController.UpdateHorse))]
    [InlineData(nameof(HorsesController.DeleteHorse))]
    [InlineData(nameof(HorsesController.RegisterHorse))]
    public void Jockey_HorseManagementActions_Rejected(string methodName)
    {
        var attr = GetMethodAuthorize(typeof(HorsesController), methodName);
        Assert.NotNull(attr);
        var roles = RolesOf(attr);
        Assert.DoesNotContain("Jockey", roles);
        Assert.Contains("HorseOwner", roles);
    }

    [Fact]
    public void Jockey_CreateHorse_Rejected()
    {
        var attr = GetMethodAuthorize(typeof(HorsesController), nameof(HorsesController.CreateHorse));
        Assert.NotNull(attr);
        Assert.DoesNotContain("Jockey", RolesOf(attr));
    }

    [Fact]
    public void Jockey_RegisterTournamentHorse_Rejected()
    {
        var attr = GetMethodAuthorize(typeof(TournamentRegistrationsController), nameof(TournamentRegistrationsController.Register));
        Assert.NotNull(attr);
        var roles = RolesOf(attr);
        Assert.DoesNotContain("Jockey", roles);
        Assert.Equal(new[] { "HorseOwner" }, roles); // Owner-only, per Task B Final Correction §3
    }

    [Fact]
    public void UnauthenticatedProtectedOperations_Rejected()
    {
        // No [AllowAnonymous] anywhere on these actions, and an [Authorize] requirement exists
        // (method- or class-level) — ASP.NET Core rejects an unauthenticated caller by default
        // whenever any Authorize requirement applies and no AllowAnonymous overrides it.
        Assert.False(HasAllowAnonymous(typeof(HorsesController), nameof(HorsesController.CreateHorse)));
        Assert.True(typeof(HorsesController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Any());

        Assert.False(HasAllowAnonymous(typeof(TournamentRegistrationsController), nameof(TournamentRegistrationsController.Register)));
        Assert.NotNull(GetMethodAuthorize(typeof(TournamentRegistrationsController), nameof(TournamentRegistrationsController.Register)));
    }

    // ── BEHAVIORAL: ownership checks that run inside the action/service body ───────────────

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

    /// <summary>Throws on every call — a safety net proving CreateHorseAsync/UpdateHorseAsync/DeleteHorseAsync never touch notifications (verified by code inspection), matching the existing FakeXxxService(NotSupportedException) convention in RaceLifecycleTests.cs.</summary>
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

    [Fact]
    public async Task Owner_CreateHorse_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "owner-create@test.com", PasswordHash = "x", FullName = "Owner", Role = UserRole.HorseOwner };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = userId, OwnerCode = "OWN-CREATE" };
        f.Db.AddRange(user, owner);
        await f.Db.SaveChangesAsync();

        var service = BuildHorseService(f);
        var result = await service.CreateHorseAsync(userId, new HorseCreateRequest { Name = "New Horse" });
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task AnotherOwner_UpdateHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (_, otherUserId, _) = await CreateApprovedOwnerHorseAsync(f, "b");

        var service = BuildHorseService(f);
        var result = await service.UpdateHorseAsync(otherUserId, horseId, new HorseUpdateRequest { Name = "Hijacked" });
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task AnotherOwner_DeleteHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (_, otherUserId, _) = await CreateApprovedOwnerHorseAsync(f, "b");

        var service = BuildHorseService(f);
        var result = await service.DeleteHorseAsync(otherUserId, horseId);
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Owner_RegisterOwnHorse_Allowed()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, userId, horseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var start = DateTime.UtcNow.AddDays(10);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = "T", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = 10, MinParticipants = 3,
            Status = TournamentStatus.Published, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();

        var controller = BuildRegistrationController(f, userId);
        var (status, message) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournament.Id, HorseId = horseId }));
        Assert.True(status is 200 or 201, message ?? "expected success");
    }

    [Fact]
    public async Task Owner_RegisterAnotherOwnersHorse_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, _, otherOwnersHorseId) = await CreateApprovedOwnerHorseAsync(f, "a");
        var (_, callingUserId, _) = await CreateApprovedOwnerHorseAsync(f, "b");
        var start = DateTime.UtcNow.AddDays(10);
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = "T", StartDate = start, EndDate = start.AddDays(5),
            RegistrationDeadline = start.AddDays(-1), MaxParticipants = 10, MinParticipants = 3,
            Status = TournamentStatus.Published, CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();

        var controller = BuildRegistrationController(f, callingUserId);
        var (status, _) = Unwrap(await controller.Register(new RegisterTournamentHorseRequest { TournamentId = tournament.Id, HorseId = otherOwnersHorseId }));
        Assert.Equal(404, status);
    }
}
