using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

/// <summary>
/// Regression coverage for DemoSeeder.SeedOwnerDemoAsync — the full OWNER-DEMO-SEED spec
/// (Bến Tre / Ba Tri / TP.Hồ Chí Minh) staged around a real existing HorseOwner plus real
/// existing Jockey/Referee accounts, resolved by email and never replaced.
/// </summary>
public class DemoSeederOwnerDemoTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ServiceProvider _provider;

    public DemoSeederOwnerDemoTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton<ILogger<ApplicationDbContext>>(NullLogger<ApplicationDbContext>.Instance);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>Seeds exactly the required identities the spec says SeedOwnerDemoAsync must
    /// resolve-and-reuse: the target Owner (with its 4 pre-existing Horses, to prove they're
    /// never touched), the 2 named Jockeys (Approved), the 2 named Referees (active,
    /// non-expired), and one Admin.</summary>
    private async Task<Owner> SeedRequiredIdentitiesAsync()
    {
        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;

        var ownerUser = new User { Id = Guid.NewGuid(), Email = "chungua1@final.com", FullName = "Chu Ngua Demo", Role = UserRole.HorseOwner, IsActive = true, CreatedAt = now };
        ownerUser.PasswordHash = hasher.HashPassword(ownerUser, "Owner@123");
        var owner = new Owner { Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = "OWN-FINAL-001", OwnerType = "Ca nhan", JoinDate = now, Status = "Dang hoat dong", CreatedAt = now, UpdatedAt = now };

        var existingHorses = new[] { "Alain", "Alex", "Blink", "Cadilac" }.Select(n => new Horse
        {
            Id = Guid.NewGuid(), Name = n, OwnerId = owner.Id, Breed = "Thoroughbred", Gender = "Mare",
            DateOfBirth = now.AddYears(-5), Age = 5, Weight = 480m, Height = 1.55m, Color = "Nau",
            TotalRaces = 5, TotalWins = 1, ApprovalStatus = ApprovalStatus.Approved
        }).ToList();

        var jockey2User = new User { Id = Guid.NewGuid(), Email = "jockey2@final.com", FullName = "Jockey Two", Role = UserRole.Jockey, IsActive = true, CreatedAt = now };
        jockey2User.PasswordHash = hasher.HashPassword(jockey2User, "Jockey@123");
        var jockey2 = new Jockey { Id = Guid.NewGuid(), UserId = jockey2User.Id, LicenseNumber = "JKY-J2", ApprovalStatus = ApprovalStatus.Approved, Status = "Dang hoat dong", CreatedAt = now, UpdatedAt = now };

        var rcJockeyUser = new User { Id = Guid.NewGuid(), Email = "rc-smoke-jockey@rc-smoke.local", FullName = "RC Smoke Jockey", Role = UserRole.Jockey, IsActive = true, CreatedAt = now };
        rcJockeyUser.PasswordHash = hasher.HashPassword(rcJockeyUser, "Jockey@123");
        var rcJockey = new Jockey { Id = Guid.NewGuid(), UserId = rcJockeyUser.Id, LicenseNumber = "JKY-RC", ApprovalStatus = ApprovalStatus.Approved, Status = "Dang hoat dong", CreatedAt = now, UpdatedAt = now };

        var refAUser = new User { Id = Guid.NewGuid(), Email = "rc-smoke-referee-a@rc-smoke.local", FullName = "RC Smoke Referee A", Role = UserRole.Referee, IsActive = true, CreatedAt = now };
        refAUser.PasswordHash = hasher.HashPassword(refAUser, "Referee@123");
        var refA = new Referee { Id = Guid.NewGuid(), UserId = refAUser.Id, LicenseNumber = "REF-A", IsActive = true, LicenseExpiryDate = now.AddYears(1), CreatedAt = now };

        var refBUser = new User { Id = Guid.NewGuid(), Email = "rc-smoke-referee-b@rc-smoke.local", FullName = "RC Smoke Referee B", Role = UserRole.Referee, IsActive = true, CreatedAt = now };
        refBUser.PasswordHash = hasher.HashPassword(refBUser, "Referee@123");
        var refB = new Referee { Id = Guid.NewGuid(), UserId = refBUser.Id, LicenseNumber = "REF-B", IsActive = true, LicenseExpiryDate = now.AddYears(1), CreatedAt = now };

        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@final.com", FullName = "Admin", Role = UserRole.Admin, IsActive = true, CreatedAt = now };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin@123");

        _db.AddRange(ownerUser, owner);
        _db.AddRange(existingHorses);
        _db.AddRange(jockey2User, jockey2, rcJockeyUser, rcJockey, refAUser, refA, refBUser, refB, adminUser);
        await _db.SaveChangesAsync();

        return owner;
    }

    private Task<int> CountAsync<T>(IQueryable<T> query) => query.CountAsync();

    private async Task<int[]> SnapshotCountsAsync() => new[]
    {
        await CountAsync(_db.Tournaments),
        await CountAsync(_db.Rounds),
        await CountAsync(_db.Races),
        await CountAsync(_db.Horses),
        await CountAsync(_db.TournamentHorseRegistrations),
        await CountAsync(_db.RaceEntries),
        await CountAsync(_db.RaceResults),
        await CountAsync(_db.JockeyInvitations),
        await CountAsync(_db.RefereeAssignments),
        await CountAsync(_db.HorseHealthChecks),
        await CountAsync(_db.RaceComplaints),
        await CountAsync(_db.RaceComplaintEvidence),
        await CountAsync(_db.Prizes),
    };

    [Fact]
    public async Task SeedOwnerDemoAsync_RunTwice_DoesNotDuplicateAnyDemoEntity()
    {
        await SeedRequiredIdentitiesAsync();

        await DemoSeeder.SeedOwnerDemoAsync(_provider);
        var afterFirstRun = await SnapshotCountsAsync();

        await DemoSeeder.SeedOwnerDemoAsync(_provider);
        var afterSecondRun = await SnapshotCountsAsync();

        Assert.Equal(afterFirstRun, afterSecondRun);
        // Sanity: the first run actually created something (a truly no-op seeder would also
        // trivially pass the equality check above).
        Assert.True(afterFirstRun.Sum() > 0);
    }

    [Fact]
    public async Task SeedOwnerDemoAsync_Rerun_RepairsStaleValuesBackToCanonical()
    {
        await SeedRequiredIdentitiesAsync();
        await DemoSeeder.SeedOwnerDemoAsync(_provider);
        var afterFirstRun = await SnapshotCountsAsync();

        // Simulate a stale row left over from an earlier version of the seeder (or hand-editing)
        // by mutating existing demo rows directly, bypassing DemoSeeder entirely.
        var benTre = await _db.Tournaments.FirstAsync(t => t.Name == "Giải đấu Bến Tre");
        benTre.Description = "[OWNER-DEMO-SEED] stale";
        benTre.MaxParticipants = null;
        benTre.SurfaceType = null;
        benTre.PrizePool = 0;

        var baTriRace = await _db.Races.FirstAsync(r => r.Name == "Cuộc đua Vô địch Ba Tri");
        var baTriResult = await _db.RaceResults.FirstAsync(r => r.RaceId == baTriRace.Id);
        baTriResult.Notes = "[OWNER-DEMO-SEED] stale provisional";

        var tpHcmRace = await _db.Races.FirstAsync(r => r.Name == "Cuộc đua Vô địch TP.Hồ Chí Minh");
        var tpHcmResult = await _db.RaceResults.FirstAsync(r => r.RaceId == tpHcmRace.Id);
        tpHcmResult.Notes = "[OWNER-DEMO-SEED] stale official";

        await _db.SaveChangesAsync();

        await DemoSeeder.SeedOwnerDemoAsync(_provider);
        var afterRepairRun = await SnapshotCountsAsync();

        // 1/entity counts: no duplicates from the repair run.
        Assert.Equal(afterFirstRun, afterRepairRun);

        // Bến Tre repaired back to canonical.
        await _db.Entry(benTre).ReloadAsync();
        Assert.Equal(8, benTre.MaxParticipants);
        Assert.Equal(SurfaceType.Turf, benTre.SurfaceType);
        Assert.Equal(100_000_000m, benTre.PrizePool);
        Assert.DoesNotContain("OWNER-DEMO-SEED", benTre.Description);
        Assert.Equal(TournamentStatus.Published, benTre.Status);
        Assert.Equal(1, benTre.MaxRounds);

        // Sao Mai still has no Bến Tre registration.
        var saoMai = await _db.Horses.FirstAsync(h => h.Name == "Sao Mai");
        Assert.False(await _db.TournamentHorseRegistrations.AnyAsync(r => r.HorseId == saoMai.Id));

        // Ba Tri stays Provisional with clean Notes.
        await _db.Entry(baTriResult).ReloadAsync();
        Assert.Equal(RaceResultStatus.Provisional, baTriResult.Status);
        Assert.DoesNotContain("OWNER-DEMO-SEED", baTriResult.Notes ?? "");
        var baTriEntries = await _db.RaceEntries.Where(e => e.RaceId == baTriRace.Id).ToListAsync();
        var baTriRanking = RaceResultRankingValidator.ParseAndValidate(baTriResult.RankingsJson, baTriResult.WinningHorseId, baTriEntries);
        Assert.Equal(baTriEntries.Count, baTriRanking.Count);

        // Ba Tri complaint semantics unchanged: still terminal Upheld/RaceOperation/AffectsResult=false, clean text.
        var complaint = await _db.RaceComplaints.FirstAsync(c => c.RaceId == baTriRace.Id && c.Type == RaceComplaintType.RaceOperation);
        Assert.Equal(RaceComplaintStatus.Upheld, complaint.Status);
        Assert.False(complaint.AffectsResult);
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.Reason);
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.RefereeResponse ?? "");
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.Ruling ?? "");

        // TP.HCM stays Official with clean Notes, FinishPosition still matches RankingsJson.
        await _db.Entry(tpHcmResult).ReloadAsync();
        Assert.Equal(RaceResultStatus.Official, tpHcmResult.Status);
        Assert.DoesNotContain("OWNER-DEMO-SEED", tpHcmResult.Notes ?? "");
        var tpHcmEntries = await _db.RaceEntries.Where(e => e.RaceId == tpHcmRace.Id).ToListAsync();
        var tpHcmRanking = RaceResultRankingValidator.ParseAndValidate(tpHcmResult.RankingsJson, tpHcmResult.WinningHorseId, tpHcmEntries);
        foreach (var item in tpHcmRanking)
            Assert.Equal(item.Position, tpHcmEntries.Single(e => e.HorseId == item.HorseId).FinishPosition);

        // TP.HCM Prize rows still total exactly 100%.
        var tpHcmTournament = await _db.Tournaments.FirstAsync(t => t.Name == "Giải đấu TP.Hồ Chí Minh");
        var prizes = await _db.Prizes.Where(p => p.TournamentId == tpHcmTournament.Id).ToListAsync();
        Assert.Equal(100m, prizes.Sum(p => p.PercentageOfPool));
    }

    [Fact]
    public async Task SeedOwnerDemoAsync_MissingRequiredOwner_ThrowsAndCreatesNoReplacement()
    {
        // Deliberately do NOT seed any required identity.
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeeder.SeedOwnerDemoAsync(_provider));

        Assert.Equal(0, await _db.Owners.CountAsync());
        Assert.Equal(0, await _db.Tournaments.CountAsync());
    }

    [Fact]
    public async Task SeedOwnerDemoAsync_BenTre_SaoMaiApprovedAndUnregistered()
    {
        await SeedRequiredIdentitiesAsync();
        await DemoSeeder.SeedOwnerDemoAsync(_provider);

        var horse = await _db.Horses.FirstOrDefaultAsync(h => h.Name == "Sao Mai");
        Assert.NotNull(horse);
        Assert.Equal(ApprovalStatus.Approved, horse!.ApprovalStatus);
        Assert.False(horse.IsArchived);

        var tournament = await _db.Tournaments.FirstAsync(t => t.Name == "Giải đấu Bến Tre");
        Assert.Equal(TournamentStatus.Published, tournament.Status);
        Assert.Equal(1, tournament.MaxRounds);
        Assert.Equal(8, tournament.MaxParticipants);
        Assert.NotNull(tournament.SurfaceType);
        Assert.Equal(100_000_000m, tournament.PrizePool);
        Assert.DoesNotContain("OWNER-DEMO-SEED", tournament.Description);
        var round = await _db.Rounds.FirstAsync(r => r.TournamentId == tournament.Id);
        Assert.Equal(round.RoundNumber, tournament.MaxRounds);
        Assert.Equal(0, round.AdvanceCount);
        var race = await _db.Races.FirstAsync(r => r.RoundId == round.Id);
        Assert.Equal(0, race.QualificationSlots);

        Assert.False(await _db.TournamentHorseRegistrations.AnyAsync(r => r.HorseId == horse.Id));
        Assert.False(await _db.RaceEntries.AnyAsync(e => e.HorseId == horse.Id));
        Assert.False(await _db.JockeyInvitations.AnyAsync(i => i.HorseId == horse.Id));
        // No Prize rows unless required — a Published, not-yet-run Tournament has no result to fund.
        Assert.False(await _db.Prizes.AnyAsync(p => p.TournamentId == tournament.Id));

        // jockey2@final.com stays completely uninvolved anywhere in the seed — free for the demo.
        var jockey2User = await _db.Users.FirstAsync(u => u.Email == "jockey2@final.com");
        var jockey2 = await _db.Jockeys.FirstAsync(j => j.UserId == jockey2User.Id);
        Assert.False(await _db.JockeyInvitations.AnyAsync(i => i.JockeyId == jockey2.Id));
        Assert.False(await _db.RaceEntries.AnyAsync(e => e.JockeyId == jockey2.Id));

        // chungua1's pre-existing horses are untouched.
        var existing = await _db.Horses.Where(h => h.OwnerId == horse.OwnerId && h.Name != "Sao Mai" && h.Name != "Hắc Phong" && h.Name != "Thiên Mã").ToListAsync();
        Assert.Equal(new[] { "Alain", "Alex", "Blink", "Cadilac" }.OrderBy(n => n), existing.Select(h => h.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task SeedOwnerDemoAsync_BaTri_FullProvisionalWorkflow()
    {
        var owner = await SeedRequiredIdentitiesAsync();
        await DemoSeeder.SeedOwnerDemoAsync(_provider);

        var tournament = await _db.Tournaments.FirstAsync(t => t.Name == "Giải đấu Ba Tri");
        Assert.Equal(TournamentStatus.Ongoing, tournament.Status);
        Assert.DoesNotContain("OWNER-DEMO-SEED", tournament.Description);

        var horse = await _db.Horses.FirstAsync(h => h.Name == "Hắc Phong");
        var registration = await _db.TournamentHorseRegistrations.FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.HorseId == horse.Id);
        Assert.NotNull(registration);
        Assert.Equal(RegistrationStatus.Approved, registration!.Status);
        Assert.Equal(0, await _db.TournamentHorseRegistrations.CountAsync(r => r.TournamentId == tournament.Id && r.Status == RegistrationStatus.Pending));

        var race = await _db.Races.FirstAsync(r => r.Name == "Cuộc đua Vô địch Ba Tri");
        Assert.Equal(RaceStatus.Finished, race.Status);

        var officialEntry = await _db.RaceEntries.FirstAsync(e => e.RaceId == race.Id && e.HorseId == horse.Id);
        Assert.Equal(RegistrationStatus.Approved, officialEntry.Status);
        Assert.True(officialEntry.OwnerConfirmed);
        Assert.True(officialEntry.JockeyConfirmed);
        Assert.NotNull(officialEntry.JockeyId);
        Assert.NotNull(officialEntry.GateNumber);
        Assert.Null(officialEntry.ScratchedAt);

        var rcJockeyUser = await _db.Users.FirstAsync(u => u.Email == "rc-smoke-jockey@rc-smoke.local");
        var rcJockey = await _db.Jockeys.FirstAsync(j => j.UserId == rcJockeyUser.Id);
        Assert.Equal(rcJockey.Id, officialEntry.JockeyId);
        // The official Hắc Phong jockey never rides another Horse in this same Tournament.
        var otherRaceIdsInTournament = await _db.Races.Where(r => r.TournamentId == tournament.Id).Select(r => r.Id).ToListAsync();
        var otherEntriesForSameJockey = await _db.RaceEntries
            .Where(e => otherRaceIdsInTournament.Contains(e.RaceId) && e.JockeyId == rcJockey.Id && e.HorseId != horse.Id)
            .CountAsync();
        Assert.Equal(0, otherEntriesForSameJockey);

        var entries = await _db.RaceEntries.Where(e => e.RaceId == race.Id).ToListAsync();
        Assert.InRange(entries.Count, 3, 4);
        Assert.All(entries, e => Assert.Null(e.FinishPosition)); // Provisional: no FinishPosition yet.
        var gateNumbers = entries.Select(e => e.GateNumber).ToList();
        Assert.Equal(gateNumbers.Count, gateNumbers.Distinct().Count());

        var result = await _db.RaceResults.FirstAsync(r => r.RaceId == race.Id);
        Assert.Equal(RaceResultStatus.Provisional, result.Status);
        Assert.DoesNotContain("OWNER-DEMO-SEED", result.Notes ?? "");
        var validated = RaceResultRankingValidator.ParseAndValidate(result.RankingsJson, result.WinningHorseId, entries);
        Assert.Equal(entries.Count, validated.Count);
        Assert.Equal(Enumerable.Range(1, entries.Count), validated.Select(v => v.Position).OrderBy(p => p));
        Assert.Equal(entries.Select(e => e.HorseId).OrderBy(id => id), validated.Select(v => v.HorseId).OrderBy(id => id));
        Assert.Contains(validated, v => v.HorseId == horse.Id);
        Assert.Equal(horse.Id, validated.Single(v => v.Position == 1).HorseId);
        Assert.Equal(horse.Id, result.WinningHorseId);

        var primaryAssignment = await _db.RefereeAssignments.FirstAsync(a => a.RaceId == race.Id && a.Role == "Chief Referee");
        Assert.Equal(RefereeAssignmentStatus.Confirmed, primaryAssignment.Status);
        var secondaryAssignment = await _db.RefereeAssignments.FirstAsync(a => a.RaceId == race.Id && a.Role == "Assistant");
        Assert.Equal(RefereeAssignmentStatus.Confirmed, secondaryAssignment.Status);

        var healthCheck = await _db.HorseHealthChecks
            .Where(h => h.HorseId == horse.Id && h.RaceId == race.Id)
            .OrderByDescending(h => h.CheckedAt)
            .FirstAsync();
        Assert.Equal(HealthCheckStatus.Passed, healthCheck.Status);
        Assert.True(healthCheck.ApprovedToRace);

        var complaint = await _db.RaceComplaints.FirstAsync(c => c.RaceId == race.Id && c.Type == RaceComplaintType.RaceOperation);
        Assert.Equal(owner.UserId, complaint.FiledByUserId);
        Assert.Equal(RaceComplaintStatus.Upheld, complaint.Status);
        Assert.False(complaint.AffectsResult);
        Assert.Equal(primaryAssignment.Id, complaint.AssignedRefereeAssignmentId);
        Assert.NotNull(complaint.RefereeResponse);
        Assert.NotNull(complaint.RefereeRespondedAt);
        Assert.NotNull(complaint.RuledByUserId);
        Assert.NotNull(complaint.Ruling);
        Assert.NotNull(complaint.ResolvedAt);
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.Reason);
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.RefereeResponse ?? "");
        Assert.DoesNotContain("OWNER-DEMO-SEED", complaint.Ruling ?? "");

        var primaryRefereeUserId = (await _db.Referees.FirstAsync(r => r.Id == primaryAssignment.RefereeId)).UserId;
        var evidence = await _db.RaceComplaintEvidence.Where(e => e.RaceComplaintId == complaint.Id).ToListAsync();
        Assert.Contains(evidence, e => e.EvidenceSource == EvidenceSource.Filer && e.UploadedByUserId == owner.UserId);
        Assert.Contains(evidence, e => e.EvidenceSource == EvidenceSource.Referee && e.UploadedByUserId == primaryRefereeUserId);

        // No active ResultJudging complaint for this Owner/Race — the Owner must remain free to
        // file a new one live during the demo.
        Assert.False(await _db.RaceComplaints.AnyAsync(c => c.RaceId == race.Id && c.Type == RaceComplaintType.ResultJudging));

        // RaceResult stays Provisional throughout — an Upheld-but-AffectsResult=false complaint
        // must never trigger the RejectedReason correction path.
        Assert.Equal(RaceResultStatus.Provisional, (await _db.RaceResults.FirstAsync(r => r.RaceId == race.Id)).Status);
        Assert.Null((await _db.RaceResults.FirstAsync(r => r.RaceId == race.Id)).RejectedReason);
    }

    [Fact]
    public async Task SeedOwnerDemoAsync_TpHcm_OfficialResultFinishPositionsAndPrizes()
    {
        await SeedRequiredIdentitiesAsync();
        await DemoSeeder.SeedOwnerDemoAsync(_provider);

        var tournament = await _db.Tournaments.FirstAsync(t => t.Name == "Giải đấu TP.Hồ Chí Minh");
        Assert.Equal(TournamentStatus.Finished, tournament.Status);
        Assert.Equal(1, tournament.MaxRounds);
        Assert.DoesNotContain("OWNER-DEMO-SEED", tournament.Description);
        var round = await _db.Rounds.FirstAsync(r => r.TournamentId == tournament.Id);
        Assert.Equal(round.RoundNumber, tournament.MaxRounds);

        var race = await _db.Races.FirstAsync(r => r.Name == "Cuộc đua Vô địch TP.Hồ Chí Minh");
        Assert.Equal(RaceStatus.Finished, race.Status);

        var result = await _db.RaceResults.FirstAsync(r => r.RaceId == race.Id);
        Assert.Equal(RaceResultStatus.Official, result.Status);
        Assert.DoesNotContain("OWNER-DEMO-SEED", result.Notes ?? "");

        var entries = await _db.RaceEntries.Where(e => e.RaceId == race.Id).ToListAsync();
        Assert.True(entries.Count >= 3);
        var validated = RaceResultRankingValidator.ParseAndValidate(result.RankingsJson, result.WinningHorseId, entries);
        foreach (var item in validated)
        {
            var entry = entries.Single(e => e.HorseId == item.HorseId);
            Assert.Equal(item.Position, entry.FinishPosition);
        }

        var thienMa = await _db.Horses.FirstAsync(h => h.Name == "Thiên Mã");
        Assert.Contains(validated, v => v.HorseId == thienMa.Id);

        var prizes = await _db.Prizes.Where(p => p.TournamentId == tournament.Id).OrderBy(p => p.Position).ToListAsync();
        Assert.Equal(3, prizes.Count);
        Assert.Equal(50m, prizes[0].PercentageOfPool);
        Assert.Equal(30m, prizes[1].PercentageOfPool);
        Assert.Equal(20m, prizes[2].PercentageOfPool);
        Assert.Equal(100m, prizes.Sum(p => p.PercentageOfPool));
        Assert.Equal(tournament.PrizePool, prizes.Sum(p => p.Amount));
        foreach (var prize in prizes)
            Assert.Equal(decimal.Round(tournament.PrizePool * prize.PercentageOfPool / 100m, 0, MidpointRounding.AwayFromZero), prize.Amount);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
