using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Services;

public class RaceEntryService : IRaceEntryService
{
    private readonly IOwnerRepository _owners;
    private readonly IHorseRepository _horses;
    private readonly IJockeyRepository _jockeys;
    private readonly IRaceRepository _races;
    private readonly IRaceEntryRepository _entries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;

    public RaceEntryService(IOwnerRepository owners, IHorseRepository horses, IJockeyRepository jockeys,
        IRaceRepository races, IRaceEntryRepository entries, IUnitOfWork unitOfWork, ApplicationDbContext db)
    {
        _owners = owners;
        _horses = horses;
        _jockeys = jockeys;
        _races = races;
        _entries = entries;
        _unitOfWork = unitOfWork;
        _db = db;
    }

    private static bool DirectRaceRegistrationDisabled => true;

    /// <summary>
    /// Đăng ký ngựa và kỵ thủ tham gia vào cuộc đua cụ thể (Chế độ đăng ký trực tiếp).
    /// Kiểm tra điều kiện phê duyệt của ngựa, thời hạn giải đấu và lịch thi đấu của kỵ thủ.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng đăng ký.</param>
    /// <param name="horseId">Mã định danh con ngựa thi đấu.</param>
    /// <param name="raceId">Mã định danh cuộc đua.</param>
    /// <param name="request">Yêu cầu đăng ký thi đấu.</param>
    /// <returns>Kết quả đăng ký tham gia cuộc đua.</returns>
    public async Task<ServiceResult<object>> RegisterAsync(Guid userId, Guid horseId, Guid raceId, RaceRegistrationRequest request)
    {
        if (DirectRaceRegistrationDisabled)
        {
            return ServiceResult<object>.Fail(400,
                "Đăng ký trực tiếp vào cuộc đua không còn được hỗ trợ. Vui lòng đăng ký tham gia giải đấu.");
        }
        var owner = await _owners.GetByUserIdAsync(userId);
        if (owner == null) return ServiceResult<object>.Fail(404, "Không tìm thấy hồ sơ chủ sở hữu");
        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null) return ServiceResult<object>.Fail(404, "Không tìm thấy ngựa");
        if (horse.ApprovalStatus != ApprovalStatus.Approved)
            return ServiceResult<object>.Fail(400, "Chỉ ngựa đã được phê duyệt mới có thể đăng ký tham gia");
        if (horse.IsArchived)
            return ServiceResult<object>.Fail(400, "Ngựa đã được lưu trữ (archive) và không thể đăng ký cuộc đua mới");

        var race = await _races.GetByIdAsync(raceId);
        if (race == null) return ServiceResult<object>.Fail(404, "Không tìm thấy cuộc đua");
        if (race.Status != RaceStatus.RegistrationOpen)
            return ServiceResult<object>.Fail(400, "Cuộc đua chưa mở đăng ký");
        if (race.Tournament?.RegistrationDeadline is DateTime deadline && DateTime.UtcNow > deadline.ToUniversalTime())
            return ServiceResult<object>.Fail(400, "Đã quá hạn đăng ký của giải đấu");

        // Task B Final: global RaceEntry invariant — Horse must hold an Approved
        // TournamentHorseRegistration for THIS Race's Tournament, same gate as
        // RaceManagementService.AssignHorseToRaceAsync/BulkAssignHorsesToRaceAsync. No
        // registration / Pending / Rejected / Withdrawn / registration for a different
        // Tournament are all rejected identically here.
        var registration = await _db.TournamentHorseRegistrations.FirstOrDefaultAsync(x =>
            x.TournamentId == race.TournamentId && x.HorseId == horseId);
        if (registration == null || registration.Status != RegistrationStatus.Approved)
            return ServiceResult<object>.Fail(400, "Ngựa chưa được duyệt đăng ký tham gia giải đấu này.");
        if (race.Entries.Count(e => e.Status != RegistrationStatus.Rejected && e.ScratchedAt == null) >= race.MaxParticipants)
            return ServiceResult<object>.Fail(409, $"Cuộc đua đã đủ số lượng tham gia tối đa ({race.MaxParticipants})");
        if (await _entries.ExistsAsync(raceId, horseId))
            return ServiceResult<object>.Fail(409, "Ngựa đã được đăng ký");
        if (await _entries.OwnerHasHorseInRaceAsync(raceId, owner.Id))
            return ServiceResult<object>.Fail(400, "Chủ ngựa chỉ có thể đăng ký một ngựa trong một cuộc đua");

