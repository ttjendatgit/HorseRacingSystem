using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// J-ADMIN-REVIEW: focused tests for the behavior actually changed by this task —
/// (1) AdminService.RejectJockeyAsync now enforces a non-blank reason server-side instead of the
/// controller silently substituting "Không có lý do"; (2) the new Admin-only Jockey detail
/// endpoint/DTO; (3) GET /api/jockeys/me now also returns ApprovalNote. ApproveJockeyAsync's
/// existing "clears ApprovalNote" behavior was NOT changed — asserted here as a regression lock,
/// not a new feature. Uses the same direct-service-instantiation pattern as the rest of this test
/// suite (RaceLifecycleTests.LifecycleFixture.Admin, and JockeyService built the same way
/// J3OwnerFinalConfirmTests does) — no WebApplicationFactory/HTTP pipeline exists anywhere here.
/// </summary>
public class JAdminReviewTests
{
    private static async Task<(Guid userId, Guid jockeyId)> CreateJockeyAsync(
        RaceLifecycleTests.LifecycleFixture f, string tag, ApprovalStatus approvalStatus = ApprovalStatus.Pending,
        string? approvalNote = null, bool userActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"jockey-{tag}-{suffix}@test.local",
            PasswordHash = "x",
            FullName = $"Jockey {tag}",
            Role = UserRole.Jockey,
            IsActive = userActive,
        };
        var jockey = new Jockey
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            LicenseNumber = $"LIC-{suffix.Substring(0, 8)}",
            LicenseFile = $"https://res.cloudinary.com/demo/documents/{suffix}.pdf",
            Phone = "0900000000",
            Address = "123 Test Street",
            IdCardNumber = "079000000000",
            DateOfBirth = new DateTime(1995, 1, 1),
            Height = 165.5m,
            Weight = 55.0m,
            ApprovalStatus = approvalStatus,
            ApprovalNote = approvalNote,
        };
        f.Db.AddRange(user, jockey);
        await f.Db.SaveChangesAsync();
        return (user.Id, jockey.Id);
    }

    private static JockeyService BuildJockeyService(RaceLifecycleTests.LifecycleFixture f)
        => new(
            new UserRepository(f.Db),
            new JockeyRepository(f.Db),
            new JockeyInvitationRepository(f.Db),
            new RaceEntryRepository(f.Db),
            new RaceRepository(f.Db),
            f.UnitOfWork,
            new NoopNotificationService());

    // ── ApprovalNote exposed on the Jockey's own profile (Part 8) ──────────────────────────────

    [Fact]
    public async Task JockeySelfProfile_Rejected_ReturnsApprovalNote()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (userId, _) = await CreateJockeyAsync(f, "self-rejected", ApprovalStatus.Rejected, "Giấy phép thi đấu không hợp lệ.");

        var result = await BuildJockeyService(f).GetMyProfileAsync(userId);

        Assert.True(result.Result.Success, result.Result.Message);
        var data = result.Result.Data!;
        var type = data.GetType();
        Assert.Equal("Rejected", type.GetProperty("approvalStatus")!.GetValue(data));
        Assert.Equal("Giấy phép thi đấu không hợp lệ.", type.GetProperty("approvalNote")!.GetValue(data));
    }

    [Fact]
    public async Task JockeySelfProfile_Pending_ApprovalNoteIsNull()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (userId, _) = await CreateJockeyAsync(f, "self-pending", ApprovalStatus.Pending);

        var result = await BuildJockeyService(f).GetMyProfileAsync(userId);

        Assert.True(result.Result.Success, result.Result.Message);
        var data = result.Result.Data!;
        Assert.Null(data.GetType().GetProperty("approvalNote")!.GetValue(data));
    }

    // ── Reject requires a real reason, enforced server-side (Part 7) ───────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectJockey_BlankReason_Rejected(string? reason)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, jockeyId) = await CreateJockeyAsync(f, $"blank-{reason?.Length ?? -1}", ApprovalStatus.Pending);

        var result = await f.Admin.RejectJockeyAsync(jockeyId, reason);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Vui lòng nhập lý do từ chối hồ sơ kỵ sĩ.", result.Result.Message);

        var jockey = await f.Db.Jockeys.SingleAsync(j => j.Id == jockeyId);
        Assert.Equal(ApprovalStatus.Pending, jockey.ApprovalStatus); // unchanged
        Assert.Null(jockey.ApprovalNote);
    }

    [Fact]
    public async Task RejectJockey_ValidReason_SavesApprovalNote()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, jockeyId) = await CreateJockeyAsync(f, "valid-reason", ApprovalStatus.Pending);

        var result = await f.Admin.RejectJockeyAsync(jockeyId, "  Giấy phép thi đấu không hợp lệ.  ");

        Assert.True(result.Result.Success, result.Result.Message);
        var jockey = await f.Db.Jockeys.SingleAsync(j => j.Id == jockeyId);
        Assert.Equal(ApprovalStatus.Rejected, jockey.ApprovalStatus);
        // Trimmed, exact reason persisted (no substitution/mutation beyond trimming whitespace).
        Assert.Equal("Giấy phép thi đấu không hợp lệ.", jockey.ApprovalNote);
    }

    [Fact]
    public async Task RejectJockey_UnknownJockey_Returns404()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.Admin.RejectJockeyAsync(Guid.NewGuid(), "Lý do hợp lệ");
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Approve clears any prior ApprovalNote (pre-existing behavior, regression-locked) ───────

    [Fact]
    public async Task ApproveJockey_ClearsOldApprovalNote()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (_, jockeyId) = await CreateJockeyAsync(f, "clears-note", ApprovalStatus.Rejected, "Lý do cũ");

        var result = await f.Admin.ApproveJockeyAsync(jockeyId);

        Assert.True(result.Result.Success, result.Result.Message);
        var jockey = await f.Db.Jockeys.SingleAsync(j => j.Id == jockeyId);
        Assert.Equal(ApprovalStatus.Approved, jockey.ApprovalStatus);
        Assert.Null(jockey.ApprovalNote);
    }

    // ── Admin Jockey detail endpoint/DTO (Part 2/3/5) ───────────────────────────────────────────

    [Fact]
    public async Task GetJockeyDetail_ReturnsFullReviewFields()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (userId, jockeyId) = await CreateJockeyAsync(f, "full-detail", ApprovalStatus.Pending);

        var result = await f.Admin.GetJockeyDetailAsync(jockeyId);

        Assert.True(result.Result.Success, result.Result.Message);
        var detail = result.Result.Data!;
        Assert.Equal(jockeyId, detail.Id);
        Assert.Equal(userId, detail.UserId);
        Assert.Equal("Jockey full-detail", detail.FullName);
        Assert.True(detail.IsActive);
        Assert.Equal("0900000000", detail.Phone);
        Assert.Equal("123 Test Street", detail.Address);
        Assert.Equal("079000000000", detail.IdCardNumber);
        Assert.NotNull(detail.DateOfBirth);
        Assert.Equal(165.5m, detail.Height);
        Assert.Equal(55.0m, detail.Weight);
        Assert.StartsWith("LIC-", detail.LicenseNumber);
        Assert.Contains("cloudinary", detail.LicenseFile);
        Assert.Equal("Pending", detail.ApprovalStatus);
        Assert.Null(detail.ApprovalNote);
    }

    [Fact]
    public async Task GetJockeyDetail_UnknownJockey_Returns404()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await f.Admin.GetJockeyDetailAsync(Guid.NewGuid());
        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Security: sensitive fields never leak through the public/Owner-facing list DTO ─────────

    [Fact]
    public void JockeyListResponse_DoesNotExposeSensitiveReviewFields()
    {
        var propertyNames = typeof(JockeyListResponse).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Phone", propertyNames);
        Assert.DoesNotContain("Address", propertyNames);
        Assert.DoesNotContain("IdCardNumber", propertyNames);
        Assert.DoesNotContain("LicenseFile", propertyNames);
        Assert.DoesNotContain("ApprovalNote", propertyNames);
        Assert.DoesNotContain("DateOfBirth", propertyNames);
    }

    [Fact]
    public void AdminController_IsAdminOnlyAtClassLevel()
    {
        // GetJockeyDetail has no method-level [Authorize] of its own — it relies entirely on
        // AdminController's class-level gate, so that gate must actually be Admin-only.
        var attr = typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("Admin", attr!.Roles);
    }

    // ── Regression: Owner-visible availability behavior is unaffected (Part 10) ────────────────

    [Fact]
    public async Task GetAvailableJockeys_PendingAndRejected_StillHiddenFromOwner()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var (pendingUserId, _) = await CreateJockeyAsync(f, "avail-pending", ApprovalStatus.Pending);
        var (rejectedUserId, _) = await CreateJockeyAsync(f, "avail-rejected", ApprovalStatus.Rejected, "no");
        var (approvedUserId, _) = await CreateJockeyAsync(f, "avail-approved", ApprovalStatus.Approved);
        var callerUserId = Guid.NewGuid();

        var jockeyService = BuildJockeyService(f);
        var result = await jockeyService.GetAvailableJockeysAsync(callerUserId, includeUnapproved: false);

        Assert.True(result.Result.Success, result.Result.Message);
        var list = ((System.Collections.IEnumerable)result.Result.Data!).Cast<JockeyListResponse>().ToList();
        Assert.Contains(list, j => j.UserId == approvedUserId);
        Assert.DoesNotContain(list, j => j.UserId == pendingUserId);
        Assert.DoesNotContain(list, j => j.UserId == rejectedUserId);
    }

    private sealed class NoopNotificationService : HorseRacing.Services.Interfaces.INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto)
            => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUserNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));
        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id)
            => Task.FromResult(ServiceResult<NotificationDetailDto>.Ok(new NotificationDetailDto()));
        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId)
            => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId)
            => Task.FromResult(ServiceResult<int>.Ok(0));
        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId)
            => Task.FromResult(ServiceResult<NotificationStatsDto>.Ok(new NotificationStatsDto()));
        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto)
            => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<System.Collections.Generic.List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId)
            => Task.FromResult(ServiceResult<System.Collections.Generic.List<NotificationDto>>.Ok(new System.Collections.Generic.List<NotificationDto>()));
        public Task ProcessUnsentNotificationsAsync()
            => Task.CompletedTask;
    }
}
