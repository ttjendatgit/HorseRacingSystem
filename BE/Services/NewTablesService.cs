using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

// PRIZE-V1: Tournament.PrizePool is the total prize budget; Prize rows allocate that budget by
// FINAL Tournament ranking Position (Part 20 — Position means rank in the Tournament's Official
// Final ranking; CreateAsync/UpdateAsync never read RaceResult/RankingsJson, they only record the
// allocation an Admin configures; Amount is frozen by PrizeAmountCalculator at that point, never
// recomputed later). Create/Update/Get/Delete remain config/display only — RaceId stays always
// null (Prize is Tournament-scoped, never Race-specific).
// PRIZE-V2 (Phase 4): DistributeAsync below is the one exception — Admin-triggered, manual
// (never automatic on Tournament->Finished), reads the frozen Prize.Amount rows and pays each to
// the Owner standing at that Position per RaceService.GetFinalStandingsAsync, using the same
// atomic-claim-then-credit pattern as PredictionRefundHelper/PredictionService (claim
// Prize.IsDistributed via a conditional ExecuteUpdateAsync BEFORE crediting the wallet, roll the
// claim back if the wallet credit fails) — no new idempotency mechanism invented.
public class PrizeService : IPrizeService
{
    private readonly IPrizeRepository _repo;
    private readonly ITournamentRepository _tournamentRepo;
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _db;
    private readonly IRaceService _raceService;
    private readonly IWalletService _walletService;
    public PrizeService(IPrizeRepository repo, ITournamentRepository tournamentRepo, IUnitOfWork uow, ApplicationDbContext db, IRaceService raceService, IWalletService walletService)
    {
        _repo = repo;
        _tournamentRepo = tournamentRepo;
        _uow = uow;
        _db = db;
        _raceService = raceService;
        _walletService = walletService;
    }

    /// <summary>
    /// PRIZE-V1.1 PART 1: Prize.Position must not exceed the Tournament's structural planned Final
    /// capacity (see PlannedFinalParticipantsHelper) — never actual registrations/RaceEntry counts.
    /// If the plan is not yet determinate (e.g. multi-round with no pre-Final Round configured yet),
    /// rejects rather than allowing an unbounded Position, per "do NOT silently clamp Position."
    /// </summary>
    private async Task<string?> ValidatePositionAgainstFinalLimitAsync(Tournament tournament, int position)
    {
        var planned = await PlannedFinalParticipantsHelper.ComputeAsync(_db, tournament.Id, tournament.MaxRounds, tournament.MaxParticipants);
        if (!planned.HasValue)
            return "Chưa xác định được số người có thể tham gia Vòng chung kết của giải đấu.";
        if (position > planned.Value)
            return "Hạng thưởng vượt quá số người có thể tham gia Vòng chung kết.";
        return null;
    }

    /// <summary>PRIZE-V1.2 FINAL HARDENING Part 1: PercentageOfPool is stored as decimal(5,2) —
    /// at most 2 fractional digits. Decimal-safe check (no floating point involved anywhere):
    /// rounding to 2 places must not change the value. Never silently rounds the submitted value
    /// — a value with more precision than the column supports is rejected outright, not clamped.
    /// </summary>
    private static bool HasMoreThanTwoDecimalPlaces(decimal value) => Math.Round(value, 2) != value;

    /// <summary>PRIZE-V1.2 PART 21: friendlier default when Admin leaves Name blank — never
    /// overwrites an Admin-entered Name (callers only invoke this when the supplied Name is
    /// null/whitespace).</summary>
    private static string DefaultPrizeName(int position) => position switch
    {
        1 => "Vô địch",
        2 => "Á quân",
        3 => "Quý quân",
        _ => $"Hạng {position}",
    };

    public async Task<ServiceResult<PrizeResponse>> CreateAsync(CreatePrizeRequest r)
    {
        if (!r.TournamentId.HasValue)
            return ServiceResult<PrizeResponse>.Fail(400, "Vui lòng chọn giải đấu cho cơ cấu giải thưởng.");

        var tournament = await _tournamentRepo.GetByIdAsync(r.TournamentId.Value);
        if (tournament == null)
            return ServiceResult<PrizeResponse>.Fail(404, "Không tìm thấy giải đấu.");

        if (tournament.Status != TournamentStatus.Draft)
            return ServiceResult<PrizeResponse>.Fail(400, "Cơ cấu giải thưởng chỉ có thể chỉnh sửa khi giải đấu ở trạng thái Nháp.");

        if (r.Position < 1)
            return ServiceResult<PrizeResponse>.Fail(400, "Hạng thưởng phải lớn hơn hoặc bằng 1.");

        // PRIZE-V1.2 PART 1: PercentageOfPool replaces Amount as the value Admin controls.
        if (r.PercentageOfPool <= 0)
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ phải lớn hơn 0.");
        if (r.PercentageOfPool > 100)
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ không được vượt quá 100%.");
        // FINAL HARDENING Part 1: precision must be checked before any total-percentage/Amount
        // calculation runs against this value.
        if (HasMoreThanTwoDecimalPlaces(r.PercentageOfPool))
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ chỉ được tối đa 2 chữ số thập phân.");

        var finalLimitError = await ValidatePositionAgainstFinalLimitAsync(tournament, r.Position);
        if (finalLimitError != null)
            return ServiceResult<PrizeResponse>.Fail(400, finalLimitError);

        if (await _repo.ExistsPositionAsync(tournament.Id, r.Position, excludePrizeId: null))
            return ServiceResult<PrizeResponse>.Fail(409, "Hạng thưởng này đã được cấu hình.");

        // PRIZE-V1.2 PART 2: percentage total, not Amount total, is the Draft-time completeness
        // source of truth — SUM(PercentageOfPool) may be <= 100 while Draft (incomplete
        // allocation allowed), but never exceed it.
        var allocatedPercentageSoFar = await _repo.GetAllocatedPercentageAsync(tournament.Id, excludePrizeId: null);
        if (allocatedPercentageSoFar + r.PercentageOfPool > 100)
            return ServiceResult<PrizeResponse>.Fail(400, "Tổng tỷ lệ phân bổ không được vượt quá 100%.");

        var prize = new Prize
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RaceId = null, // V1: allocation is Tournament-scoped by Final ranking, never Race-specific
            Name = string.IsNullOrWhiteSpace(r.Name) ? DefaultPrizeName(r.Position) : r.Name,
            Amount = 0, // derived below via PrizeAmountCalculator, never client-controlled
            // Canonical monetary convention for this product is VND (see PRIZE-V1 report §9) — the
            // legacy entity default of "USD" is only ever overridden here, never relied upon.
            Currency = "VND",
            Position = r.Position,
            PercentageOfPool = r.PercentageOfPool,
            SponsorName = r.SponsorName,
            Description = null,
            IsDistributed = false,
            DistributedAt = null,
            CreatedAt = DateTime.UtcNow,
        };