        var acceptedInvitation = horse.JockeyInvitations
            .Where(i => i.Status == JockeyInvitationStatus.Accepted)
            .OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        var selfJockey = acceptedInvitation == null ? await _jockeys.GetByUserIdAsync(userId) : null;
        var jockeyId = acceptedInvitation?.JockeyId ?? selfJockey?.Id;
        if (jockeyId.HasValue && await _entries.HasJockeyScheduleConflictAsync(jockeyId.Value, race.ScheduledAt,
                race.ScheduledEndAt ?? race.ScheduledAt.AddMinutes(30)))
            return ServiceResult<object>.Fail(409, "Kỵ sĩ đã có cuộc đua trùng thời gian");

        var entry = new RaceEntry { Id = Guid.NewGuid(), RaceId = raceId, HorseId = horseId,
            JockeyId = jockeyId, Status = RegistrationStatus.Pending, OwnerConfirmed = true,
            JockeyConfirmed = jockeyId.HasValue };
        await _entries.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<object>.Ok(entry);
    }

    /// <summary>
    /// Phê duyệt một hồ sơ đăng ký thi đấu của ngựa vào cuộc đua (dành cho Ban tổ chức).
    /// </summary>
    /// <param name="entryId">Mã định danh hồ sơ đăng ký cuộc đua.</param>
    /// <returns>Kết quả phê duyệt đăng ký.</returns>
    public async Task<ServiceResult<bool>> ApproveAsync(Guid entryId)
    {
        var entry = await _entries.GetByIdAsync(entryId);
        if (entry == null) return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký tham gia");
        if (entry.Race?.Status is not (RaceStatus.RegistrationOpen or RaceStatus.RegistrationClosed))
            return ServiceResult<bool>.Fail(400, "Không thể duyệt entry trong trạng thái hiện tại của cuộc đua");
        var approvedCount = (await _entries.GetByRaceAsync(entry.RaceId)).Count(e => e.Status == RegistrationStatus.Approved && e.ScratchedAt == null);
        if (approvedCount >= entry.Race.MaxParticipants) return ServiceResult<bool>.Fail(409, "Cuộc đua đã đủ suất được duyệt");
        entry.Status = RegistrationStatus.Approved;
        entry.ScratchedAt = null;
        entry.ScratchReason = null;
        await _entries.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> RejectAsync(Guid entryId, string? reason)
    {
        var entry = await _entries.GetByIdAsync(entryId);
        if (entry == null) return ServiceResult<bool>.Fail(404, "Không tìm thấy đăng ký tham gia");
        if (entry.Race?.Status is RaceStatus.InProgress or RaceStatus.Finished)
            return ServiceResult<bool>.Fail(400, "Không thể từ chối entry sau khi cuộc đua bắt đầu");
        entry.Status = RegistrationStatus.Rejected;
        entry.ScratchedAt = DateTime.UtcNow;
        entry.ScratchReason = string.IsNullOrWhiteSpace(reason) ? "Bị từ chối bởi admin" : reason.Trim();
        // GATE-V1: a rejected entry is no longer participating (RaceEntryService.
        // ValidateRaceEntriesForStartAsync excludes it), so its gate frees up for another entry —
        // this is the only production path that sets ScratchedAt (there is no separate Scratch
        // endpoint yet), so clearing it here covers both reject and scratch without redesigning
        // the scratch lifecycle.
        entry.GateNumber = null;
        await _entries.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ValidateRaceEntriesForStartAsync(Guid raceId)
    {
        var entries = await _entries.GetByRaceAsync(raceId);
        if (entries.Count == 0) return ServiceResult<bool>.Fail(400, "Cuộc đua chưa có ngựa tham gia");

        // R1a: a Rejected or Scratched entry is kept in history (never deleted) but is no
        // longer a real participant, so it must not be evaluated for start readiness — and
        // must not count toward "no participants remain" either. An entry that is still
        // participating but merely incomplete (missing confirmation/jockey/health/etc.) is
        // NOT filtered out here — it still fails the readiness loop below by design.
        var participatingEntries = entries
            .Where(e => e.Status != RegistrationStatus.Rejected && e.ScratchedAt == null)
            .ToList();
        if (participatingEntries.Count == 0)
            return ServiceResult<bool>.Fail(400, "Cuộc đua không còn ngựa nào đang tham gia (tất cả đã bị từ chối/rút lui)");

        // A user who owns the horse and rides it has already consented to both
        // roles by registering. This also repairs entries created before that
        // intent was persisted on the confirmation flags.
        var selfRegisteredEntries = participatingEntries.Where(e =>
            e.JockeyId.HasValue &&
            e.Horse?.Owner?.UserId == e.Jockey?.UserId &&
            (!e.OwnerConfirmed || !e.JockeyConfirmed)).ToList();
        foreach (var entry in selfRegisteredEntries)
        {
            entry.OwnerConfirmed = true;
            entry.JockeyConfirmed = true;
        }
        if (selfRegisteredEntries.Count > 0)
            await _unitOfWork.SaveChangesAsync();

        // R0.1: the LATEST health check per Horse+Race is authoritative — an
        // older Passed check no longer clears a horse whose most recent
        // check is Failed/RequiresRecheck (see report). One query for the
        // whole race, grouped in-memory, avoids an N+1 per entry.
        var healthChecksForRace = await _db.HorseHealthChecks
            .Where(h => h.RaceId == raceId)
            .ToListAsync();
        var latestHealthCheckByHorseId = healthChecksForRace
            .GroupBy(h => h.HorseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.CheckedAt).First());

        var invalidReasons = new List<string>();
        foreach (var e in participatingEntries)
        {
            var horseName = e.Horse?.Name ?? e.HorseId.ToString();
            var reasons = new List<string>();

            if (e.Status != RegistrationStatus.Approved) reasons.Add("Entry chưa Approved");
            if (!e.OwnerConfirmed) reasons.Add("Chủ ngựa chưa xác nhận (OwnerConfirmed=false)");
            if (!e.JockeyConfirmed) reasons.Add("Kỵ sĩ chưa xác nhận (JockeyConfirmed=false)");
            if (e.JockeyId == null) reasons.Add("Chưa chọn kỵ sĩ");
            if (e.Horse?.ApprovalStatus != ApprovalStatus.Approved) reasons.Add("Hồ sơ ngựa chưa được Admin duyệt");

            // R1a: defense-in-depth re-check of Jockey PERSON eligibility. Invite/Accept/
            // FinalConfirm already enforce this at pairing time, but approval/active status
            // can change afterward (e.g. Admin rejects/deactivates the Jockey later) — this
            // must be re-verified right before the race actually starts.
            if (e.JockeyId.HasValue)
            {
                if (e.Jockey == null || e.Jockey.ApprovalStatus != ApprovalStatus.Approved)
                    reasons.Add("Hồ sơ kỵ sĩ chưa được Admin duyệt");
                if (e.Jockey?.User != null && !e.Jockey.User.IsActive)
                    reasons.Add("Tài khoản kỵ sĩ đã bị vô hiệu hóa");
            }

            var hasValidLatestHealthCheck = latestHealthCheckByHorseId.TryGetValue(e.HorseId, out var latestCheck)
                && latestCheck.Status == HealthCheckStatus.Passed
                && latestCheck.ApprovedToRace;
            if (!hasValidLatestHealthCheck) reasons.Add("Chưa có kiểm tra sức khỏe Đạt/Đã phê duyệt (theo lần kiểm tra gần nhất)");

            if (e.JockeyId.HasValue && reasons.Count == 0)
            {
                var race = e.Race ?? await _races.GetByIdAsync(raceId);
                if (race != null && await _entries.HasJockeyScheduleConflictAsync(e.JockeyId!.Value,
                        race.ScheduledAt, race.ScheduledEndAt ?? race.ScheduledAt.AddMinutes(30), e.Id))
                {
                    reasons.Add("Kỵ sĩ bị trùng lịch đua");
                }
            }

            if (reasons.Count > 0)
            {
                invalidReasons.Add($"{horseName} [{string.Join(", ", reasons)}]");
            }
        }

        return invalidReasons.Count == 0
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail(400, $"Entry chưa đủ điều kiện xuất phát:\n{string.Join("\n", invalidReasons)}");
    }
}
