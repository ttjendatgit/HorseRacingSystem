using System;
using System.Threading.Tasks;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public class AdminViolationResolutionTests
{
    [Fact]
    public async Task ResolveViolationAsync_UnknownViolation_ReturnsNotFound()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var result = await f.Admin.ResolveViolationAsync(Guid.NewGuid(), "Trừ 50% thưởng");

        Assert.False(result.Result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task ResolveViolationAsync_AlreadyResolved_ReturnsBadRequest()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var violation = await CreateViolationAsync(f, "resolved-flow", penalty: "Cấm thi đấu 1 mùa giải");

        var result = await f.Admin.ResolveViolationAsync(violation.Id, "Trừ 50% thưởng");

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        var stored = await f.Db.ViolationRecords.SingleAsync(v => v.Id == violation.Id);
        Assert.Equal("Cấm thi đấu 1 mùa giải", stored.Penalty);
    }

    [Fact]
    public async Task ResolveViolationAsync_UnresolvedViolationWithValidPenalty_SucceedsAndPersists()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var violation = await CreateViolationAsync(f, "unresolved-flow", penalty: null);

        var result = await f.Admin.ResolveViolationAsync(violation.Id, "Trừ 50% thưởng");

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(200, result.StatusCode);
        var stored = await f.Db.ViolationRecords.SingleAsync(v => v.Id == violation.Id);
        Assert.Equal("Trừ 50% thưởng", stored.Penalty);
    }

    private static async Task<ViolationRecord> CreateViolationAsync(
        RaceLifecycleTests.LifecycleFixture f,
        string tag,
        string? penalty)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var start = DateTime.UtcNow.AddDays(10);

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = $"Tournament {tag}",
            StartDate = start.Date,
            EndDate = start.Date.AddDays(3),
            Status = TournamentStatus.Published,
            IsActive = true,
            MaxRounds = 1,
            MaxParticipants = 8,
            RegistrationDeadline = start.Date.AddDays(-1)
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            Name = "Round 1",
            TournamentId = tournament.Id,
            RoundNumber = 1,
            AdvanceCount = 0,
            ScheduledStartDate = start,
            ScheduledEndDate = start.AddHours(2)
        };
        var race = new Race
        {
            Id = Guid.NewGuid(),
            Name = $"Race {tag}",
            TournamentId = tournament.Id,
            RoundId = round.Id,
            ScheduledAt = start.AddMinutes(10),
            ScheduledEndAt = start.AddMinutes(70),
            Status = RaceStatus.InProgress,
            MaxParticipants = 8,
            Distance = 1200
        };

        var ownerUser = new User { Id = Guid.NewGuid(), Email = $"owner-{tag}-{suffix}@test.local", PasswordHash = "x", FullName = $"Owner {tag}", Role = UserRole.HorseOwner, IsActive = true };
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = $"OWN-{suffix.Substring(0, 8)}" };
        var horse = new Horse { Id = Guid.NewGuid(), Name = $"Horse {tag}", OwnerId = owner.Id, ApprovalStatus = ApprovalStatus.Approved };
        var entry = new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Id, HorseId = horse.Id, Status = RegistrationStatus.Approved };

        var refereeUser = new User { Id = Guid.NewGuid(), Email = $"referee-{tag}-{suffix}@test.local", PasswordHash = "x", FullName = $"Referee {tag}", Role = UserRole.Referee, IsActive = true };
        var referee = new Referee { Id = Guid.NewGuid(), UserId = refereeUser.Id, LicenseNumber = $"REF-{suffix.Substring(0, 8)}", LicenseExpiryDate = DateTime.UtcNow.AddYears(1) };

        var violation = new ViolationRecord
        {
            Id = Guid.NewGuid(),
            RaceId = race.Id,
            RaceEntryId = entry.Id,
            RefereeId = referee.Id,
            ViolationType = ViolationType.Interference,
            Description = "Chèn ép ngựa khác trên đường đua",
            RecordedAt = DateTime.UtcNow,
            Penalty = penalty
        };

        f.Db.AddRange(tournament, round, race, ownerUser, owner, horse, entry, refereeUser, referee, violation);
        await f.Db.SaveChangesAsync();
        return violation;
    }
}
