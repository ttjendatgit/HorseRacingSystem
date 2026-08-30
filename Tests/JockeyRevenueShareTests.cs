using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests;

public class JockeyRevenueShareTests
{
    private static PrizeService MakePrizeService(RaceLifecycleTests.LifecycleFixture f)
        => new PrizeService(new PrizeRepository(f.Db), f.TournamentRepo, f.UnitOfWork, f.Db, f.RaceSvc, f.FaultWallet);

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

    [Fact]
    public async Task WalletService_AllowsJockeyWallet()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var jockeyUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "jockey@test.com",
            PasswordHash = "hash",
            FullName = "Jockey Test",
            Role = UserRole.Jockey,
            IsActive = true
        };
        f.Db.Users.Add(jockeyUser);
        await f.Db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var walletSvc = new WalletService(new WalletRepository(f.Db), new UserRepository(f.Db), f.UnitOfWork, config);
        var result = await walletSvc.AddPointsAsync(jockeyUser.Id, 300000m, "prize_test");

        Assert.True(result.Result.Success);
        var wallet = await f.Db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == jockeyUser.Id);
        Assert.NotNull(wallet);
        Assert.Equal(300000m, wallet.Balance);
    }

    [Fact]
    public async Task InviteJockey_PercentageValidation()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var ownerUser = new User { Id = Guid.NewGuid(), Email = "owner@test.com", Role = UserRole.HorseOwner, IsActive = true };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = "Fast Horse", ApprovalStatus = ApprovalStatus.Approved };
        
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Draft Tournament",
            Status = TournamentStatus.Published,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        var race = new Race
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Name = "Race 1",
            Status = RaceStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddDays(2)
        };

        var entry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            HorseId = horse.Id,
            Status = RegistrationStatus.Approved,
            OwnerConfirmed = true
        };

        var jockeyUser = new User { Id = Guid.NewGuid(), Email = "jockey_val@test.com", Role = UserRole.Jockey, IsActive = true };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser.Id, ApprovalStatus = ApprovalStatus.Approved };

        f.Db.Users.AddRange(ownerUser, jockeyUser);
        f.Db.Owners.Add(owner);
        f.Db.Jockeys.Add(jockey);
        f.Db.Horses.Add(horse);
        f.Db.Tournaments.Add(tournament);
        f.Db.Races.Add(race);
        f.Db.RaceEntries.Add(entry);
        await f.Db.SaveChangesAsync();

        var horseSvc = new HorseService(
            new HorseRepository(f.Db),
            new OwnerRepository(f.Db),
            new JockeyRepository(f.Db),
            f.RaceRepo,
            f.EntryRepo,
            new JockeyInvitationRepository(f.Db),
            f.UnitOfWork,
            new ThrowingNotificationService(),
            f.Db
        );

        var invalidReq = new JockeyInvitationCreateRequest
        {
            JockeyId = jockey.Id,
            RaceId = race.Id,
            JockeySharePercentage = 150m // Invalid (>100)
        };

        var result = await horseSvc.InviteJockeyAsync(ownerUser.Id, horse.Id, invalidReq);
        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task PrizeDistribute_SplitsBetweenOwnerAndJockey()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var ownerUser = new User { Id = Guid.NewGuid(), Email = "owner2@test.com", Role = UserRole.HorseOwner, IsActive = true };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id };
        var jockeyUser = new User { Id = Guid.NewGuid(), Email = "jockey2@test.com", Role = UserRole.Jockey, IsActive = true };
        var jockey = new Jockey { Id = Guid.NewGuid(), UserId = jockeyUser.Id, ApprovalStatus = ApprovalStatus.Approved };
        var horse = new Horse { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = "Champion Horse", ApprovalStatus = ApprovalStatus.Approved };

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "Grand Prix",
            Status = TournamentStatus.Finished,
            PrizePool = 1000000m,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(-1),
            MaxRounds = 1,
            MaxParticipants = 8
        };

        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Name = "Final Round",
            RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow.AddDays(-3),
            ScheduledEndDate = DateTime.UtcNow.AddDays(-1)
        };

        var race = new Race
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundId = round.Id,
            Name = "Final Race",
            Status = RaceStatus.Finished,
            ScheduledAt = DateTime.UtcNow.AddDays(-2),
            MaxParticipants = 8
        };

        var prize = new Prize
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Position = 1,
            PercentageOfPool = 100m,
            Amount = 1000000m,
            Currency = "VND",
            IsDistributed = false
        };

        var invitation = new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            RaceId = race.Id,
            Status = JockeyInvitationStatus.Accepted,
            JockeySharePercentage = 30m, // 30% Jockey, 70% Owner
            CreatedAt = DateTime.UtcNow
        };

        var raceEntry = new RaceEntry
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            HorseId = horse.Id,
            JockeyId = jockey.Id,
            FinishPosition = 1,
            OwnerConfirmed = true,
            JockeyConfirmed = true
        };

        var rankingJson = JsonSerializer.Serialize(new[]
        {
            new { HorseId = horse.Id, Position = 1, Status = "Completed" }
        });

        var raceResult = new RaceResult
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            WinningHorseId = horse.Id,
            RankingsJson = rankingJson,
            Status = RaceResultStatus.Official,
            ApprovedAt = DateTime.UtcNow
        };

        f.Db.Users.AddRange(ownerUser, jockeyUser);
        f.Db.Owners.Add(owner);
        f.Db.Jockeys.Add(jockey);
        f.Db.Horses.Add(horse);
        f.Db.Tournaments.Add(tournament);
        f.Db.Rounds.Add(round);
        f.Db.Races.Add(race);
        f.Db.Prizes.Add(prize);
        f.Db.JockeyInvitations.Add(invitation);
        f.Db.RaceEntries.Add(raceEntry);
        f.Db.RaceResults.Add(raceResult);
        await f.Db.SaveChangesAsync();

        var prizeSvc = MakePrizeService(f);
        var distResult = await prizeSvc.DistributeAsync(tournament.Id);

        Assert.True(distResult.Result.Success);
        Assert.Single(distResult.Result.Data!.Distributed);

        Assert.Equal(300000m, distResult.Result.Data!.Distributed[0].JockeyAmount);
        Assert.Equal(700000m, distResult.Result.Data.Distributed[0].OwnerAmount);
        Assert.Equal(30m, distResult.Result.Data.Distributed[0].JockeySharePercentage);

        var ownerWallet = await f.Db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == ownerUser.Id);
        var jockeyWallet = await f.Db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == jockeyUser.Id);

        Assert.NotNull(ownerWallet);
        Assert.NotNull(jockeyWallet);
        Assert.Equal(700000m, ownerWallet.Balance);
        Assert.Equal(300000m, jockeyWallet.Balance);

        var log = await f.Db.PrizeDistributionLogs.AsNoTracking().FirstOrDefaultAsync(l => l.PrizeId == prize.Id);
        Assert.NotNull(log);
        Assert.Equal(700000m, log.OwnerAmount);
        Assert.Equal(300000m, log.JockeyAmount);
        Assert.Equal(30m, log.JockeySharePercentage);

        var jockeyHistory = await prizeSvc.GetMyJockeyPrizeHistoryAsync(jockeyUser.Id);
        Assert.True(jockeyHistory.Result.Success);
        Assert.Single(jockeyHistory.Result.Data!);
        Assert.Equal(300000m, jockeyHistory.Result.Data![0].JockeyAmount);
    }
}