        // PRIZE-V1.2 PART 3/4: recompute every row's Amount for this Tournament together — adding
        // a row changes the total percentage, which can shift what the "last row" (by Position)
        // should absorb as its rounding remainder. Existing rows come back from a tracking query
        // (PrizeRepository.GetByTournamentAsync has no AsNoTracking), so mutating .Amount in
        // memory is enough for SaveChangesAsync to persist them — no separate Update() call needed.
        var existingPrizes = (await _repo.GetByTournamentAsync(tournament.Id)).ToList();
        var allPrizes = existingPrizes.Append(prize);
        PrizeAmountCalculator.RecalculateAmounts(allPrizes, tournament.PrizePool);

        await _repo.AddAsync(prize);
        await _uow.SaveChangesAsync();
        return ServiceResult<PrizeResponse>.Success(Map(prize), 201);
    }

    public async Task<ServiceResult<PrizeResponse>> UpdateAsync(Guid id, UpdatePrizeRequest r)
    {
        var prize = await _repo.GetByIdAsync(id);
        if (prize == null)
            return ServiceResult<PrizeResponse>.Fail(404, "Không tìm thấy giải thưởng.");

        if (!prize.TournamentId.HasValue)
            return ServiceResult<PrizeResponse>.Fail(400, "Giải thưởng này không thuộc giải đấu nào và không thể chỉnh sửa qua luồng cơ cấu giải thưởng.");

        var tournament = await _tournamentRepo.GetByIdAsync(prize.TournamentId.Value);
        if (tournament == null)
            return ServiceResult<PrizeResponse>.Fail(404, "Không tìm thấy giải đấu.");

        if (tournament.Status != TournamentStatus.Draft)
            return ServiceResult<PrizeResponse>.Fail(400, "Cơ cấu giải thưởng chỉ có thể chỉnh sửa khi giải đấu ở trạng thái Nháp.");

        if (r.Position < 1)
            return ServiceResult<PrizeResponse>.Fail(400, "Hạng thưởng phải lớn hơn hoặc bằng 1.");

        if (r.PercentageOfPool <= 0)
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ phải lớn hơn 0.");
        if (r.PercentageOfPool > 100)
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ không được vượt quá 100%.");
        // FINAL HARDENING Part 1: precision must be checked before any total-percentage/Amount
        // calculation runs against this value.
        if (HasMoreThanTwoDecimalPlaces(r.PercentageOfPool))
            return ServiceResult<PrizeResponse>.Fail(400, "Tỷ lệ phân bổ chỉ được tối đa 2 chữ số thập phân.");

        var finalLimitError = await ValidatePositionAgainstFinalLimitAsync(tournament, r.Position);
        if (finalLimitError != null)
            return ServiceResult<PrizeResponse>.Fail(400, finalLimitError);

        if (await _repo.ExistsPositionAsync(tournament.Id, r.Position, excludePrizeId: prize.Id))
            return ServiceResult<PrizeResponse>.Fail(409, "Hạng thưởng này đã được cấu hình.");

        var allocatedPercentageExcludingThis = await _repo.GetAllocatedPercentageAsync(tournament.Id, excludePrizeId: prize.Id);
        if (allocatedPercentageExcludingThis + r.PercentageOfPool > 100)
            return ServiceResult<PrizeResponse>.Fail(400, "Tổng tỷ lệ phân bổ không được vượt quá 100%.");

        // TournamentId is immutable after creation (Part 9) — never reassigned here. To move a
        // Prize to another Tournament, delete it in Draft and create a new one there.
        prize.Position = r.Position;
        prize.PercentageOfPool = r.PercentageOfPool;
        prize.Name = string.IsNullOrWhiteSpace(r.Name) ? DefaultPrizeName(r.Position) : r.Name;
        prize.SponsorName = r.SponsorName;

        var allPrizesForUpdate = await _repo.GetByTournamentAsync(tournament.Id); // includes `prize` itself (already tracked/mutated above)
        PrizeAmountCalculator.RecalculateAmounts(allPrizesForUpdate, tournament.PrizePool);

        await _repo.UpdateAsync(prize);
        await _uow.SaveChangesAsync();
        return ServiceResult<PrizeResponse>.Ok(Map(prize));
    }

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByTournamentAsync(Guid tid) =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetByTournamentAsync(tid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByRaceAsync(Guid rid) =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetByRaceAsync(rid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var prize = await _repo.GetByIdAsync(id);
        if (prize == null)
            return ServiceResult<bool>.Fail(404, "Không tìm thấy giải thưởng.");

        Tournament? tournamentForRecalc = null;
        if (prize.TournamentId.HasValue)
        {
            tournamentForRecalc = await _tournamentRepo.GetByIdAsync(prize.TournamentId.Value);
            if (tournamentForRecalc != null && tournamentForRecalc.Status != TournamentStatus.Draft)
                return ServiceResult<bool>.Fail(400, "Cơ cấu giải thưởng chỉ có thể chỉnh sửa khi giải đấu ở trạng thái Nháp.");
        }

        await _repo.DeleteAsync(id);

        // PRIZE-V1.2 PART 3/4: removing a row changes the total percentage and which row is now
        // "last" by Position — recompute the remaining rows' Amounts before saving.
        if (tournamentForRecalc != null)
        {
            var remaining = (await _repo.GetByTournamentAsync(tournamentForRecalc.Id))
                .Where(p => p.Id != id).ToList();
            PrizeAmountCalculator.RecalculateAmounts(remaining, tournamentForRecalc.PrizePool);
        }

        await _uow.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// PRIZE-V2 (Phase 4): manual, Admin-triggered payout — never automatic on Tournament-&gt;Finished.
    /// Reads the already-frozen Prize.Amount rows (computed once by PrizeAmountCalculator while
    /// Draft, never recomputed here) and pays each un-distributed Prize to the Owner standing at
    /// that Position in RaceService.GetFinalStandingsAsync's Standings. Prize.Amount is split
    /// between Owner and Jockey using JockeyInvitation.JockeySharePercentage when an Accepted
    /// invitation exists for that pairing in the tournament; otherwise 100% goes to the Owner.
    /// One Prize's failure never blocks the others.
    /// </summary>
    public async Task<ServiceResult<PrizeDistributionResultDto>> DistributeAsync(Guid tournamentId)
    {
        var standingsResult = await _raceService.GetFinalStandingsAsync(tournamentId);
        if (!standingsResult.Result.Success || standingsResult.Result.Data == null)
            return ServiceResult<PrizeDistributionResultDto>.Fail(standingsResult.StatusCode, standingsResult.Result.Message ?? "Không thể đọc kết quả chung cuộc.");

        var standings = standingsResult.Result.Data;
        if (!standings.IsFinal)
            return ServiceResult<PrizeDistributionResultDto>.Fail(400, "Giải đấu chưa kết thúc, chưa thể trao thưởng.");
        if (standings.IsVoid)
            return ServiceResult<PrizeDistributionResultDto>.Fail(400, "Giải đấu đã bị huỷ, không có kết quả để trao thưởng.");
        if (standings.RequiresManualReview)
            return ServiceResult<PrizeDistributionResultDto>.Fail(400, "Vòng quyết định có nhiều cuộc đua song song, cần xử lý thủ công trước khi trao thưởng.");

        var standingByPosition = (standings.Standings ?? new List<StandingEntryDto>())
            .ToDictionary(s => s.Position, s => s);

        // AsNoTracking + direct DbContext query (not _repo.GetByTournamentAsync, which is tracked):
        // the atomic claim below is a bulk ExecuteUpdateAsync, which bypasses the change tracker —
        // a tracked read here would keep returning a stale IsDistributed=false for a Prize this
        // same DbContext already tracked earlier (e.g. a prior DistributeAsync call in the same
        // scope), same reasoning as WalletRepository.AddBalanceAsync/GetWalletBalanceAsync elsewhere.
        var pending = await _db.Prizes
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId && !p.IsDistributed)
            .OrderBy(p => p.Position)
            .ToListAsync();

        var result = new PrizeDistributionResultDto { TournamentId = tournamentId };

        // Rỗng (chưa cấu hình Prize nào, hoặc đã trao hết từ trước) — thành công, không phải lỗi.
        foreach (var prize in pending)
        {
            if (!standingByPosition.TryGetValue(prize.Position, out var entry) || entry.OwnerUserId == null || entry.OwnerId == null)
            {
                result.Skipped.Add(new PrizeDistributionSkippedDto
                {
                    Position = prize.Position,
                    Amount = prize.Amount,
                    Reason = !standingByPosition.ContainsKey(prize.Position)
                        ? $"Không có ngựa nào về đích ở Hạng {prize.Position}."
                        : $"Ngựa ở Hạng {prize.Position} chưa xác định được Chủ sở hữu (Owner) để trao thưởng."
                });
                continue;
            }

            // Atomic claim TRƯỚC khi cộng ví
            var claimed = await _db.Prizes
                .Where(p => p.Id == prize.Id && !p.IsDistributed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsDistributed, true)
                    .SetProperty(p => p.DistributedAt, DateTime.UtcNow));

            if (claimed == 0)
            {
                result.Skipped.Add(new PrizeDistributionSkippedDto { Position = prize.Position, Amount = prize.Amount, Reason = "Đã trao trước đó." });
                continue;
            }

            // Tra cứu kỵ sĩ chính thức và tỉ lệ ăn chia (nếu có).
            // UserId lấy thẳng từ bảng Jockeys — không phụ thuộc Include(User) trên RaceEntry,
            // vì thiếu navigation đó trước đây khiến cả phần chia cho kỵ sĩ bị bỏ qua dù Owner vẫn nhận 100%.
            Guid? jockeyId = entry.JockeyId;
            Guid? jockeyUserId = null;
            decimal jockeySharePercentage = 0;
            decimal jockeyAmount = 0;
            decimal ownerAmount = prize.Amount;

            if (jockeyId == null)
            {
                jockeyId = await _db.RaceEntries
                    .AsNoTracking()
                    .Where(re => re.HorseId == entry.HorseId
                        && re.JockeyId != null
                        && re.Race != null
                        && re.Race.TournamentId == tournamentId)
                    .OrderByDescending(re => re.Race!.ScheduledAt)
                    .Select(re => re.JockeyId)
                    .FirstOrDefaultAsync();
            }

            if (jockeyId != null)
            {
                jockeyUserId = await _db.Jockeys
                    .AsNoTracking()
                    .Where(j => j.Id == jockeyId)
                    .Select(j => (Guid?)j.UserId)
                    .FirstOrDefaultAsync();

                var invitations = await _db.JockeyInvitations
                    .AsNoTracking()
                    .Where(i => i.HorseId == entry.HorseId
                        && i.JockeyId == jockeyId
                        && i.Status == JockeyInvitationStatus.Accepted
                        && (i.RaceId == null || _db.Races.Any(r => r.Id == i.RaceId && r.TournamentId == tournamentId)))
                    .ToListAsync();

                var invitation = invitations
                    .OrderByDescending(i => i.JockeySharePercentage)
                    .ThenByDescending(i => i.CreatedAt)
                    .FirstOrDefault();

                if (invitation != null && invitation.JockeySharePercentage > 0 && jockeyUserId.HasValue)
                {
                    jockeySharePercentage = invitation.JockeySharePercentage;
                    jockeyAmount = Math.Round(prize.Amount * (jockeySharePercentage / 100m), 2);
                    ownerAmount = prize.Amount - jockeyAmount;
                }
            }

            try
            {
                using (var creditScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var ownerWalletResult = await _walletService.AddPointsAsync(entry.OwnerUserId.Value, ownerAmount, $"prize_owner_{prize.Id}");
                    if (!ownerWalletResult.Result.Success)
                        throw new InvalidOperationException(ownerWalletResult.Result.Message ?? "Không thể cộng tiền vào ví chủ ngựa.");

                    if (jockeyAmount > 0 && jockeyUserId.HasValue)
                    {
                        var jockeyWalletResult = await _walletService.AddPointsAsync(jockeyUserId.Value, jockeyAmount, $"prize_jockey_{prize.Id}");
                        if (!jockeyWalletResult.Result.Success)
                            throw new InvalidOperationException(jockeyWalletResult.Result.Message ?? "Không thể cộng tiền vào ví kỵ sĩ.");
                    }

                    _db.PrizeDistributionLogs.Add(new PrizeDistributionLog
                    {
                        Id = Guid.NewGuid(),
                        PrizeId = prize.Id,
                        TournamentId = tournamentId,
                        Position = prize.Position,
                        OwnerId = entry.OwnerId.Value,
                        OwnerUserId = entry.OwnerUserId.Value,
                        HorseId = entry.HorseId,
                        HorseName = entry.HorseName,
                        Amount = prize.Amount,
                        Currency = prize.Currency,
                        JockeyId = jockeyId,
                        JockeyUserId = jockeyUserId,
                        OwnerAmount = ownerAmount,
                        JockeyAmount = jockeyAmount,
                        JockeySharePercentage = jockeySharePercentage,
                        DistributedAt = DateTime.UtcNow,
                    });
                    await _uow.SaveChangesAsync();

                    creditScope.Complete();
                }

                result.Distributed.Add(new PrizeDistributionEntryDto
                {
                    Position = prize.Position,
                    HorseName = entry.HorseName,
                    OwnerName = entry.OwnerName,
                    JockeyName = entry.JockeyName,
                    Amount = prize.Amount,
                    OwnerAmount = ownerAmount,
                    JockeyAmount = jockeyAmount,
                    JockeySharePercentage = jockeySharePercentage,
                    Currency = prize.Currency,
                });
            }
            catch (Exception ex)
            {
                await _db.Prizes
                    .Where(p => p.Id == prize.Id && p.IsDistributed)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.IsDistributed, false)
                        .SetProperty(p => p.DistributedAt, (DateTime?)null));

                result.Errors.Add(new PrizeDistributionErrorDto { Position = prize.Position, Amount = prize.Amount, Reason = ex.Message });
            }
        }

        return ServiceResult<PrizeDistributionResultDto>.Ok(result);
    }

    /// <summary>
    /// PRIZE-V2 (Phase 4): Owner-facing "lịch sử nhận thưởng" — đọc PrizeDistributionLog (nguồn sự
    /// thật duy nhất cho payout đã thực sự xảy ra; Prize không lưu OwnerId nên không suy ngược
    /// được), lọc theo OwnerUserId == userId hiện tại (JWT của người gọi), mới nhất trước.
    /// </summary>
    public async Task<ServiceResult<List<PrizeHistoryEntryDto>>> GetMyPrizeHistoryAsync(Guid ownerUserId)
    {
        var history = await _db.PrizeDistributionLogs
            .AsNoTracking()
            .Where(l => l.OwnerUserId == ownerUserId)
            .OrderByDescending(l => l.DistributedAt)
            .Select(l => new PrizeHistoryEntryDto
            {
                TournamentId = l.TournamentId,
                TournamentName = l.Tournament != null ? l.Tournament.Name : string.Empty,
                Position = l.Position,
                HorseName = l.HorseName,
                Amount = l.OwnerAmount > 0 ? l.OwnerAmount : l.Amount,
                Currency = l.Currency,
                DistributedAt = l.DistributedAt,
            })
            .ToListAsync();

        return ServiceResult<List<PrizeHistoryEntryDto>>.Ok(history);
    }

    public async Task<ServiceResult<List<JockeyPrizeHistoryEntryDto>>> GetMyJockeyPrizeHistoryAsync(Guid jockeyUserId)
    {
        var history = await _db.PrizeDistributionLogs
            .AsNoTracking()
            .Where(l => l.JockeyUserId == jockeyUserId && l.JockeyAmount > 0)
            .OrderByDescending(l => l.DistributedAt)
            .Select(l => new JockeyPrizeHistoryEntryDto
            {
                TournamentId = l.TournamentId,
                TournamentName = l.Tournament != null ? l.Tournament.Name : string.Empty,
                Position = l.Position,
                HorseName = l.HorseName,
                TotalPrizeAmount = l.Amount,
                JockeyAmount = l.JockeyAmount,
                OwnerAmount = l.OwnerAmount,
                JockeySharePercentage = l.JockeySharePercentage,
                Currency = l.Currency,
                DistributedAt = l.DistributedAt,
            })
            .ToListAsync();

        return ServiceResult<List<JockeyPrizeHistoryEntryDto>>.Ok(history);
    }

    private static PrizeResponse Map(Prize p) => new()
    {
        Id = p.Id, TournamentId = p.TournamentId, Position = p.Position,
        PercentageOfPool = p.PercentageOfPool, Amount = p.Amount,
        Name = p.Name, SponsorName = p.SponsorName, CreatedAt = p.CreatedAt,
    };
}

