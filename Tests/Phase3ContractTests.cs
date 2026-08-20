using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Phase3B: DTO/API contract coverage for the Tournament/Round/Race/Track additions,
/// wired against a real Sqlite in-memory DB and the actual production services (not
/// mocks), reusing RaceLifecycleTests.LifecycleFixture.
/// </summary>
public class Phase3ContractTests
{
    // ── Tournament ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTournament_PersistsAndReturnsPhase3Fields()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Giải Vô Địch",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10),
            PrizePool = 50000m,
            Venue = "Trường đua Phú Thọ",
            Country = "Việt Nam",
            Category = "Grade 1",
            SurfaceType = "turf", // lower-case to also prove case-insensitive parsing
            MinParticipants = 4,
            MaxParticipants = 16,
            MaxRounds = 3,
            RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        });

        Assert.True(create.Result.Success, create.Result.Message);
        var t = create.Result.Data!;
        Assert.Equal(50000m, t.PrizePool);
        Assert.Equal("Trường đua Phú Thọ", t.Venue);
        Assert.Equal("Việt Nam", t.Country);
        Assert.Equal("Grade 1", t.Category);
        Assert.Equal("Turf", t.SurfaceType);
        Assert.Equal(4, t.MinParticipants);
        Assert.Equal(16, t.MaxParticipants);
        Assert.Equal(3, t.MaxRounds);
    }

    [Fact]
    public async Task CreateTournament_OmittedNullableFields_RemainNull_AndMaxRoundsDefaultsToOne()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Giải Tối Giản",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3,
            MaxParticipants = 10,
            RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        });

        Assert.True(create.Result.Success, create.Result.Message);
        var t = create.Result.Data!;
        Assert.Null(t.Venue);
        Assert.Null(t.Country);
        Assert.Null(t.Category);
        Assert.Null(t.SurfaceType);
        Assert.Equal(3, t.MinParticipants);
        Assert.Equal(10, t.MaxParticipants);
        Assert.Equal(0m, t.PrizePool);
        // Must default to 1, never fall through to CLR int default 0.
        Assert.Equal(1, t.MaxRounds);
    }

    [Fact]
    public async Task CreateTournament_InvalidSurfaceType_Returns400_NoSilentCoercion()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Giải Lỗi",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5),
            SurfaceType = "Grass", // not a SurfaceType member
            RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        });

        Assert.False(create.Result.Success);
        Assert.Equal(400, create.StatusCode);
    }

    [Fact]
    public async Task UpdateTournament_OldStyleRequestOmittingNewFields_DoesNotClearExistingValues()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Giải Gốc",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5),
            PrizePool = 100m,
            Venue = "Sân V1",
            Country = "VN",
            Category = "Grade 2",
            SurfaceType = "Dirt",
            MinParticipants = 3,
            MaxParticipants = 10,
            MaxRounds = 5,
            RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        });
        var id = create.Result.Data!.Id;

        // Simulates a pre-Phase3B client: only sets Name, leaving every new field
        // at its C# default (null / not HasValue).
        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest
        {
            Name = "Giải Gốc (đổi tên)"
        });

        Assert.True(update.Result.Success, update.Result.Message);
        var t = update.Result.Data!;
        Assert.Equal("Giải Gốc (đổi tên)", t.Name);
        Assert.Equal(100m, t.PrizePool);
        Assert.Equal("Sân V1", t.Venue);
        Assert.Equal("VN", t.Country);
        Assert.Equal("Grade 2", t.Category);
        Assert.Equal("Dirt", t.SurfaceType);
        Assert.Equal(3, t.MinParticipants);
        Assert.Equal(10, t.MaxParticipants);
        Assert.Equal(5, t.MaxRounds);
    }

    [Fact]
    public async Task UpdateTournament_ExplicitZeroPrizePool_IsApplied_NotTreatedAsOmitted()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        var create = await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "Giải Zero",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5),
            PrizePool = 999m,
            MinParticipants = 3,
            MaxParticipants = 10,
            RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        });
        var id = create.Result.Data!.Id;

        var update = await f.TournamentSvc.UpdateTournamentAsync(id, new UpdateTournamentRequest { PrizePool = 0m });

        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(0m, update.Result.Data!.PrizePool);
    }

    // ── Round ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Round_AdvanceCount_RoundTripsThroughCreateAndUpdate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 10, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;

        var create = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "Round 1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1),
            AdvanceCount = 8
        });
        Assert.True(create.Result.Success, create.Result.Message);
        Assert.Equal(8, create.Result.Data!.AdvanceCount);

        var update = await f.RoundSvc.UpdateRoundAsync(create.Result.Data.Id, new UpdateRoundRequest { AdvanceCount = 5 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(5, update.Result.Data!.AdvanceCount);
    }

    [Fact]
    public async Task Round1_PlannedParticipants_FromTournamentMaxParticipants()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 20, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;

        var round1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });

        Assert.Equal(20, round1.Result.Data!.PlannedParticipants);
    }

    [Fact]
    public async Task Round1_PlannedParticipants_Null_WhenTournamentMaxParticipantsUnset()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();

        // Draft-save now requires MaxParticipants (Phase4B), so a null-MaxParticipants Tournament
        // can no longer be produced through CreateTournamentAsync. Seed directly via the DbContext
        // to arrange this state (e.g. representing legacy data) and prove the read-side derivation
        // still fails safe to null regardless of how the write-side validation evolves.
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = "T",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(5),
            Status = TournamentStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        f.Db.Add(tournament);
        await f.Db.SaveChangesAsync();

        var round1 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournament.Id, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });

        Assert.Null(round1.Result.Data!.PlannedParticipants);
    }

    [Fact]
    public async Task RoundN_PlannedParticipants_FromSinglePredecessorAdvanceCount()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 20, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;

        await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1), AdvanceCount = 8
        });
        var round2 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R2", TournamentId = tournamentId, RoundNumber = 2,
            ScheduledStartDate = DateTime.UtcNow.AddDays(2), ScheduledEndDate = DateTime.UtcNow.AddDays(3)
        });

        Assert.Equal(8, round2.Result.Data!.PlannedParticipants);
    }

    [Fact]
    public async Task RoundN_MissingPredecessor_PlannedParticipantsIsNull()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 20, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;

        // RoundNumber == 3 with no Round 2 ever created for this tournament.
        var round3 = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R3", TournamentId = tournamentId, RoundNumber = 3,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });

        Assert.Null(round3.Result.Data!.PlannedParticipants);
    }

    // RoundN_DuplicatePredecessor_PlannedParticipantsIsNull_NotGuessed removed (Phase5): the
    // scenario it tested — two Rounds sharing a RoundNumber within one Tournament — is no longer
    // constructible at all, not even by seeding directly via the DbContext, now that Phase5 adds a
    // real UNIQUE(TournamentId, RoundNumber) DB index (enforced in tests too, since EnsureCreatedAsync
    // builds schema straight from the model). ComputePlannedParticipantsAsync's Count!=1 defensive
    // fallback still exists in source as defense-in-depth but the "2+ rows" branch of it is now
    // unreachable through any supported path; RoundN_MissingPredecessor_PlannedParticipantsIsNull
    // still covers the reachable "Count==0" branch.

    [Fact]
    public async Task ActualParticipants_SameHorseAcrossTwoRacesInSameRound_CountsOnce()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 10, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });
        var roundId = round.Result.Data!.Id;

        var race1 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race1", TournamentId = tournamentId, RoundId = roundId, ScheduledAt = DateTime.UtcNow.AddHours(1)
        });
        var race2 = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race2", TournamentId = tournamentId, RoundId = roundId, ScheduledAt = DateTime.UtcNow.AddHours(2)
        });

        // Current schema does not yet prevent one Horse from having entries in two
        // Races within the same Round — seed exactly that state.
        var sharedHorseId = Guid.NewGuid();
        f.Db.AddRange(
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race1.Result.Data!.Id, HorseId = sharedHorseId, Status = RegistrationStatus.Approved },
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race2.Result.Data!.Id, HorseId = sharedHorseId, Status = RegistrationStatus.Approved });
        await f.Db.SaveChangesAsync();

        var roundAfter = await f.RoundSvc.GetRoundAsync(roundId);
        Assert.Equal(1, roundAfter.Result.Data!.ActualParticipants);
    }

    [Fact]
    public async Task ActualParticipants_MixedRegistrationStatus_AreNotFiltered()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var tournamentId = (await f.TournamentSvc.CreateTournamentAsync(new CreateTournamentRequest
        {
            Name = "T", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(5),
            MinParticipants = 3, MaxParticipants = 10, RegistrationDeadline = DateTime.UtcNow.AddDays(-1)
        })).Result.Data!.Id;
        var round = await f.RoundSvc.CreateRoundAsync(new CreateRoundRequest
        {
            Name = "R1", TournamentId = tournamentId, RoundNumber = 1,
            ScheduledStartDate = DateTime.UtcNow, ScheduledEndDate = DateTime.UtcNow.AddDays(1)
        });
        var roundId = round.Result.Data!.Id;
        var race = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "Race1", TournamentId = tournamentId, RoundId = roundId, ScheduledAt = DateTime.UtcNow.AddHours(1)
        });

        f.Db.AddRange(
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Result.Data!.Id, HorseId = Guid.NewGuid(), Status = RegistrationStatus.Pending },
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Result.Data.Id, HorseId = Guid.NewGuid(), Status = RegistrationStatus.Approved },
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Result.Data.Id, HorseId = Guid.NewGuid(), Status = RegistrationStatus.Rejected },
            new RaceEntry { Id = Guid.NewGuid(), RaceId = race.Result.Data.Id, HorseId = Guid.NewGuid(), Status = RegistrationStatus.Withdrawn });
        await f.Db.SaveChangesAsync();

        var roundAfter = await f.RoundSvc.GetRoundAsync(roundId);
        // All 4 distinct horses count, regardless of RegistrationStatus.
        Assert.Equal(4, roundAfter.Result.Data!.ActualParticipants);
    }

    // ── Race ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Race_QualificationSlots_RoundTripsThroughCreateAndUpdate()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var seeded = await f.CreateReadyToStartRaceAsync();
        var race = await f.RaceRepo.GetByIdAsync(seeded.Id);
        var tournamentId = race!.TournamentId;
        var roundId = race.RoundId;

        var create = await f.RaceManagement.CreateRaceAsync(new CreateRaceRequest
        {
            Name = "QSlots Race", TournamentId = tournamentId, RoundId = roundId,
            ScheduledAt = DateTime.UtcNow.AddHours(3), QualificationSlots = 4
        });
        Assert.True(create.Result.Success, create.Result.Message);
        Assert.Equal(4, create.Result.Data!.QualificationSlots);

        var update = await f.RaceManagement.UpdateRaceAsync(create.Result.Data.Id, new UpdateRaceRequest { QualificationSlots = 6 });
        Assert.True(update.Result.Success, update.Result.Message);
        Assert.Equal(6, update.Result.Data!.QualificationSlots);
    }

    [Fact]
    public async Task RaceDetailResponse_RoundId_IsPopulated_RoundNumberAndName_ArePopulated()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var seeded = await f.CreateReadyToStartRaceAsync();

        var details = await f.RaceManagement.GetRaceDetailsAsync(seeded.Id);
        Assert.True(details.Result.Success);
        var race = await f.RaceRepo.GetByIdAsync(seeded.Id);

        Assert.Equal(race!.RoundId, details.Result.Data!.RoundId);
        Assert.Equal(1, details.Result.Data.RoundNumber);
        Assert.Equal("Round 1", details.Result.Data.RoundName);
    }

    [Fact]
    public async Task RaceDetailResponse_TrackName_IsPopulated_AfterIncludeFix()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var track = new Track { Id = Guid.NewGuid(), Name = "Đường đua Chính", CreatedAt = DateTime.UtcNow, Capacity = 500 };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();

        var seeded = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.UpdateRaceAsync(seeded.Id, new UpdateRaceRequest { TrackId = track.Id });

        var details = await f.RaceManagement.GetRaceDetailsAsync(seeded.Id);
        Assert.Equal("Đường đua Chính", details.Result.Data!.TrackName);
    }

    [Fact]
    public async Task RaceSummaryDto_IncludesPhase3Additions()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var track = new Track { Id = Guid.NewGuid(), Name = "Track A", CreatedAt = DateTime.UtcNow };
        f.Db.Add(track);
        await f.Db.SaveChangesAsync();

        var seeded = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.UpdateRaceAsync(seeded.Id, new UpdateRaceRequest { TrackId = track.Id, QualificationSlots = 2 });

        var list = await f.RaceSvc.GetRacesAsync();
        var summaries = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<RaceSummaryDto>>(list.Result.Data);
        var summary = summaries.Single(s => s.Id == seeded.Id);

        var race = await f.RaceRepo.GetByIdAsync(seeded.Id);
        Assert.Equal(race!.RoundId, summary.RoundId);
        Assert.Equal(1, summary.RoundNumber);
        Assert.Equal("Round 1", summary.RoundName);
        Assert.Equal(track.Id, summary.TrackId);
        Assert.Equal("Track A", summary.TrackName);
        Assert.Equal(2, summary.QualificationSlots);
        // Preserve pre-existing fields.
        Assert.Equal(seeded.Id, summary.Id);
        Assert.NotEmpty(summary.Name);
        Assert.Equal(race.TournamentId, summary.TournamentId);
    }

    [Fact]
    public async Task Race_ResultStatus_RemainsSeparateFromStatus()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var seeded = await f.CreateReadyToStartRaceAsync();
        await f.RaceManagement.OpenRegistrationAsync(seeded.Id);
        await f.RaceManagement.CloseRegistrationAsync(seeded.Id);
        await f.RaceManagement.StartRaceAsync(seeded.Id);
        await f.RaceManagement.EndRaceAsync(seeded.Id);
        await f.LiveResult.UpdateRaceResultAsync(seeded.Id, RaceLifecycleTests.WinnerLoserRanking(seeded.WinnerHorseId, seeded.LoserHorseId));

        var getResult = await f.RaceSvc.GetRaceAsync(seeded.Id);
        var dto = Assert.IsType<RaceDetailResponse>(getResult.Result.Data);

        Assert.Equal("Finished", dto.Status);
        Assert.Equal("Provisional", dto.ResultStatus);
    }

    [Fact]
    public async Task GetRaceById_ReturnsDto_NotRawEntity()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var seeded = await f.CreateReadyToStartRaceAsync();

        var result = await f.RaceSvc.GetRaceAsync(seeded.Id);

        Assert.True(result.IsSuccess);
        Assert.IsType<RaceDetailResponse>(result.Result.Data);
    }

    [Fact]
    public void RaceDetailResponse_HasNoLegacyApprovalFields()
    {
        // Structural guarantee: the DTO type itself cannot carry ApprovalStatus/IsOfficial,
        // regardless of what data flows through it.
        Assert.Null(typeof(RaceDetailResponse).GetProperty("ApprovalStatus"));
        Assert.Null(typeof(RaceDetailResponse).GetProperty("IsOfficial"));
        Assert.Null(typeof(RaceSummaryDto).GetProperty("ApprovalStatus"));
        Assert.Null(typeof(RaceSummaryDto).GetProperty("IsOfficial"));
    }

    [Fact]
    public void RaceDetailResponse_RoundId_IsNonNullableGuid()
    {
        // Compile-time contract check surfaced as a reflection assertion: RoundId must be
        // System.Guid, not System.Guid? (Nullable<Guid>).
        var prop = typeof(RaceDetailResponse).GetProperty("RoundId")!;
        Assert.Equal(typeof(Guid), prop.PropertyType);
    }

    // ── Track ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TrackList_IncludesCapacity()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        f.Db.Add(new Track { Id = Guid.NewGuid(), Name = "Track Cap", Capacity = 300, Length = 1600, CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        var controller = new TracksController(f.Db);
        var result = await controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetAnonymousProperty<System.Collections.Generic.IEnumerable<TrackResponse>>(ok.Value!, "data");

        var track = data.Single(t => t.Name == "Track Cap");
        Assert.Equal(300, track.Capacity);
        Assert.Equal(1600, track.Length);
    }

    [Fact]
    public async Task TrackCreate_PersistsAndReturnsCapacity()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var controller = new TracksController(f.Db);

        var result = await controller.Create(new CreateTrackRequest { Name = "New Track", Capacity = 800, Length = 2000 });
        var ok = Assert.IsType<OkObjectResult>(result);
        var track = GetAnonymousProperty<TrackResponse>(ok.Value!, "data");

        Assert.Equal(800, track.Capacity);
        Assert.Equal(2000, track.Length);

        var persisted = await f.Db.Tracks.FirstAsync(t => t.Id == track.Id);
        Assert.Equal(800, persisted.Capacity);
    }

    [Fact]
    public void TracksController_HasNoDetailByIdRoute()
    {
        var methods = typeof(TracksController).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, m => m.Name == "GetById" || m.Name == "Get" && m.GetParameters().Any(p => p.Name == "id"));
    }

    private static T GetAnonymousProperty<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name) ?? throw new InvalidOperationException($"Property '{name}' not found on {obj.GetType()}");
        return (T)prop.GetValue(obj)!;
    }
}
