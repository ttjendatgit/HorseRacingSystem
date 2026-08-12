using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class PredictionService : IPredictionService
{
    private readonly IRaceRepository _races;
    private readonly IPredictionRepository _predictions;
    private readonly IWalletService _walletService;
    private readonly IWalletRepository _walletRepo;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public PredictionService(
        IRaceRepository races,
        IPredictionRepository predictions,
        IWalletService walletService,
        IWalletRepository walletRepo,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _races = races;
        _predictions = predictions;
        _walletService = walletService;
        _walletRepo = walletRepo;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<object>> CreatePredictionAsync(Guid userId, PredictionCreateRequest request)
    {
        if (request.BetAmount <= 0)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số tiền cược phải lớn hơn 0");
        }

        var race = await _races.GetByIdWithEntriesAsync(request.RaceId);
        if (race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }

        // Allow predictions throughout race registration, until the time-based
        // cutoff below. This preserves the normal management lifecycle.
        if (race.Status != RaceStatus.Scheduled &&
            race.Status != RaceStatus.RegistrationOpen &&
            race.Status != RaceStatus.RegistrationClosed)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Cuộc đua không mở cho dự đoán");
        }

        // Khóa cược: không đặt trong vòng 5 phút trước giờ đua
        if (race.ScheduledAt - DateTime.UtcNow < TimeSpan.FromMinutes(5))
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Dự đoán đã đóng. Không thể đặt cược trong vòng 5 phút trước giờ đua.");
        }

        // Chặn cược trùng: mỗi khán giả chỉ đặt 1 dự đoán cho mỗi cuộc đua
        if (await _predictions.ExistsAsync(request.RaceId, userId))
        {
            return ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Bạn đã đặt dự đoán cho cuộc đua này rồi.");
        }

        var horseInRace = race.Entries.Any(e => e.HorseId == request.PredictedHorseId);
        if (!horseInRace)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Ngựa không đăng ký tham gia cuộc đua này");
        }

        // Check wallet balance before creating prediction
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        var balance = wallet?.Balance ?? 0;
        if (balance < request.BetAmount)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số dư không đủ để đặt cược.");
        }

        // Get odds from the race entry
        var entry = race.Entries.FirstOrDefault(e => e.HorseId == request.PredictedHorseId);
        var odds = entry?.Odds ?? 1.0m;

        // Save prediction FIRST
        var prediction = new Prediction
        {
            Id = Guid.NewGuid(),
            RaceId = request.RaceId,
            PredictedHorseId = request.PredictedHorseId,
            SpectatorUserId = userId,
            Status = PredictionStatus.Pending,
            BetAmount = request.BetAmount,
            Odds = odds,
            PotentialPayout = request.BetAmount * odds,
            HorseNameSnapshot = entry?.Horse?.Name,
            CreatedAt = DateTime.UtcNow
        };

        await _predictions.AddAsync(prediction);
        await _unitOfWork.SaveChangesAsync();

        // THEN deduct funds — if this fails, remove the prediction record
        var deductResult = await _walletService.DeductFundsAsync(userId, request.BetAmount, $"bet_{request.RaceId}");
        if (!deductResult.IsSuccess)
        {
            await _predictions.DeleteAsync(prediction.Id);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số dư không đủ để đặt cược.");
        }

        return ServiceResult<object>.Ok(new
        {
            prediction.Id,
            prediction.RaceId,
            prediction.PredictedHorseId,
            RaceName = race.Name,
            PredictedHorseName = prediction.PredictedHorse?.Name ?? entry?.Horse?.Name,
            Status = prediction.Status.ToString(),
            prediction.BetAmount,
            prediction.Odds,
            prediction.PotentialPayout,
            prediction.CreatedAt
        });
    }

    public async Task<ServiceResult<object>> SettlePredictionAsync(Guid raceId, Guid winningHorseId)
    {
        var race = await _races.GetByIdWithEntriesAsync(raceId);
        if (race == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy cuộc đua");
        }

        if (race.Status != RaceStatus.Finished)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Cuộc đua chưa kết thúc");
        }

        // Atomically mark losers first — only Pending predictions NOT for the winning horse
        await _predictions.ExecuteUpdateLosersAsync(raceId, winningHorseId);

        // Atomically mark winners — sets PayoutAmount = PotentialPayout (or BetAmount * 2 as default)
        // and transitions status from Pending to Won
        await _predictions.ExecuteUpdateWinnersAsync(raceId, winningHorseId);

        // Now load winners to pay them
        var winners = await _predictions.GetWinnersByRaceAsync(raceId);
        var failed = new List<Guid>();

        foreach (var w in winners)
        {
            var payout = w.PayoutAmount ?? w.PotentialPayout;
            if (payout <= 0) payout = w.BetAmount * 2;

            try
            {
                await _walletService.AddPointsAsync(w.SpectatorUserId, payout, $"win_{w.Id}");
            }
            catch (Exception ex)
            {
                failed.Add(w.Id);
                // Revert winner status back to Pending so it can be retried
                w.Status = PredictionStatus.Pending;
                w.PayoutAmount = null;
                w.SettledAt = null;
                Console.WriteLine($"Failed to pay winner {w.Id}: {ex.Message}");
                continue;
            }

            // Notify winner
            try
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = w.SpectatorUserId,
                    Title = "Chúc mừng! Bạn đã thắng cược",
                    Message = $"Ngựa {w.PredictedHorse?.Name ?? w.HorseNameSnapshot ?? "?"} đã về nhất! Bạn nhận được {payout:N0} điểm vào ví.",
                    Type = NotificationType.InApp,
                    Category = NotificationCategory.BetWon,
                    RelatedEntityId = w.RaceId,
                    RelatedEntityType = "Race"
                });
            }
            catch { /* non-critical: notification failure doesn't affect payout */ }
        }

        return ServiceResult<object>.Ok(new
        {
            raceId,
            winningHorseId,
            settled = winners.Count
        });
    }

    public async Task<ServiceResult<object>> GetMyPredictionsAsync(Guid userId)
    {
        var predictions = await _predictions.GetByUserAsync(userId);
        var result = predictions.Select(p => new
        {
            p.Id,
            p.RaceId,
            RaceName = p.Race?.Name ?? p.RaceId.ToString(),
            PredictedHorseId = p.PredictedHorseId,
            PredictedHorseName = p.PredictedHorse?.Name ?? p.HorseNameSnapshot ?? p.PredictedHorseId.ToString(),
            Status = p.Status.ToString(),
            p.BetAmount,
            p.Odds,
            p.PotentialPayout,
            p.PayoutAmount,
            p.CreatedAt,
            p.SettledAt
        });
        return ServiceResult<object>.Ok(result);
    }
}