public class ProtestService : IProtestService
{
    private readonly IProtestRepository _repo;
    private readonly IRaceRepository _raceRepo;
    private readonly IRaceResultRepository _raceResultRepo;
    private readonly IRaceEntryRepository _entryRepo;
    private readonly IOwnerRepository _ownerRepo;
    private readonly IJockeyRepository _jockeyRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService? _notifications;
    private readonly IAuditLogService? _auditLogs;

    public ProtestService(
        IProtestRepository repo,
        IRaceRepository raceRepo,
        IRaceResultRepository raceResultRepo,
        IRaceEntryRepository entryRepo,
        IOwnerRepository ownerRepo,
        IJockeyRepository jockeyRepo,
        IUserRepository userRepo,
        IUnitOfWork uow,
        INotificationService? notifications = null,
        IAuditLogService? auditLogs = null)
    {
        _repo = repo;
        _raceRepo = raceRepo;
        _raceResultRepo = raceResultRepo;
        _entryRepo = entryRepo;
        _ownerRepo = ownerRepo;
        _jockeyRepo = jockeyRepo;
        _userRepo = userRepo;
        _uow = uow;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    public async Task<ServiceResult<ProtestResponse>> FileAsync(CreateProtestRequest r, Guid userId)
    {
        // R0.1: at minimum, an Official result and a Cancelled race must not
        // accept a new Protest — no evidence in current FE/business supports
        // a narrower race-status window than that, so nothing more is added.
        var race = await _raceRepo.GetByIdAsync(r.RaceId);
        if (race == null)
            return ServiceResult<ProtestResponse>.Fail(404, "Không tìm thấy cuộc đua");
        if (race.Result?.Status == RaceResultStatus.Official)
            return ServiceResult<ProtestResponse>.Fail(409, "Kết quả cuộc đua đã chính thức và không thể phát sinh/thay đổi khiếu nại.");
        if (race.Status == RaceStatus.Cancelled)
            return ServiceResult<ProtestResponse>.Fail(400, "Không thể khiếu nại cuộc đua đã bị hủy.");

        if (r.AgainstEntryId == Guid.Empty)
            return ServiceResult<ProtestResponse>.Fail(400, "AgainstEntryId is required.");
        if (string.IsNullOrWhiteSpace(r.Reason))
            return ServiceResult<ProtestResponse>.Fail(400, "Reason is required.");

        var againstEntry = await _entryRepo.GetByIdWithHorseAsync(r.AgainstEntryId, r.RaceId);
        if (againstEntry == null)
            return ServiceResult<ProtestResponse>.Fail(400, "AgainstEntry must belong to the protested race.");

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null)
            return ServiceResult<ProtestResponse>.Fail(403, "Only a known race participant can file a protest.");

        if (!await HasFilingStandingAsync(user, r.RaceId))
            return ServiceResult<ProtestResponse>.Fail(403, "You do not have standing to file a protest for this race.");

        if (await _repo.HasActiveByFilerRaceEntryAsync(userId, r.RaceId, r.AgainstEntryId))
            return ServiceResult<ProtestResponse>.Fail(409, "An active protest already exists for this race entry.");

        var protest = new Protest
        {
            Id = Guid.NewGuid(), RaceId = r.RaceId, FiledByUserId = userId,
            AgainstEntryId = r.AgainstEntryId, Reason = r.Reason.Trim(), Evidence = r.Evidence,
            Status = ProtestStatus.Pending, FiledAt = DateTime.UtcNow,
            Race = race, FiledByUser = user, AgainstEntry = againstEntry
        };
        await _repo.AddAsync(protest);
        await _uow.SaveChangesAsync();
        await NotifyFilerAsync(protest, "Protest filed", "Your race result protest has been filed.");
        return ServiceResult<ProtestResponse>.Success(Map(protest), 201);
    }

