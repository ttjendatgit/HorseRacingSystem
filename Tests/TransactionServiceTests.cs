using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TransactionServiceTests
{
    [Fact]
    public async Task GetHistoryAsync_ReturnsDepositsOrderedByNewest()
    {
        var userId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Amount = 100000, Status = "completed", CreatedAt = DateTime.UtcNow.AddMinutes(-5), CompletedAt = DateTime.UtcNow.AddMinutes(-4), Reference = "AAA" },
            new() { Id = Guid.NewGuid(), UserId = userId, Amount = 50000, Status = "pending", CreatedAt = DateTime.UtcNow.AddMinutes(-1), Reference = "BBB" },
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 20000, Status = "completed", CreatedAt = DateTime.UtcNow, Reference = "CCC" },
        };

        var service = new TransactionService(
            new FakeTransactionRepository(transactions),
            new FakeUserRepository(),
            new FakeWalletService(),
            new FakeNotificationService(),
            new FakeUnitOfWork(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<TransactionService>.Instance);

        var result = await service.GetHistoryAsync(userId);

        Assert.True(result.IsSuccess);
        var items = Assert.IsType<List<DepositHistoryItem>>(result.Result.Data);
        Assert.Equal(2, items.Count);
        Assert.Equal("completed", items[0].Status);
        Assert.Equal("pending", items[1].Status);
        Assert.Equal(100000m, items[0].Amount);
    }

    private sealed class FakeTransactionRepository : ITransactionRepository
    {
        private readonly List<Transaction> _transactions;

        public FakeTransactionRepository(List<Transaction> transactions) => _transactions = transactions;

        public Task AddAsync(Transaction transaction) => Task.CompletedTask;
        public Task<Transaction?> GetByReferenceAsync(string reference) => Task.FromResult(_transactions.FirstOrDefault(t => t.Reference == reference));
        public Task<Transaction?> GetLatestByUserAsync(Guid userId) => Task.FromResult(_transactions.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).FirstOrDefault());
        public Task<bool> HasCompletedSinceAsync(Guid userId, DateTime since) => Task.FromResult(_transactions.Any(t => t.UserId == userId && t.Status == "completed" && t.CompletedAt >= since));
        public Task<Transaction?> GetPendingByRefAsync(string reference) => Task.FromResult(_transactions.FirstOrDefault(t => t.Reference == reference && t.Status == "pending"));
        public Task<bool> ExistsBySepayIdAsync(long sepayTransactionId) => Task.FromResult(false);
        public Task<bool> TryCompleteByRefAsync(string reference, long sepayTransactionId) => Task.FromResult(true);
        public Task<List<string>> GetPendingReferencesAsync() => Task.FromResult(_transactions.Where(t => t.Status == "pending" && !string.IsNullOrEmpty(t.Reference)).Select(t => t.Reference!).ToList());
        public Task<List<Transaction>> GetHistoryByUserAsync(Guid userId) => Task.FromResult(_transactions.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).ToList());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid userId) => Task.FromResult<User?>(new User { Id = userId });
        public Task<User?> GetByRefreshTokenHashAsync(string hash) => Task.FromResult<User?>(null);
        public Task<List<User>> GetAllAsync() => Task.FromResult(new List<User>());
        public Task AddAsync(User user) => Task.CompletedTask;
        public Task UpdateAsync(User user) => Task.CompletedTask;
    }

    private sealed class FakeWalletService : IWalletService
    {
        public Task<ServiceResult<object>> GetBalanceAsync(Guid userId) => Task.FromResult(ServiceResult<object>.Ok(new { balance = 0m }));
        public Task<ServiceResult<object>> AddFundsAsync(Guid userId, decimal amount, string reference) => Task.FromResult(ServiceResult<object>.Ok(new { added = amount }));
        public Task<ServiceResult<object>> AddPointsAsync(Guid userId, decimal points, string reference) => Task.FromResult(ServiceResult<object>.Ok(new { added = points }));
        public Task<ServiceResult<object>> DeductFundsAsync(Guid userId, decimal amount, string reference) => Task.FromResult(ServiceResult<object>.Ok(new { deducted = amount }));
        public Task<ServiceResult<decimal>> ConvertPointsToVndAsync(Guid userId, decimal points) => Task.FromResult(ServiceResult<decimal>.Ok(points));
        public decimal GetPointsPerVnd() => 1000m;
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<ServiceResult<NotificationDto>> CreateNotificationAsync(CreateNotificationDto dto) => Task.FromResult(ServiceResult<NotificationDto>.Ok(new NotificationDto()));
        public Task<ServiceResult<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId) => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));
        public Task<ServiceResult<List<NotificationDto>>> GetUnreadNotificationsAsync(Guid userId) => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));
        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsWithFilterAsync(Guid userId, NotificationFilterDto filter) => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));
        public Task<ServiceResult<NotificationDetailDto>> GetNotificationByIdAsync(Guid id) => Task.FromResult(ServiceResult<NotificationDetailDto>.Ok(new NotificationDetailDto()));
        public Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId) => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> MarkMultipleAsReadAsync(MarkNotificationsAsReadDto dto) => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> DeleteNotificationAsync(Guid notificationId) => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<bool>> DeleteAllNotificationsAsync(Guid userId) => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<int>> GetUnreadCountAsync(Guid userId) => Task.FromResult(ServiceResult<int>.Ok(0));
        public Task<ServiceResult<NotificationStatsDto>> GetNotificationStatsAsync(Guid userId) => Task.FromResult(ServiceResult<NotificationStatsDto>.Ok(new NotificationStatsDto()));
        public Task<ServiceResult<bool>> SendBulkNotificationsAsync(BulkNotificationDto dto) => Task.FromResult(ServiceResult<bool>.Ok(true));
        public Task<ServiceResult<List<NotificationDto>>> GetNotificationsForEntityAsync(string entityType, Guid entityId) => Task.FromResult(ServiceResult<List<NotificationDto>>.Ok(new List<NotificationDto>()));
        public Task ProcessUnsentNotificationsAsync() => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync() => Task.FromResult(1);
    }
}
