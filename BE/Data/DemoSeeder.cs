using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HorseRacing.Data;

public static class DemoSeeder
{
    // Ưu tiên env var ADMIN_PASSWORD / REFEREE_PASSWORD để tránh mật khẩu mặc định trong production
    private static string AdminPwd =>
        Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";
    private static string RefereePwd =>
        Environment.GetEnvironmentVariable("REFEREE_PASSWORD") ?? "Referee@123";

    /// <summary>
    /// Production: tạo tài khoản admin và trọng tài nếu chưa tồn tại.
    /// </summary>
    public static async Task EnsureAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;
        var changed = false;

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            AddUser(db, hasher, "admin@horseracing.com", AdminPwd, "System Admin", UserRole.Admin, now);
            logger.LogInformation("Admin account created: admin@horseracing.com (password từ env ADMIN_PASSWORD nếu có)");
            changed = true;
        }

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Referee))
        {
            var refereeUser = AddUser(db, hasher, "trongtai@horseracing.com", RefereePwd, "Nguyen Van Trong Tai", UserRole.Referee, now);
            await db.SaveChangesAsync();
            db.Referees.Add(new Referee
            {
                Id = Guid.NewGuid(), UserId = refereeUser.Id,
                LicenseNumber = "REF-ADMIN-001", Certifications = "Race Rules, Track Safety",
                LicenseExpiryDate = now.AddYears(2), IsActive = true,
                Rating = 4.0m, TotalOfficiated = 0, Specialization = "Chief Referee",
                Nationality = "Vietnam", CreatedAt = now
            });
            logger.LogInformation("Referee account created: trongtai@horseracing.com (password từ env REFEREE_PASSWORD nếu có)");
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();

        await SeedHoangTournamentsInternalAsync(db, logger);
    }

    public static async Task SeedHoangTournamentsInternalAsync(ApplicationDbContext db, ILogger logger)
    {
        var now = DateTime.UtcNow;

        var t1Exists = await db.Tournaments.AnyAsync(t => t.Name == "Giải đua ngựa của hoàng");
        var t2Exists = await db.Tournaments.AnyAsync(t => t.Name == "Giải đua ngựa quốc gia của hoàng");

        // 1. Tạo hoặc lấy Track
        var track = await db.Tracks.FirstOrDefaultAsync();
        if (track == null)
        {
            track = new Track
            {
                Id = Guid.NewGuid(),
                Name = "Trường Đua Quốc Gia Hoàng Kim",
                Length = 2400,
                Capacity = 16
            };
            db.Tracks.Add(track);
            await db.SaveChangesAsync();
        }

        // 2. Lấy hoặc tạo danh sách ngựa đã được duyệt (Approved)
        var horses = await db.Horses
            .Where(h => h.ApprovalStatus == ApprovalStatus.Approved && !h.IsArchived)
            .Take(8)
            .ToListAsync();

        if (horses.Count < 2)
        {
            var owner = await db.Owners.FirstOrDefaultAsync();
            if (owner == null)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.HorseOwner);
                if (user == null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = "hoang.owner@horseracing.com",
                        FullName = "Nguyễn Hoàng",
                        Role = UserRole.HorseOwner,
                        IsActive = true,
                        CreatedAt = now
                    };
                    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Owner@123");
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }
                owner = new Owner
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OwnerCode = "OWN-HOANG01",
                    OwnerType = "Cá nhân",
                    JoinDate = now,
                    Status = "Đang hoạt động"
                };
                db.Owners.Add(owner);
                await db.SaveChangesAsync();
            }

            var horseNames = new[] { "Xích Thố Hoàng", "Bạch Long Hoàng", "Phi Long Hoàng", "Thần Phong Hoàng", "Hắc Mã Hoàng", "Hoàng Kim Mã" };
            foreach (var name in horseNames)
            {
                var h = new Horse
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    OwnerId = owner.Id,
                    ApprovalStatus = ApprovalStatus.Approved,
                    Age = 4,
                    Weight = 480,
                    Height = 160,
                    Color = "Vàng Hoàng Kim",
                    Breed = "Thuần Chủng Hoàng Gia"
                };
                db.Horses.Add(h);
            }
            await db.SaveChangesAsync();
            horses = await db.Horses.Where(h => h.ApprovalStatus == ApprovalStatus.Approved && !h.IsArchived).ToListAsync();
        }

        // 3. Giải 1: "Giải đua ngựa của hoàng"
        if (!t1Exists)
        {
            var t1 = new Tournament
            {
                Id = Guid.NewGuid(),
                Name = "Giải đua ngựa của hoàng",
                Description = "Giải đua ngựa Hoàng Kim đặc biệt dành cho các kỵ thủ xuất sắc thi đấu và khán giả tham gia cược.",
                StartDate = now,
                EndDate = now.AddDays(7),
                RegistrationDeadline = now.AddHours(24),
                Status = TournamentStatus.Published,
                PrizePool = 50000000,
                MaxRounds = 1,
                MaxParticipants = 8,
                Venue = "Trường Đua Hoàng Kim",
                Country = "Việt Nam",
                CreatedAt = now,
                PublishedAt = now
            };
            db.Tournaments.Add(t1);
            await db.SaveChangesAsync();

            var r1 = new Round
            {
                Id = Guid.NewGuid(),
                TournamentId = t1.Id,
                RoundNumber = 1,
                Name = "Vòng Chung Kết Hoàng Kim"
            };
            db.Rounds.Add(r1);
            await db.SaveChangesAsync();

            var race1 = new Race
            {
                Id = Guid.NewGuid(),
                TournamentId = t1.Id,
                RoundId = r1.Id,
                Name = "Cuộc Đua Mở Màn - Hoàng Kim Cup",
                ScheduledAt = now.AddDays(7),
                ScheduledEndAt = now.AddDays(7).AddHours(1),
                Status = RaceStatus.RegistrationOpen,
                TrackId = track.Id,
                Location = "Trường Đua Hoàng Kim",
                MaxParticipants = 8,
                Distance = 2000,
                CreatedAt = now
            };
            db.Races.Add(race1);
            await db.SaveChangesAsync();

            int gate = 1;
            var oddsList = new[] { 2.5m, 3.2m, 1.8m, 4.0m, 5.5m, 2.1m, 3.5m, 6.0m };
            int oIdx = 0;
            foreach (var h in horses.Take(8))
            {
                db.RaceEntries.Add(new RaceEntry
                {
                    Id = Guid.NewGuid(),
                    RaceId = race1.Id,
                    HorseId = h.Id,
                    GateNumber = gate++,
                    Status = RegistrationStatus.Approved,
                    OwnerConfirmed = true,
                    Odds = oddsList[oIdx++ % oddsList.Length]
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Tạo thành công Giải 1: 'Giải đua ngựa của hoàng' kèm cuộc đua và ngựa thi đấu.");
        }

        // 4. Giải 2: "Giải đua ngựa quốc gia của hoàng"
        if (!t2Exists)
        {
            var t2 = new Tournament
            {
                Id = Guid.NewGuid(),
                Name = "Giải đua ngựa quốc gia của hoàng",
                Description = "Giải đua ngựa cấp Quốc Gia quy mô đỉnh cao với tổng tiền thưởng lớn, quy tụ các chiến mã hàng đầu.",
                StartDate = now,
                EndDate = now.AddDays(14),
                RegistrationDeadline = now.AddHours(48),
                Status = TournamentStatus.Published,
                PrizePool = 200000000,
                MaxRounds = 2,
                MaxParticipants = 12,
                Venue = "Sân Đua Đỉnh Cao Quốc Gia",
                Country = "Việt Nam",
                CreatedAt = now,
                PublishedAt = now
            };
            db.Tournaments.Add(t2);
            await db.SaveChangesAsync();

            var r2 = new Round
            {
                Id = Guid.NewGuid(),
                TournamentId = t2.Id,
                RoundNumber = 1,
                Name = "Vòng Loại Quốc Gia"
            };
            db.Rounds.Add(r2);
            await db.SaveChangesAsync();

            var race2 = new Race
            {
                Id = Guid.NewGuid(),
                TournamentId = t2.Id,
                RoundId = r2.Id,
                Name = "Trận Siêu Đua Quốc Gia - Vòng 1",
                ScheduledAt = now.AddDays(7),
                ScheduledEndAt = now.AddDays(7).AddHours(2),
                Status = RaceStatus.RegistrationOpen,
                TrackId = track.Id,
                Location = "Sân Đua Đỉnh Cao Quốc Gia",
                MaxParticipants = 8,
                Distance = 2400,
                CreatedAt = now
            };
            db.Races.Add(race2);
            await db.SaveChangesAsync();

            int gate = 1;
            var oddsList = new[] { 2.5m, 3.2m, 1.8m, 4.0m, 5.5m, 2.1m, 3.5m, 6.0m };
            int oIdx = 0;
            foreach (var h in horses.Take(8))
            {
                db.RaceEntries.Add(new RaceEntry
                {
                    Id = Guid.NewGuid(),
                    RaceId = race2.Id,
                    HorseId = h.Id,
                    GateNumber = gate++,
                    Status = RegistrationStatus.Approved,
                    OwnerConfirmed = true,
                    Odds = oddsList[oIdx++ % oddsList.Length]
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Tạo thành công Giải 2: 'Giải đua ngựa quốc gia của hoàng' kèm cuộc đua và ngựa thi đấu.");
        }

        // 5. Giải 3: "Giải xuyên lục địa"
        var t3 = await db.Tournaments.FirstOrDefaultAsync(t => t.Name == "Giải xuyên lục địa");
        if (t3 == null)
        {
            t3 = new Tournament
            {
                Id = Guid.NewGuid(),
                Name = "Giải xuyên lục địa",
                Description = "Giải đua ngựa Xuyên Lục Địa Đỉnh Cao quy tụ các kỵ thủ và chiến mã đẳng cấp quốc tế thi đấu tranh cúp vô địch.",
                StartDate = now,
                EndDate = now.AddDays(20),
                RegistrationDeadline = now.AddHours(72),
                Status = TournamentStatus.Published,
                PrizePool = 500000000,
                MaxRounds = 3,
                MaxParticipants = 16,
                Venue = "Đấu Trường Siêu Đua Xuyên Lục Địa",
                Country = "Quốc Tế",
                CreatedAt = now,
                PublishedAt = now
            };
            db.Tournaments.Add(t3);
            await db.SaveChangesAsync();

            var r3 = new Round
            {
                Id = Guid.NewGuid(),
                TournamentId = t3.Id,
                RoundNumber = 1,
                Name = "Vòng Loại Xuyên Lục Địa"
            };
            db.Rounds.Add(r3);
            await db.SaveChangesAsync();

            var race3 = new Race
            {
                Id = Guid.NewGuid(),
                TournamentId = t3.Id,
                RoundId = r3.Id,
                Name = "Trận Đại Đua Xuyên Lục Địa - Vòng 1",
                ScheduledAt = now.AddDays(7),
                ScheduledEndAt = now.AddDays(7).AddHours(4),
                Status = RaceStatus.RegistrationOpen,
                TrackId = track.Id,
                Location = "Đấu Trường Siêu Đua Xuyên Lục Địa",
                MaxParticipants = 8,
                Distance = 3000,
                CreatedAt = now
            };
            db.Races.Add(race3);
            await db.SaveChangesAsync();

            int gate = 1;
            var oddsList = new[] { 2.5m, 3.2m, 1.8m, 4.0m, 5.5m, 2.1m, 3.5m, 6.0m };
            int oIdx = 0;
            foreach (var h in horses.Take(8))
            {
                db.RaceEntries.Add(new RaceEntry
                {
                    Id = Guid.NewGuid(),
                    RaceId = race3.Id,
                    HorseId = h.Id,
                    GateNumber = gate++,
                    Status = RegistrationStatus.Approved,
                    OwnerConfirmed = true,
                    Odds = oddsList[oIdx++ % oddsList.Length]
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Tạo thành công Giải 3: 'Giải xuyên lục địa' kèm cuộc đua và ngựa thi đấu.");
        }

        // Always refresh all 3 target tournaments and their races to ensure ScheduledAt is in 7 days, Odds assigned, and Jockeys assigned!
        var jockeys = await db.Jockeys
            .Where(j => j.ApprovalStatus == ApprovalStatus.Approved)
            .Take(8)
            .ToListAsync();

        if (jockeys.Count < 8)
        {
            var jockeyNames = new[] { "Nguyễn Văn Hùng", "Trần Quốc Tuấn", "Lê Hoàng Nam", "Phạm Đức Anh", "Vũ Minh Trí", "Đặng Quang Huy", "Hoàng Kim Sang", "Bùi Thành Long" };
            int jIdx = 1;
            foreach (var jName in jockeyNames)
            {
                var email = $"jockey.hoang{jIdx}@horseracing.com";
                var jUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (jUser == null)
                {
                    jUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        FullName = jName,
                        Role = UserRole.Jockey,
                        IsActive = true,
                        CreatedAt = now
                    };
                    jUser.PasswordHash = new PasswordHasher<User>().HashPassword(jUser, "Jockey@123");
                    db.Users.Add(jUser);
                    await db.SaveChangesAsync();
                }

                var existingJockey = await db.Jockeys.FirstOrDefaultAsync(j => j.UserId == jUser.Id);
                if (existingJockey == null)
                {
                    db.Jockeys.Add(new Jockey
                    {
                        Id = Guid.NewGuid(),
                        UserId = jUser.Id,
                        LicenseNumber = $"JKY-HOANG-00{jIdx}",
                        Nationality = "Việt Nam",
                        Gender = "Male",
                        DateOfBirth = new DateTime(1998, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        Height = 1.65m,
                        Weight = 52m,
                        ExperienceYears = 6,
                        TotalRaces = 150,
                        TotalWins = 35,
                        WinRate = 23.33m,
                        Rank = jIdx,
                        Status = "Đang hoạt động",
                        ApprovalStatus = ApprovalStatus.Approved,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                jIdx++;
            }
            await db.SaveChangesAsync();
            jockeys = await db.Jockeys.Where(j => j.ApprovalStatus == ApprovalStatus.Approved).Take(8).ToListAsync();
        }

        var targetNames = new[] { "Giải đua ngựa của hoàng", "Giải đua ngựa quốc gia của hoàng", "Giải xuyên lục địa" };
        var targetTournaments = await db.Tournaments
            .Include(t => t.Races)
                .ThenInclude(r => r.Entries)
            .Where(t => targetNames.Contains(t.Name))
            .ToListAsync();

        var tracks = await db.Tracks.ToListAsync();
        foreach (var trk in tracks)
        {
            if (trk.Capacity == null || trk.Capacity <= 0)
            {
                trk.Capacity = 12;
            }
        }
        await db.SaveChangesAsync();

        var referee = await db.Referees.FirstOrDefaultAsync();
        if (referee == null)
        {
            var refUser = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Referee);
            if (refUser == null)
            {
                refUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = "trongtai.hoang@horseracing.com",
                    FullName = "Nguyễn Văn Trọng Tài",
                    Role = UserRole.Referee,
                    IsActive = true,
                    CreatedAt = now
                };
                refUser.PasswordHash = new PasswordHasher<User>().HashPassword(refUser, "Ref@123");
                db.Users.Add(refUser);
                await db.SaveChangesAsync();
            }
            referee = new Referee
            {
                Id = Guid.NewGuid(),
                UserId = refUser.Id,
                LicenseNumber = "REF-HOANG-001",
                Certifications = "International Race Rules, Safety Standards",
                LicenseExpiryDate = now.AddYears(3),
                IsActive = true,
                Rating = 4.8m,
                TotalOfficiated = 120,
                Specialization = "Chief Referee",
                Nationality = "Việt Nam",
                CreatedAt = now
            };
            db.Referees.Add(referee);
            await db.SaveChangesAsync();
        }

        var refreshOdds = new[] { 2.5m, 3.2m, 1.8m, 4.0m, 5.5m, 2.1m, 3.5m, 6.0m };
        int tOffset = 7;
        foreach (var tt in targetTournaments)
        {
            tt.Status = TournamentStatus.Ongoing; // Set to Ongoing so races can be started!
            tt.StartDate = now;

            foreach (var race in tt.Races)
            {
                race.Status = RaceStatus.RegistrationOpen;
                race.ScheduledAt = now.AddDays(tOffset); // Separate days (7, 9, 11) -> NO schedule conflict!
                race.ScheduledEndAt = now.AddDays(tOffset).AddHours(2);

                // Assign Confirmed Referee to race so Admin can click "Bắt đầu"
                var hasRefAssignment = await db.RefereeAssignments
                    .AnyAsync(ra => ra.RaceId == race.Id && ra.Status == RefereeAssignmentStatus.Confirmed);

                if (!hasRefAssignment)
                {
                    db.RefereeAssignments.Add(new RefereeAssignment
                    {
                        Id = Guid.NewGuid(),
                        RaceId = race.Id,
                        RefereeId = referee.Id,
                        Role = "Chief Referee",
                        Status = RefereeAssignmentStatus.Confirmed,
                        AssignedAt = now,
                        ConfirmedAt = now
                    });
                    await db.SaveChangesAsync();
                }

                int idx = 0;
                foreach (var entry in race.Entries)
                {
                    entry.Status = RegistrationStatus.Approved;
                    entry.OwnerConfirmed = true;
                    entry.JockeyConfirmed = true;

                    if (jockeys.Count > 0)
                    {
                        entry.JockeyId = jockeys[idx % jockeys.Count].Id;
                    }

                    if (entry.Odds <= 0)
                    {
                        entry.Odds = refreshOdds[idx % refreshOdds.Length];
                    }

                    var hasHealthCheck = await db.HorseHealthChecks
                        .AnyAsync(hc => hc.HorseId == entry.HorseId && hc.RaceId == race.Id && hc.Status == HealthCheckStatus.Passed && hc.ApprovedToRace);

                    if (!hasHealthCheck)
                    {
                        db.HorseHealthChecks.Add(new HorseHealthCheck
                        {
                            Id = Guid.NewGuid(),
                            HorseId = entry.HorseId,
                            RaceId = race.Id,
                            RefereeId = referee.Id,
                            Status = HealthCheckStatus.Passed,
                            CheckedAt = now,
                            ApprovedToRace = true,
                            Verdict = "Đạt yêu cầu sức khỏe xuất phát",
                            Observations = "Sức khỏe nhịp tim bình thường, đủ điều kiện thi đấu."
                        });
                    }

                    var horse = await db.Horses.FirstOrDefaultAsync(h => h.Id == entry.HorseId);
                    if (horse != null)
                    {
                        var hasOwnerReg = await db.TournamentHorseRegistrations
                            .AnyAsync(r => r.TournamentId == tt.Id && r.OwnerId == horse.OwnerId);

                        if (!hasOwnerReg)
                        {
                            db.TournamentHorseRegistrations.Add(new TournamentHorseRegistration
                            {
                                Id = Guid.NewGuid(),
                                TournamentId = tt.Id,
                                HorseId = horse.Id,
                                OwnerId = horse.OwnerId,
                                Status = RegistrationStatus.Approved,
                                CreatedAt = now,
                                ApprovedAt = now
                            });
                            await db.SaveChangesAsync();
                        }
                    }

                    idx++;
                }
            }
            tOffset += 2;
        }
        await db.SaveChangesAsync();
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding demo data...");
        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;

        // Helper to create UTC DateTimes
        DateTime Utc(int year, int month, int day) => new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

        // ── USERS ──
        var admin = AddUser(db, hasher, "admin@horseracing.com", "Admin@123", "System Admin", UserRole.Admin, now);

        var owner1 = AddUser(db, hasher, "john.stable@email.com", "Owner@123", "John Whitfield", UserRole.HorseOwner, now);
        var owner2 = AddUser(db, hasher, "sarah.ranch@email.com", "Owner@123", "Sarah O'Brien", UserRole.HorseOwner, now);

        var jockey1 = AddUser(db, hasher, "marcus.rider@email.com", "Jockey@123", "Marcus Chen", UserRole.Jockey, now);
        var jockey2 = AddUser(db, hasher, "elena.race@email.com", "Jockey@123", "Elena Rodriguez", UserRole.Jockey, now);

        var referee1 = AddUser(db, hasher, "chief.ref@email.com", "Ref@123", "Robert Thompson", UserRole.Referee, now);
        var referee2 = AddUser(db, hasher, "asst.ref@email.com", "Ref@123", "Maria Santos", UserRole.Referee, now);

        AddUser(db, hasher, "fan.one@email.com", "Fan@123", "Alex Johnson", UserRole.Spectator, now);
        AddUser(db, hasher, "fan.two@email.com", "Fan@123", "Jamie Williams", UserRole.Spectator, now);

        await db.SaveChangesAsync();

        // ── OWNERS ──
        var ownerProfile1 = new Owner { Id = Guid.NewGuid(), UserId = owner1.Id, OwnerCode = "OWN-A1B2C3D4", OwnerType = "Cá nhân", OrganizationName = "Whitfield Stables", JoinDate = now.AddMonths(-12), Status = "Đang hoạt động", CreatedAt = now, UpdatedAt = now };
        var ownerProfile2 = new Owner { Id = Guid.NewGuid(), UserId = owner2.Id, OwnerCode = "OWN-E5F6G7H8", OwnerType = "Organization", OrganizationName = "O'Brien Racing Ltd", BusinessLicenseNumber = "BL-2024-001", JoinDate = now.AddMonths(-8), Status = "Đang hoạt động", CreatedAt = now, UpdatedAt = now };
        var jockeyOwnerProfile1 = new Owner { Id = Guid.NewGuid(), UserId = jockey1.Id, OwnerCode = "OWN-JKY00001", OwnerType = "Cá nhân", JoinDate = now, Status = "Đang hoạt động", CreatedAt = now, UpdatedAt = now };
        var jockeyOwnerProfile2 = new Owner { Id = Guid.NewGuid(), UserId = jockey2.Id, OwnerCode = "OWN-JKY00002", OwnerType = "Cá nhân", JoinDate = now, Status = "Đang hoạt động", CreatedAt = now, UpdatedAt = now };
        db.Owners.AddRange(ownerProfile1, ownerProfile2, jockeyOwnerProfile1, jockeyOwnerProfile2);
        await db.SaveChangesAsync();

        // ── HORSES ──
        var horses = new List<Horse>
        {
            new() { Id = Guid.NewGuid(), Name = "Silver Comet", OwnerId = ownerProfile1.Id, Breed = "Thoroughbred", Gender = "Stallion", DateOfBirth = Utc(2020, 3, 15), Age = 5, Weight = 520m, Height = 1.65m, Color = "Gray", TotalRaces = 12, TotalWins = 5, ApprovalStatus = ApprovalStatus.Approved, ImageUrl = "/assets/horse1.png" },
            new() { Id = Guid.NewGuid(), Name = "Thunder Strike", OwnerId = ownerProfile1.Id, Breed = "Arabian", Gender = "Mare", DateOfBirth = Utc(2019, 7, 8), Age = 6, Weight = 480m, Height = 1.58m, Color = "Bay", TotalRaces = 18, TotalWins = 7, ApprovalStatus = ApprovalStatus.Approved, ImageUrl = "/assets/horse2.png" },
            new() { Id = Guid.NewGuid(), Name = "Midnight Runner", OwnerId = ownerProfile1.Id, Breed = "Quarter Horse", Gender = "Gelding", DateOfBirth = Utc(2021, 1, 20), Age = 4, Weight = 540m, Height = 1.62m, Color = "Black", TotalRaces = 6, TotalWins = 2, ApprovalStatus = ApprovalStatus.Approved },
            new() { Id = Guid.NewGuid(), Name = "Golden Arrow", OwnerId = ownerProfile2.Id, Breed = "Thoroughbred", Gender = "Stallion", DateOfBirth = Utc(2020, 5, 30), Age = 5, Weight = 500m, Height = 1.67m, Color = "Chestnut", TotalRaces = 9, TotalWins = 4, ApprovalStatus = ApprovalStatus.Approved },
            new() { Id = Guid.NewGuid(), Name = "Storm Chaser", OwnerId = ownerProfile2.Id, Breed = "Thoroughbred", Gender = "Mare", DateOfBirth = Utc(2018, 11, 12), Age = 7, Weight = 510m, Height = 1.60m, Color = "Dark Bay", TotalRaces = 22, TotalWins = 10, ApprovalStatus = ApprovalStatus.Approved },
            new() { Id = Guid.NewGuid(), Name = "Desert Wind", OwnerId = ownerProfile2.Id, Breed = "Arabian", Gender = "Gelding", DateOfBirth = Utc(2021, 8, 3), Age = 3, Weight = 460m, Height = 1.55m, Color = "Palomino", TotalRaces = 3, TotalWins = 1, ApprovalStatus = ApprovalStatus.Approved },
        };
        db.Horses.AddRange(horses);
        await db.SaveChangesAsync();

        // ── JOCKEYS ──
        var jockeyProfile1 = new Jockey { Id = Guid.NewGuid(), UserId = jockey1.Id, LicenseNumber = "JKY-001-2024", Nationality = "USA", Gender = "Male", DateOfBirth = Utc(1995, 4, 12), Height = 1.70m, Weight = 54m, ExperienceYears = 8, TotalRaces = 340, TotalWins = 82, WinRate = 24.12m, Rank = 5, Status = "Đang hoạt động", ApprovalStatus = ApprovalStatus.Approved, CreatedAt = now, UpdatedAt = now };
        var jockeyProfile2 = new Jockey { Id = Guid.NewGuid(), UserId = jockey2.Id, LicenseNumber = "JKY-002-2024", Nationality = "UK", Gender = "Female", DateOfBirth = Utc(1998, 9, 25), Height = 1.62m, Weight = 50m, ExperienceYears = 5, TotalRaces = 210, TotalWins = 48, WinRate = 22.86m, Rank = 8, Status = "Đang hoạt động", ApprovalStatus = ApprovalStatus.Approved, CreatedAt = now, UpdatedAt = now };
        db.Jockeys.AddRange(jockeyProfile1, jockeyProfile2);
        await db.SaveChangesAsync();

        // ── REFEREES ──
        var refe1 = new Referee { Id = Guid.NewGuid(), UserId = referee1.Id, LicenseNumber = "REF-001-2024", Certifications = "Veterinary Medicine, Track Safety", LicenseExpiryDate = now.AddYears(2), IsActive = true, Rating = 4.5m, TotalOfficiated = 85, Specialization = "Chief Referee", Nationality = "USA", CreatedAt = now };
        var refe2 = new Referee { Id = Guid.NewGuid(), UserId = referee2.Id, LicenseNumber = "REF-002-2024", Certifications = "Animal Welfare, Race Rules", LicenseExpiryDate = now.AddYears(1), IsActive = true, Rating = 4.2m, TotalOfficiated = 52, Specialization = "Assistant, Veterinary", Nationality = "Brazil", CreatedAt = now };
        db.Referees.AddRange(refe1, refe2);
        await db.SaveChangesAsync();

        // ── TOURNAMENT ──
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(), Name = "Spring Championship 2026", StartDate = now.AddDays(-5), EndDate = now.AddDays(10),
            Description = "The premier spring racing championship featuring top thoroughbreds and jockeys from around the world.",
            Category = "Grade 1", Venue = "Churchill Downs", Country = "USA", SurfaceType = SurfaceType.Dirt,
            MaxRounds = 3, PrizePool = 250000m, IsActive = true, CreatedAt = now
        };
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        // ── ROUNDS ──
        var round1 = new Round { Id = Guid.NewGuid(), Name = "Qualifying Heats", TournamentId = tournament.Id, RoundNumber = 1, ScheduledStartDate = now.AddDays(-3), ScheduledEndDate = now.AddDays(2), Description = "Opening qualifying rounds" };
        var round2 = new Round { Id = Guid.NewGuid(), Name = "Semi Finals", TournamentId = tournament.Id, RoundNumber = 2, ScheduledStartDate = now.AddDays(4), ScheduledEndDate = now.AddDays(7), Description = "Top 12 advance to semi-finals" };
        db.Rounds.AddRange(round1, round2);
        await db.SaveChangesAsync();

        // ── RACES ──
        var race1 = new Race { Id = Guid.NewGuid(), Name = "Opening Sprint", TournamentId = tournament.Id, RoundId = round1.Id, ScheduledAt = now.AddDays(-2), ActualStartTime = now.AddDays(-2).AddHours(14), ActualEndTime = now.AddDays(-2).AddHours(14).AddMinutes(3), Status = RaceStatus.Finished, Location = "Main Track", Description = "1200m sprint qualifying", MaxParticipants = 8, Distance = 1200, CreatedAt = now };
        var race2 = new Race { Id = Guid.NewGuid(), Name = "Mid-Distance Classic", TournamentId = tournament.Id, RoundId = round1.Id, ScheduledAt = now.AddDays(1), Status = RaceStatus.Scheduled, Location = "Main Track", Description = "2000m mid-distance race", MaxParticipants = 8, Distance = 2000, CreatedAt = now };
        var race3 = new Race { Id = Guid.NewGuid(), Name = "Endurance Challenge", TournamentId = tournament.Id, RoundId = round2.Id, ScheduledAt = now.AddDays(5), Status = RaceStatus.Scheduled, Location = "Outer Track", Description = "3200m endurance test", MaxParticipants = 6, Distance = 3200, CreatedAt = now };
        db.Races.AddRange(race1, race2, race3);
        await db.SaveChangesAsync();

        // ── RACE ENTRIES ──
        var entry1 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race1.Id, HorseId = horses[0].Id, JockeyId = jockeyProfile1.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 3, FinishPosition = 1, FinishTime = 71.24m, WeightCarried = 56m };
        var entry2 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race1.Id, HorseId = horses[3].Id, JockeyId = jockeyProfile2.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 1, FinishPosition = 2, FinishTime = 71.89m, WeightCarried = 55m };
        var entry3 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race1.Id, HorseId = horses[1].Id, JockeyId = jockeyProfile1.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 5, FinishPosition = 3, FinishTime = 72.45m, WeightCarried = 57m, Equipment = "Blinkers" };
        var entry4 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race2.Id, HorseId = horses[0].Id, JockeyId = jockeyProfile1.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = false, GateNumber = 2 };
        var entry5 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race2.Id, HorseId = horses[3].Id, JockeyId = jockeyProfile2.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = false, JockeyConfirmed = true, GateNumber = 4 };
        var entry6 = new RaceEntry { Id = Guid.NewGuid(), RaceId = race2.Id, HorseId = horses[4].Id, JockeyId = jockeyProfile1.Id, Status = RegistrationStatus.Approved, OwnerConfirmed = true, JockeyConfirmed = true, GateNumber = 1, WeightCarried = 58m };
        db.RaceEntries.AddRange(entry1, entry2, entry3, entry4, entry5, entry6);
        await db.SaveChangesAsync();

        // ── RACE RESULT ──
        var result1 = new RaceResult { Id = Guid.NewGuid(), RaceId = race1.Id, WinningHorseId = horses[0].Id, TotalParticipants = 3, WinnerFinishTime = 71.24m, RecordedAt = now.AddDays(-2).AddHours(15), PublishedAt = now.AddDays(-2).AddHours(16), ApprovedAt = now.AddDays(-2).AddHours(16), Status = RaceResultStatus.Official, WinnerPurse = 15000m, RankingsJson = "[{\"position\":1,\"horseName\":\"Silver Comet\",\"jockeyName\":\"Marcus Chen\",\"time\":71.24,\"margin\":\"-\"},{\"position\":2,\"horseName\":\"Golden Arrow\",\"jockeyName\":\"Elena Rodriguez\",\"time\":71.89,\"margin\":\"0.65s\"},{\"position\":3,\"horseName\":\"Thunder Strike\",\"jockeyName\":\"Marcus Chen\",\"time\":72.45,\"margin\":\"1.21s\"}]", Notes = "Clean race, no incidents" };
        db.RaceResults.Add(result1);
        await db.SaveChangesAsync();

        // ── REFEREE ASSIGNMENTS ──
        db.RefereeAssignments.AddRange(
            new() { Id = Guid.NewGuid(), RaceId = race1.Id, RefereeId = refe1.Id, Role = "Chief Referee", Status = RefereeAssignmentStatus.Completed, AssignedAt = now.AddDays(-4), ConfirmedAt = now.AddDays(-4).AddHours(2), CompletedAt = now.AddDays(-2).AddHours(16) },
            new() { Id = Guid.NewGuid(), RaceId = race2.Id, RefereeId = refe1.Id, Role = "Chief Referee", Status = RefereeAssignmentStatus.Confirmed, AssignedAt = now.AddDays(-1), ConfirmedAt = now },
            new() { Id = Guid.NewGuid(), RaceId = race2.Id, RefereeId = refe2.Id, Role = "Assistant", Status = RefereeAssignmentStatus.Assigned, AssignedAt = now.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // ── HEALTH CHECKS ──
        db.HorseHealthChecks.Add(new HorseHealthCheck { Id = Guid.NewGuid(), HorseId = horses[0].Id, RaceId = race1.Id, RefereeId = refe1.Id, Status = HealthCheckStatus.Passed, CheckedAt = now.AddDays(-2).AddHours(12), Observations = "Fit and healthy", ApprovedToRace = true, Verdict = "Cleared to race" });
        await db.SaveChangesAsync();

        // ── PRIZES ──
        // PRIZE-V1.2: Amount is now DERIVED from PercentageOfPool * PrizePool / 100 — these seed
        // values are hand-computed to already be internally consistent (60/25/15 = 100% of
        // 250,000 = 150,000/62,500/37,500 exactly), matching what PrizeAmountCalculator would
        // itself produce, so no seed-time recalculation call is needed. Currency is VND to match
        // the app's monetary convention. The old orphan RaceId-only/TournamentId-null row was
        // removed — Prize is tournament-final-ranking allocation only in V1, not race-scoped.
        db.Prizes.AddRange(
            new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, Name = "1st Place - Spring Championship", Amount = 150000m, Currency = "VND", Position = 1, PercentageOfPool = 60, SponsorName = "RaceMaster Inc.", CreatedAt = now },
            new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, Name = "2nd Place - Spring Championship", Amount = 62500m, Currency = "VND", Position = 2, PercentageOfPool = 25, CreatedAt = now },
            new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, Name = "3rd Place - Spring Championship", Amount = 37500m, Currency = "VND", Position = 3, PercentageOfPool = 15, CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // ── CONTRACT ──
        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(), OwnerId = ownerProfile1.Id, JockeyId = jockeyProfile1.Id, HorseId = horses[0].Id,
            Title = "2026 Season Riding Contract", Status = ContractStatus.Active,
            StartDate = now.AddMonths(-1), EndDate = now.AddMonths(11),
            BaseFee = 25000m, WinBonusPercent = 8, PerRaceFee = 1500m,
            TermsAndConditions = "Exclusive riding rights for Silver Comet during the 2026 racing season. Bonus applicable on 1st place finishes.",
            SignedByOwnerAt = now.AddMonths(-1), SignedByJockeyAt = now.AddMonths(-1),
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // ── PROTEST (example) ──
        db.Protests.Add(new Protest
        {
            Id = Guid.NewGuid(), RaceId = race1.Id, FiledByUserId = owner2.Id, AgainstEntryId = entry1.Id,
            Reason = "Alleged interference at the final turn. Golden Arrow was forced wide by Silver Comet, losing momentum.",
            Evidence = "Video timestamp 1:42-1:48 shows lateral movement into lane 3.",
            Status = ProtestStatus.Pending, FiledAt = now.AddDays(-2).AddHours(18)
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Demo data seeded successfully! 9 users, 6 horses, 3 races, 1 tournament.");
    }

    public static async Task SeedExtraAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;
        var changed = false;

        // ── 2 tài khoản trọng tài ──
        var referees = new[]
        {
            (Email: "trongtai2@horseracing.com", Name: "Tran Van Trong Tai", License: "REF-ADMIN-002"),
            (Email: "trongtai3@horseracing.com", Name: "Le Thi Trong Tai", License: "REF-ADMIN-003"),
        };
        foreach (var r in referees)
        {
            if (await db.Users.AnyAsync(u => u.Email == r.Email)) continue;
            var user = AddUser(db, hasher, r.Email, "Referee@123", r.Name, UserRole.Referee, now);
            await db.SaveChangesAsync();
            db.Referees.Add(new Referee
            {
                Id = Guid.NewGuid(), UserId = user.Id,
                LicenseNumber = r.License, Certifications = "Race Rules, Track Safety",
                LicenseExpiryDate = now.AddYears(2), IsActive = true,
                Rating = 4.0m, TotalOfficiated = 0, Specialization = "Chief Referee",
                Nationality = "Vietnam", CreatedAt = now
            });
            logger.LogInformation("Referee seeded: {Email} (password: Referee@123)", r.Email);
            changed = true;
        }

        // ── Chủ ngựa cho 3 ngựa demo ──
        Owner? ownerProfile = null;
        if (!await db.Users.AnyAsync(u => u.Email == "chusohuu@horseracing.com"))
        {
            var ownerUser = AddUser(db, hasher, "chusohuu@horseracing.com", "Owner@123", "Chu So Huu Demo", UserRole.HorseOwner, now);
            await db.SaveChangesAsync();
            ownerProfile = new Owner
            {
                Id = Guid.NewGuid(), UserId = ownerUser.Id, OwnerCode = "OWN-DEMO-001",
                OwnerType = "Cá nhân", JoinDate = now, Status = "Đang hoạt động",
                CreatedAt = now, UpdatedAt = now
            };
            db.Owners.Add(ownerProfile);
            logger.LogInformation("Horse owner seeded: chusohuu@horseracing.com (password: Owner@123)");
            changed = true;
        }
        else
        {
            var ownerUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "chusohuu@horseracing.com");
            ownerProfile = await db.Owners.FirstOrDefaultAsync(o => o.UserId == ownerUser!.Id);
        }

        // ── 3 kỵ sĩ (đã được phê duyệt) ──
        var jockeyEmails = new[] { "jockey1@horseracing.com", "jockey2@horseracing.com", "jockey3@horseracing.com" };
        var jockeys = new List<Jockey>();
        foreach (var email in jockeyEmails)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = AddUser(db, hasher, email, "Jockey@123", email.StartsWith("jockey1") ? "Ky Si Mot" : email.StartsWith("jockey2") ? "Ky Si Hai" : "Ky Si Ba", UserRole.Jockey, now);
                await db.SaveChangesAsync();
                var jockey = new Jockey
                {
                    Id = Guid.NewGuid(), UserId = user.Id,
                    LicenseNumber = "JKY-DEMO-" + (jockeys.Count + 1).ToString("00"),
                    ApprovalStatus = ApprovalStatus.Approved, Status = "Đang hoạt động",
                    Nationality = "Vietnam", ExperienceYears = 3,
                    CreatedAt = now, UpdatedAt = now
                };
                db.Jockeys.Add(jockey);
                jockeys.Add(jockey);
                logger.LogInformation("Jockey seeded: {Email} (password: Jockey@123)", email);
                changed = true;
            }
            else
            {
                var existing = await db.Jockeys.FirstOrDefaultAsync(j => j.UserId == user.Id);
                if (existing == null)
                {
                    existing = new Jockey
                    {
                        Id = Guid.NewGuid(), UserId = user.Id,
                        LicenseNumber = "JKY-DEMO-" + (jockeys.Count + 1).ToString("00"),
                        ApprovalStatus = ApprovalStatus.Approved, Status = "Đang hoạt động",
                        Nationality = "Vietnam", ExperienceYears = 3,
                        CreatedAt = now, UpdatedAt = now
                    };
                    db.Jockeys.Add(existing);
                    jockeys.Add(existing);
                    changed = true;
                }
                else
                {
                    jockeys.Add(existing);
                }
            }
        }

        // ── 3 con ngựa đã có kỵ sĩ (lời mời Accepted) ──
        var horseSeeds = new[]
        {
            (Name: "Hoa Toc", JockeyIdx: 0, Breed: "Thoroughbred", Color: "Xám"),
            (Name: "Kim Long", JockeyIdx: 1, Breed: "Arabian", Color: "Nâu"),
            (Name: "Bach Ma", JockeyIdx: 2, Breed: "Quarter Horse", Color: "Trắng"),
        };
        if (ownerProfile != null)
        {
            foreach (var (horseName, jockeyIdx, breed, color) in horseSeeds)
            {
                if (await db.Horses.AnyAsync(h => h.Name == horseName)) continue;
                var horse = new Horse
                {
                    Id = Guid.NewGuid(), Name = horseName, OwnerId = ownerProfile.Id,
                    Breed = breed, Gender = "Stallion", Age = 4,
                    Weight = 500m, Height = 1.60m, Color = color,
                    TotalRaces = 0, TotalWins = 0,
                    ApprovalStatus = ApprovalStatus.Approved
                };
                db.Horses.Add(horse);
                await db.SaveChangesAsync();

                var jockey = jockeys[jockeyIdx];
                db.JockeyInvitations.Add(new JockeyInvitation
                {
                    Id = Guid.NewGuid(), HorseId = horse.Id, JockeyId = jockey.Id,
                    Status = JockeyInvitationStatus.Accepted,
                    CreatedAt = now, RespondedAt = now,
                    Message = "Lời mời demo (đã được kỵ sĩ chấp nhận)"
                });
                logger.LogInformation("Horse seeded: {Name} (kỵ sĩ: {Jockey})", horseName, jockey.User?.FullName);
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    private static User AddUser(ApplicationDbContext db, PasswordHasher<User> hasher, string email, string password, string fullName, UserRole role, DateTime now)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, FullName = fullName, Role = role,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        return user;
    }
}