    public async Task<ServiceResult<IEnumerable<ProtestResponse>>> GetPendingAsync() =>
        ServiceResult<IEnumerable<ProtestResponse>>.Ok((await _repo.GetPendingAsync()).Select(Map));

    public async Task<ServiceResult<IEnumerable<ProtestResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<ProtestResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<IEnumerable<ProtestResponse>>> GetByFiledByUserAsync(Guid filedByUserId) =>
        ServiceResult<IEnumerable<ProtestResponse>>.Ok((await _repo.GetByFiledByUserAsync(filedByUserId)).Select(Map));

    public async Task<ServiceResult<ProtestResponse>> MarkUnderReviewAsync(Guid id, Guid reviewedByUserId)
    {
        var protest = await _repo.GetByIdAsync(id);
        if (protest == null) return ServiceResult<ProtestResponse>.Fail(404, "KhÃ´ng tÃ¬m tháº¥y khiáº¿u náº¡i");

        var officialGuard = await EnsureRaceIsNotOfficialAsync(protest.RaceId);
        if (officialGuard != null) return officialGuard;

        if (protest.Status != ProtestStatus.Pending)
            return ServiceResult<ProtestResponse>.Fail(400, "Only a pending protest can be marked under review.");

        var oldStatus = protest.Status;
        protest.Status = ProtestStatus.UnderReview;
        await _repo.UpdateAsync(protest);
        await _uow.SaveChangesAsync();
        await AuditAdminActionAsync(protest, reviewedByUserId, AuditAction.Update, oldStatus, protest.Status, "Protest marked under review.");
        await NotifyFilerAsync(protest, "Protest under review", "An admin has started reviewing your protest.");
        return ServiceResult<ProtestResponse>.Ok(Map(protest));
    }

