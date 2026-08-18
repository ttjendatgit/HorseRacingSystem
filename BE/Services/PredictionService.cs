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

    /// <summary>
    /// Phase2B: settlement is only valid for a Finished race with an Official
    /// result — never on Finished alone, since a Finished race may still carry
    /// a Provisional (or no) result.
    ///
    /// Idempotency: the payout loop operates ONLY on the prediction IDs this
    /// specific invocation claims (captured by GetPendingWinnerIdsAsync
    /// before any mutation), never on a fresh "all Won predictions for this
    /// race" read. This matters specifically for the retry-after-partial-
    /// failure case: if winner A was already paid by an earlier invocation
    /// and winner B's payout failed and was reverted to Pending, a retry's
    /// bulk update legitimately transitions B (and only B) from Pending to
    /// Won — but a query of "all Won for this race" at that point would
    /// still include A, and a payout loop driven by that query would re-pay
    /// A a second time. Scoping to the pre-captured claimed-ID set makes
    /// that impossible: A is never in the retry's claimed set because it was
    /// no longer Pending when the retry captured its IDs.
    ///
    /// Partial-failure recovery: a winner whose wallet payout throws is
    /// reverted from Won back to Pending via PredictionRepository.
    /// RevertWonToPendingAsync — a direct single-row bulk update, not a
    /// tracked-entity mutation, so the revert cannot be silently lost to a
    /// stale already-tracked entity in the DbContext's identity map — so it
    /// is picked up correctly by a later retry's claimed-ID capture instead
    /// of being silently stranded as "Won" with no payout ever recorded.
    /// The payout exception is deliberately caught here rather than left to
    /// propagate: AdminService.ApproveRaceResultAsync's TransactionScope
    /// must still commit the Provisional-&gt;Official transition and the
    /// other winners' successful payouts even if one spectator's wallet
    /// operation fails — see RaceLifecycleTests.
    /// Settlement_PartialPayoutFailure_DoesNotStrandOrDoublePay for the
    /// failure-injection proof that this is race-scoped and safe to retry.
    /// </summary>
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

        if (race.Result == null || race.Result.Status != RaceResultStatus.Official)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Kết quả cuộc đua chưa chính thức (Official).");
        }

        // Capture exactly which winning predictions THIS invocation is
        // claiming, before any mutation — see the idempotency note above.
        var claimedWinnerIds = await _predictions.GetPendingWinnerIdsAsync(raceId, winningHorseId);

        // Atomically mark losers — only Pending predictions NOT for the winning horse
        var losersAffected = await _predictions.ExecuteUpdateLosersAsync(raceId, winningHorseId);

        // Atomically mark winners — sets PayoutAmount = PotentialPayout (or BetAmount * 2 as default)
        // and transitions status from Pending to Won. Affects exactly the
        // rows captured above (nothing else could have gone Pending->Won for
        // this race/horse in between, since only this code path does that).
        var winnersAffected = await _predictions.ExecuteUpdateWinnersAsync(raceId, winningHorseId);

        if (losersAffected == 0 && winnersAffected == 0)
        {
            // Nothing was Pending — either there were never any predictions, or
            // settlement already ran for this race. Either way, there is
            // nothing new to pay.
            return ServiceResult<object>.Ok(new { raceId, winningHorseId, settled = 0, alreadySettled = true });
        }

        // Pay exactly the claimed set — never a fresh "all Won" read.
        var winners = claimedWinnerIds.Count > 0
            ? await _predictions.GetByIdsAsync(claimedWinnerIds)
            : new List<Prediction>();
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
                // Revert winner status back to Pending so it can be retried —
                // a direct bulk update, independent of change-tracker state.
                await _predictions.RevertWonToPendingAsync(w.Id);
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
            settled = winners.Count - failed.Count,
            failed = failed.Count
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
