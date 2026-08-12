using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class JockeyInvitationRepositoryTests
{
    [Fact]
    public async Task GetByJockeyAsync_ReturnsPendingAndAcceptedInvitations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

        var horseId = Guid.NewGuid();
        var jockeyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = ownerUserId,
            Email = "owner@example.com",
            PasswordHash = "test",
            FullName = "Nguyễn Văn Chủ",
            Role = UserRole.HorseOwner
        });
        db.Owners.Add(new Owner
        {
            Id = ownerId,
            UserId = ownerUserId,
            OwnerCode = "OWNER-TEST"
        });
        db.Horses.Add(new Horse { Id = horseId, Name = "Test Horse", OwnerId = ownerId, ApprovalStatus = ApprovalStatus.Pending });
        db.Jockeys.Add(new Jockey { Id = jockeyId, UserId = Guid.NewGuid(), Status = "Active", ApprovalStatus = ApprovalStatus.Approved });
        await db.SaveChangesAsync();

        db.JockeyInvitations.AddRange(
            new JockeyInvitation { Id = Guid.NewGuid(), JockeyId = jockeyId, HorseId = horseId, Status = JockeyInvitationStatus.Pending, CreatedAt = DateTime.UtcNow },
            new JockeyInvitation { Id = Guid.NewGuid(), JockeyId = jockeyId, HorseId = horseId, Status = JockeyInvitationStatus.Accepted, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new JockeyInvitation { Id = Guid.NewGuid(), JockeyId = jockeyId, HorseId = horseId, Status = JockeyInvitationStatus.Declined, CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
        );
        db.JockeyInvitations.Add(new JockeyInvitation
        {
            Id = Guid.NewGuid(),
            JockeyId = jockeyId,
            HorseId = horseId,
            Status = JockeyInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        });
        await db.SaveChangesAsync();

        var repo = new JockeyInvitationRepository(db);
        var result = await repo.GetByJockeyAsync(jockeyId);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, item => item.Status == JockeyInvitationStatus.Pending);
        Assert.Contains(result, item => item.Status == JockeyInvitationStatus.Accepted);
        Assert.Contains(result, item => item.Status == JockeyInvitationStatus.Declined);
        Assert.All(result, item => Assert.Equal("Nguyễn Văn Chủ", item.Horse?.Owner?.User?.FullName));
    }
}