    public async Task<ServiceResult<ProtestResponse>> RuleAsync(Guid id, RuleProtestRequest r, Guid ruledByUserId)
    {
        var protest = await _repo.GetByIdAsync(id);
        if (protest == null) return ServiceResult<ProtestResponse>.Fail(404, "Không tìm thấy khiếu nại");

        // R0.1: post-Official immutability — ruling a Protest must not be
        // able to imply the ranking should change once the Result is
        // already Official.
        var race = await _raceRepo.GetByIdAsync(protest.RaceId);
        if (race?.Result?.Status == RaceResultStatus.Official)
            return ServiceResult<ProtestResponse>.Fail(409, "Kết quả cuộc đua đã chính thức và không thể phát sinh/thay đổi khiếu nại.");

        if (protest.Status != ProtestStatus.Pending && protest.Status != ProtestStatus.UnderReview)
            return ServiceResult<ProtestResponse>.Fail(400, "Terminal protests cannot be changed.");
        if (r.Outcome is not ProtestStatus.Upheld and not ProtestStatus.Rejected)
            return ServiceResult<ProtestResponse>.Fail(400, "Outcome must be Upheld or Rejected.");

        var oldStatus = protest.Status;
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            protest.Status = r.Outcome.Value;
            protest.Ruling = r.Ruling;
            protest.Resolution = r.Resolution;
            protest.AdminNotes = r.AdminNotes;
            protest.RuledByUserId = ruledByUserId;
            protest.RuledAt = DateTime.UtcNow;
            protest.ResolvedAt = protest.RuledAt;
            await _repo.UpdateAsync(protest);

            if (protest.Status == ProtestStatus.Upheld)
            {
                var raceResult = await _raceResultRepo.GetByRaceIdAsync(protest.RaceId);
                if (raceResult?.Status == RaceResultStatus.Provisional)
                {
                    raceResult.RejectedReason = RaceResultCorrectionMessages.UpheldProtestRequiresCorrection;
                    await _raceResultRepo.UpdateAsync(raceResult);
                }
            }

            await _uow.SaveChangesAsync();
            scope.Complete();
        }
        await AuditAdminActionAsync(
            protest,
            ruledByUserId,
            protest.Status == ProtestStatus.Upheld ? AuditAction.Approve : AuditAction.Reject,
            oldStatus,
            protest.Status,
            $"Protest ruled {protest.Status}.");
        await NotifyFilerAsync(protest, $"Protest {protest.Status}", "An admin has issued a final protest decision.");
        return ServiceResult<ProtestResponse>.Ok(Map(protest));
    }

    public async Task<ServiceResult<ProtestResponse>> WithdrawAsync(Guid id, Guid requestingUserId)
    {
        var protest = await _repo.GetByIdAsync(id);
        if (protest == null) return ServiceResult<ProtestResponse>.Fail(404, "Protest not found.");
        if (protest.FiledByUserId != requestingUserId)
            return ServiceResult<ProtestResponse>.Fail(403, "Only the original filer can withdraw this protest.");

        var officialGuard = await EnsureRaceIsNotOfficialAsync(protest.RaceId);
        if (officialGuard != null) return officialGuard;

        if (protest.Status != ProtestStatus.Pending && protest.Status != ProtestStatus.UnderReview)
            return ServiceResult<ProtestResponse>.Fail(400, "Terminal protests cannot be withdrawn.");

        protest.Status = ProtestStatus.Withdrawn;
        protest.ResolvedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(protest);
        await _uow.SaveChangesAsync();
        await NotifyFilerAsync(protest, "Protest withdrawn", "Your protest has been withdrawn.");
        return ServiceResult<ProtestResponse>.Ok(Map(protest));
    }

    private async Task<ServiceResult<ProtestResponse>?> EnsureRaceIsNotOfficialAsync(Guid raceId)
    {
        var race = await _raceRepo.GetByIdAsync(raceId);
        return race?.Result?.Status == RaceResultStatus.Official
            ? ServiceResult<ProtestResponse>.Fail(409, "Race result is official and protests can no longer be changed.")
            : null;
    }

    private async Task<bool> HasFilingStandingAsync(User user, Guid raceId)
    {
        if (user.Role == UserRole.Admin)
            return true;

        if (user.Role == UserRole.HorseOwner)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(user.Id);
            return owner != null && await _entryRepo.OwnerHasHorseInRaceAsync(raceId, owner.Id);
        }

        if (user.Role == UserRole.Jockey)
        {
            var jockey = await _jockeyRepo.GetByUserIdAsync(user.Id);
            if (jockey == null) return false;
            var entries = await _entryRepo.GetByRaceAsync(raceId);
            return entries.Any(e => e.JockeyId == jockey.Id);
        }

        return false;
    }

    private async Task NotifyFilerAsync(Protest protest, string title, string message)
    {
        if (_notifications == null || protest.FiledByUserId == Guid.Empty) return;
        await _notifications.CreateNotificationAsync(new CreateNotificationDto
        {
            UserId = protest.FiledByUserId,
            Title = title,
            Message = message,
            Type = NotificationType.InApp,
            Category = NotificationCategory.Other,
            RelatedEntityId = protest.Id,
            RelatedEntityType = nameof(Protest),
            ActionUrl = "/profile"
        });
    }

    private async Task AuditAdminActionAsync(
        Protest protest,
        Guid adminUserId,
        AuditAction action,
        ProtestStatus oldStatus,
        ProtestStatus newStatus,
        string description)
    {
        if (_auditLogs == null) return;
        await _auditLogs.LogActionAsync(new CreateAuditLogDto
        {
            AdminId = adminUserId,
            EntityType = nameof(Protest),
            EntityId = protest.Id,
            Action = action,
            OldValues = JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
            NewValues = JsonSerializer.Serialize(new { Status = newStatus.ToString() }),
            Description = description,
            UserId = protest.FiledByUserId
        });
    }

    private static ProtestResponse Map(Protest p) => new()
    {
        Id = p.Id, RaceId = p.RaceId, RaceName = p.Race?.Name, FiledByUserId = p.FiledByUserId,
        FiledByName = p.FiledByUser?.FullName, AgainstEntryId = p.AgainstEntryId,
        AgainstHorseName = p.AgainstEntry?.Horse?.Name, Reason = p.Reason, Evidence = p.Evidence,
        Status = p.Status.ToString(), Ruling = p.Ruling, Resolution = p.Resolution, AdminNotes = p.AdminNotes,
        RuledByUserId = p.RuledByUserId, FiledAt = p.FiledAt, RuledAt = p.RuledAt, ResolvedAt = p.ResolvedAt
    };
}

