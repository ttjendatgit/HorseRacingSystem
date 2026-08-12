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

    public async Task<ServiceResult<object>> RegisterAsync(Guid userId, Guid horseId, Guid raceId, RaceRegistrationRequest request)
    {
        var owner = await _owners.GetByUserIdAsync(userId);
        if (owner == null) return ServiceResult<object>.Fail(404, "Không tìm thấy hồ sơ chủ sở hữu");
        var horse = await _horses.GetOwnedHorseAsync(horseId, owner.Id);
        if (horse == null) return ServiceResult<object>.Fail(404, "Không tìm thấy ngựa");
        if (horse.ApprovalStatus != ApprovalStatus.Approved)
            return ServiceResult<object>.Fail(400, "Chỉ ngựa đã được phê duyệt mới có thể đăng ký tham gia");

        var race = await _races.GetByIdAsync(raceId);
        if (race == null) return ServiceResult<object>.Fail(404, "Không tìm thấy cuộc đua");
        if (race.Status != RaceStatus.RegistrationOpen)
            return ServiceResult<object>.Fail(400, "Cuộc đua chưa mở đăng ký");
        if (race.Tournament?.RegistrationDeadline is DateTime deadline && DateTime.UtcNow > deadline.ToUniversalTime())
            return ServiceResult<object>.Fail(400, "Đã quá hạn đăng ký của giải đấu");
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
        await _entries.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ValidateRaceEntriesForStartAsync(Guid raceId)
    {
        var entries = await _entries.GetByRaceAsync(raceId);
        if (entries.Count == 0) return ServiceResult<bool>.Fail(400, "Cuộc đua chưa có ngựa tham gia");

        // A user who owns the horse and rides it has already consented to both
        // roles by registering. This also repairs entries created before that
        // intent was persisted on the confirmation flags.
        var selfRegisteredEntries = entries.Where(e =>
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

        var healthPassed = await _db.HorseHealthChecks
            .Where(h => h.RaceId == raceId && h.ApprovedToRace && h.Status == HealthCheckStatus.Passed)
            .Select(h => h.HorseId).Distinct().ToListAsync();

        var invalidReasons = new List<string>();
        foreach (var e in entries)
        {
            var horseName = e.Horse?.Name ?? e.HorseId.ToString();
            var reasons = new List<string>();

            if (e.Status != RegistrationStatus.Approved) reasons.Add("Entry chưa Approved");
            if (!e.OwnerConfirmed) reasons.Add("Chủ ngựa chưa xác nhận (OwnerConfirmed=false)");
            if (!e.JockeyConfirmed) reasons.Add("Kỵ sĩ chưa xác nhận (JockeyConfirmed=false)");
            if (e.JockeyId == null) reasons.Add("Chưa chọn kỵ sĩ");
            if (e.ScratchedAt != null) reasons.Add("Ngựa đã bị rút lui (Scratched)");
            if (e.Horse?.ApprovalStatus != ApprovalStatus.Approved) reasons.Add("Hồ sơ ngựa chưa được Admin duyệt");
            if (!healthPassed.Contains(e.HorseId)) reasons.Add("Chưa có kiểm tra sức khỏe Đạt/Đã phê duyệt");

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