public class HorseTransferService : IHorseTransferService
{
    private readonly IHorseTransferRepository _repo;
    private readonly IUnitOfWork _uow;
    public HorseTransferService(IHorseTransferRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<HorseTransferResponse>> CreateAsync(CreateHorseTransferRequest r, Guid fromOwnerId)
    {
        var transfer = new HorseTransfer
        {
            Id = Guid.NewGuid(), HorseId = r.HorseId, FromOwnerId = fromOwnerId, ToOwnerId = r.ToOwnerId,
            TransferType = Enum.Parse<TransferType>(r.TransferType), Price = r.Price, Reason = r.Reason,
            Status = TransferStatus.Pending, RequestedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(transfer);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Success(Map(transfer), 201);
    }

    public async Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetPendingAsync() =>
        ServiceResult<IEnumerable<HorseTransferResponse>>.Ok((await _repo.GetPendingAsync()).Select(Map));

    public async Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<HorseTransferResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<HorseTransferResponse>> ApproveAsync(Guid id, ApproveHorseTransferRequest r, Guid approvedByUserId)
    {
        var t = await _repo.GetByIdAsync(id);
        if (t == null) return ServiceResult<HorseTransferResponse>.Fail(404, "Không tìm thấy chuyển nhượng");
        t.Status = TransferStatus.Approved;
        t.ApprovedByUserId = approvedByUserId;
        t.ApprovedAt = DateTime.UtcNow;
        t.CompletedAt = DateTime.UtcNow;
        t.AdminNotes = r.AdminNotes;
        await _repo.UpdateAsync(t);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Ok(Map(t));
    }

    public async Task<ServiceResult<HorseTransferResponse>> RejectAsync(Guid id, string reason, Guid approvedByUserId)
    {
        var t = await _repo.GetByIdAsync(id);
        if (t == null) return ServiceResult<HorseTransferResponse>.Fail(404, "Không tìm thấy chuyển nhượng");
        t.Status = TransferStatus.Rejected;
        t.ApprovedByUserId = approvedByUserId;
        t.ApprovedAt = DateTime.UtcNow;
        t.AdminNotes = reason;
        await _repo.UpdateAsync(t);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Ok(Map(t));
    }

    private static HorseTransferResponse Map(HorseTransfer t) => new()
    {
        Id = t.Id, HorseId = t.HorseId, HorseName = t.Horse?.Name, FromOwnerId = t.FromOwnerId,
        FromOwnerName = t.FromOwner?.User?.FullName, ToOwnerId = t.ToOwnerId,
        ToOwnerName = t.ToOwner?.User?.FullName, TransferType = t.TransferType.ToString(),
        Price = t.Price, Reason = t.Reason, Status = t.Status.ToString(), AdminNotes = t.AdminNotes,
        RequestedAt = t.RequestedAt, CompletedAt = t.CompletedAt
    };
}

public class ContractService : IContractService
{
    private readonly IContractRepository _repo;
    private readonly IUnitOfWork _uow;
    public ContractService(IContractRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<ContractResponse>> CreateAsync(CreateContractRequest r)
    {
        var c = new Contract
        {
            Id = Guid.NewGuid(), OwnerId = r.OwnerId, JockeyId = r.JockeyId, HorseId = r.HorseId,
            Title = r.Title, Status = ContractStatus.Draft, StartDate = r.StartDate, EndDate = r.EndDate,
            BaseFee = r.BaseFee, WinBonusPercent = r.WinBonusPercent, PerRaceFee = r.PerRaceFee,
            TermsAndConditions = r.TermsAndConditions, CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Success(Map(c), 201);
    }

    public async Task<ServiceResult<ContractResponse>> SignByOwnerAsync(Guid id, Guid ownerId)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null || c.OwnerId != ownerId) return ServiceResult<ContractResponse>.Fail(404, "Không tìm thấy hợp đồng");
        c.SignedByOwnerAt = DateTime.UtcNow;
        if (c.SignedByJockeyAt != null) c.Status = ContractStatus.Active;
        await _repo.UpdateAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Ok(Map(c));
    }

    public async Task<ServiceResult<ContractResponse>> SignByJockeyAsync(Guid id, Guid jockeyId)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null || c.JockeyId != jockeyId) return ServiceResult<ContractResponse>.Fail(404, "Không tìm thấy hợp đồng");
        c.SignedByJockeyAt = DateTime.UtcNow;
        if (c.SignedByOwnerAt != null) c.Status = ContractStatus.Active;
        await _repo.UpdateAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Ok(Map(c));
    }

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetByOwnerAsync(Guid oid) =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetByOwnerAsync(oid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetByJockeyAsync(Guid jid) =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetByJockeyAsync(jid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    private static ContractResponse Map(Contract c) => new()
    {
        Id = c.Id, OwnerId = c.OwnerId, OwnerName = c.Owner?.User?.FullName, JockeyId = c.JockeyId,
        JockeyName = c.Jockey?.User?.FullName, HorseId = c.HorseId, HorseName = c.Horse?.Name,
        Title = c.Title, Status = c.Status.ToString(), StartDate = c.StartDate, EndDate = c.EndDate,
        BaseFee = c.BaseFee, WinBonusPercent = c.WinBonusPercent, PerRaceFee = c.PerRaceFee,
        TermsAndConditions = c.TermsAndConditions, SignedByOwnerAt = c.SignedByOwnerAt,
        SignedByJockeyAt = c.SignedByJockeyAt, CreatedAt = c.CreatedAt
    };
}

public class InjuryRecordService : IInjuryRecordService
{
    private readonly IInjuryRecordRepository _repo;
    private readonly IUnitOfWork _uow;
    public InjuryRecordService(IInjuryRecordRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<InjuryRecordResponse>> CreateAsync(CreateInjuryRecordRequest r, Guid reportedByUserId)
    {
        var record = new InjuryRecord
        {
            Id = Guid.NewGuid(), HorseId = r.HorseId, InjuryType = r.InjuryType,
            Description = r.Description, Severity = Enum.Parse<InjurySeverity>(r.Severity),
            BodyPart = r.BodyPart, Treatment = r.Treatment, Medication = r.Medication,
            VeterinarianName = r.VeterinarianName, ExpectedRecoveryDate = r.ExpectedRecoveryDate,
            RequiresSurgery = r.RequiresSurgery, ReportedByUserId = reportedByUserId,
            DiagnosedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(record);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Success(Map(record), 201);
    }

    public async Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetByHorseAsync(Guid hid) =>
        ServiceResult<IEnumerable<InjuryRecordResponse>>.Ok((await _repo.GetByHorseAsync(hid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<InjuryRecordResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<InjuryRecordResponse>> MarkRecoveredAsync(Guid id)
    {
        var r = await _repo.GetByIdAsync(id);
        if (r == null) return ServiceResult<InjuryRecordResponse>.Fail(404, "Không tìm thấy bản ghi");
        r.Status = InjuryStatus.Recovered;
        r.RecoveredAt = DateTime.UtcNow;
        await _repo.UpdateAsync(r);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Ok(Map(r));
    }

    public async Task<ServiceResult<InjuryRecordResponse>> ClearToRaceAsync(Guid id)
    {
        var r = await _repo.GetByIdAsync(id);
        if (r == null) return ServiceResult<InjuryRecordResponse>.Fail(404, "Không tìm thấy bản ghi");
        r.ClearedToRace = true;
        r.ClearedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(r);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Ok(Map(r));
    }

    private static InjuryRecordResponse Map(InjuryRecord r) => new()
    {
        Id = r.Id, HorseId = r.HorseId, HorseName = r.Horse?.Name, Severity = r.Severity.ToString(),
        Status = r.Status.ToString(), InjuryType = r.InjuryType, Description = r.Description,
        BodyPart = r.BodyPart, Treatment = r.Treatment, VeterinarianName = r.VeterinarianName,
        DiagnosedAt = r.DiagnosedAt, ExpectedRecoveryDate = r.ExpectedRecoveryDate,
        RecoveredAt = r.RecoveredAt, ClearedToRace = r.ClearedToRace, ClearedAt = r.ClearedAt
    };
}
