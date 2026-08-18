using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/tournament-registrations")]
public class TournamentRegistrationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public TournamentRegistrationsController(ApplicationDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    // ── Owner: đăng ký ngựa vào giải ──
    // Task B Final Correction: Owner-only. Jockey must never submit a TournamentHorseRegistration
    // — the Owner boundary is that Owner owns/manages Horses and submits Tournament registrations;
    // full Jockey invitation/license flow is a separate, later feature.
    [HttpPost]
    [Authorize(Roles = "HorseOwner")]
    public async Task<ActionResult> Register([FromBody] RegisterTournamentHorseRequest r)
    {
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == GetUserId());
        if (owner == null)
            return NotFound(new { message = "Không tìm thấy hồ sơ chủ ngựa" });

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == r.TournamentId);
        if (tournament == null)
            return NotFound(new { message = "Không tìm thấy giải đấu" });

        // Task B §1: Owner registration is open exactly while Tournament.Status == Published AND
        // now < RegistrationDeadline — no separate Tournament RegistrationOpen/Closed status
        // exists (locked spec §14.4); this is purely derived and re-checked on every submit.
        // Deadline equality or after is rejected (strict inequality, matches §4.5's UTC comparison rule).
        if (tournament.Status != TournamentStatus.Published)
            return BadRequest(new { message = "Giải đấu hiện không mở đăng ký." });
        if (!tournament.RegistrationDeadline.HasValue || DateTime.UtcNow >= tournament.RegistrationDeadline.Value)
            return BadRequest(new { message = "Đã hết hạn đăng ký cho giải đấu này." });

        var horse = await _db.Horses.FirstOrDefaultAsync(h => h.Id == r.HorseId && h.OwnerId == owner.Id);
        if (horse == null)
            return NotFound(new { message = "Không tìm thấy ngựa của bạn" });
        if (horse.ApprovalStatus != ApprovalStatus.Approved)
            return BadRequest(new { message = "Ngựa chưa được admin phê duyệt" });

        // Task B Correction 2 §1 / locked spec §5, §21.1: an Owner may have at most ONE active
        // (Pending/Approved) registration per Tournament, regardless of how many Horses they
        // own — checked BEFORE the Horse-scoped check below so the more specific Owner-level
        // reason surfaces first. Rejected/Withdrawn never block re-registration while the
        // deadline hasn't passed (already enforced above).
        var ownerHasActiveRegistration = await _db.TournamentHorseRegistrations.AnyAsync(x =>
            x.TournamentId == r.TournamentId && x.OwnerId == owner.Id
            && (x.Status == RegistrationStatus.Pending || x.Status == RegistrationStatus.Approved));
        if (ownerHasActiveRegistration)
            return BadRequest(new { message = "Bạn đã có một ngựa đang đăng ký hoặc đã được duyệt cho giải đấu này." });

        // Task B §3 / locked spec §5, §21.2: same shape, keyed on Horse — only Pending/Approved
        // are "active"; Rejected/Withdrawn never block re-registration. Kept alongside the
        // Owner-level check above (they protect different invariants — §21.1 vs §21.2).
        var hasActiveRegistration = await _db.TournamentHorseRegistrations.AnyAsync(x =>
            x.TournamentId == r.TournamentId && x.HorseId == r.HorseId
            && (x.Status == RegistrationStatus.Pending || x.Status == RegistrationStatus.Approved));
        if (hasActiveRegistration)
            return BadRequest(new { message = "Ngựa này đã được đăng ký vào giải đấu" });

        // Task B §5 (not in the locked spec, but does not contradict it — added per explicit task
        // scope): the same Horse cannot hold an active registration in two Published/Ongoing
        // Tournaments whose date windows overlap. Per-Horse, not per-Owner — a different Horse
        // owned by the same Owner may freely join an overlapping Tournament.
        if (await HasOverlappingActiveTournamentAsync(r.HorseId, r.TournamentId, tournament.StartDate, tournament.EndDate))
            return BadRequest(new { message = "Ngựa đang tham gia một giải đấu có thời gian trùng." });

        var registration = new TournamentHorseRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = r.TournamentId,
            HorseId = r.HorseId,
            OwnerId = owner.Id,
            Status = RegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.TournamentHorseRegistrations.Add(registration);
        await _unitOfWork.SaveChangesAsync();
        return Ok(registration);
    }

    // ── Owner: danh sách đăng ký của mình ──
    [HttpGet("my")]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> MyRegistrations()
    {
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == GetUserId());
        if (owner == null)
            return NotFound(new { message = "Không tìm thấy hồ sơ chủ ngựa" });

        var list = await _db.TournamentHorseRegistrations
            .Where(x => x.OwnerId == owner.Id)
            .Include(x => x.Tournament)
            .Include(x => x.Horse)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(list.Select(x => new
        {
            id = x.Id,
            tournamentId = x.TournamentId,
            tournamentName = x.Tournament?.Name,
            horseId = x.HorseId,
            horseName = x.Horse?.Name,
            status = x.Status.ToString(),
            note = x.Note,
            createdAt = x.CreatedAt,
            approvedAt = x.ApprovedAt
        }));
    }

    // ── Task B §8: Admin minimum display — Approved/Pending counts for one Tournament ──
    [HttpGet("tournament/{tournamentId:guid}/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Summary(Guid tournamentId)
    {
        var counts = await _db.TournamentHorseRegistrations
            .Where(x => x.TournamentId == tournamentId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var approvedCount = counts.FirstOrDefault(c => c.Status == RegistrationStatus.Approved)?.Count ?? 0;
        var pendingCount = counts.FirstOrDefault(c => c.Status == RegistrationStatus.Pending)?.Count ?? 0;

        return Ok(new { approvedCount, pendingCount });
    }

    // ── Ngựa đã được duyệt cho giải (dùng khi phân công vào cuộc đua) ──
    [HttpGet("tournament/{tournamentId:guid}/approved-horses")]
    [Authorize(Roles = "Admin,HorseOwner,Jockey")]
    public async Task<ActionResult> ApprovedHorses(Guid tournamentId)
    {
        var horses = await _db.TournamentHorseRegistrations
            .Where(x => x.TournamentId == tournamentId && x.Status == RegistrationStatus.Approved)
            .Include(x => x.Horse)!.ThenInclude(h => h!.Owner)!.ThenInclude(o => o!.User)
            .Include(x => x.Horse)!.ThenInclude(h => h!.JockeyInvitations)!.ThenInclude(i => i!.Jockey)!.ThenInclude(j => j!.User)
            .Include(x => x.Horse)!.ThenInclude(h => h!.RaceEntries)!.ThenInclude(e => e!.Jockey)!.ThenInclude(j => j!.User)
            .Include(x => x.Horse)!.ThenInclude(h => h!.RaceEntries)!.ThenInclude(e => e!.Race)
            .Select(x => x.Horse!)
            .Distinct()
            .ToListAsync();

        var result = horses.Select(h =>
        {
            var activeInv = h.JockeyInvitations
                .Where(i => i.Status == JockeyInvitationStatus.Accepted || i.Status == JockeyInvitationStatus.Pending)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefault();
            var raceJockey = h.RaceEntries
                .Where(e => e.Jockey != null)
                .OrderByDescending(e => e.Race?.ScheduledAt ?? DateTime.MinValue)
                .Select(e => e.Jockey)
                .FirstOrDefault();
            var jockey = activeInv?.Jockey ?? raceJockey;

            return new
            {
                h.Id,
                h.Name,
                h.Breed,
                h.Gender,
                h.Age,
                h.Color,
                h.Weight,
                h.Height,
                h.TotalRaces,
                h.TotalWins,
                h.ImageUrl,
                AssignedJockeyId = jockey?.Id,
                AssignedJockeyName = jockey?.User?.FullName
            };
        }).ToList();

        return Ok(result);
    }

    // ── Admin: danh sách chờ duyệt ──
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Pending()
    {
        var list = await _db.TournamentHorseRegistrations
            .Where(x => x.Status == RegistrationStatus.Pending)
            .Include(x => x.Tournament)
            .Include(x => x.Horse)!.ThenInclude(h => h!.Owner)!.ThenInclude(o => o!.User)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(list.Select(x => new
        {
            id = x.Id,
            tournamentId = x.TournamentId,
            tournamentName = x.Tournament?.Name,
            horseId = x.HorseId,
            horseName = x.Horse?.Name,
            ownerName = x.Horse?.Owner?.User?.FullName ?? x.Horse?.Owner?.User?.Email,
            createdAt = x.CreatedAt,
            status = x.Status.ToString()
        }));
    }

    // ── Admin: duyệt ──
    // Task B §4/§5: capacity (Approved < Tournament.MaxParticipants) and Horse-Tournament overlap
    // must be re-checked at Approve time, not just at submit time — both can have changed since
    // the registration was created. Wrapped in a Serializable transaction so two concurrent
    // Approve calls racing for the last slot can't both succeed: Postgres/Npgsql detects the
    // conflict at commit (SQLite, used in tests, is serializable by default).
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Approve(Guid id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var registration = await _db.TournamentHorseRegistrations.FirstOrDefaultAsync(x => x.Id == id);
        if (registration == null)
            return NotFound(new { message = "Không tìm thấy đăng ký" });

        if (registration.Status == RegistrationStatus.Approved)
            return Ok(new { message = "Đã duyệt ngựa vào giải." }); // idempotent no-op (locked spec §18 item 1)
        if (registration.Status != RegistrationStatus.Pending)
            return BadRequest(new { message = $"Không thể duyệt đăng ký ở trạng thái {registration.Status}." });

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == registration.TournamentId);
        if (tournament == null)
            return NotFound(new { message = "Không tìm thấy giải đấu" });

        // Capacity gate — Tournament.MaxParticipants, never Race.MaxParticipants.
        var approvedCount = await _db.TournamentHorseRegistrations.CountAsync(x =>
            x.TournamentId == registration.TournamentId && x.Status == RegistrationStatus.Approved);
        if (tournament.MaxParticipants.HasValue && approvedCount >= tournament.MaxParticipants.Value)
            return BadRequest(new { message = $"Giải đấu đã đủ số lượng ngựa tối đa ({tournament.MaxParticipants.Value})." });

        if (await HasOverlappingActiveTournamentAsync(registration.HorseId, registration.TournamentId, tournament.StartDate, tournament.EndDate))
            return BadRequest(new { message = "Ngựa đang tham gia một giải đấu có thời gian trùng." });

        registration.Status = RegistrationStatus.Approved;
        registration.ApprovedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        try
        {
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            return Conflict(new { message = "Một admin khác vừa duyệt đăng ký này. Vui lòng thử lại." });
        }

        return Ok(new { message = "Đã duyệt ngựa vào giải." });
    }

    /// <summary>
    /// Task B §5: true when the given Horse already holds a Pending/Approved registration in a
    /// Published/Ongoing Tournament (other than <paramref name="excludeTournamentId"/>) whose
    /// [StartDate, EndDate] window overlaps [candidateStart, candidateEnd). Cancelled/Finished
    /// Tournaments and Rejected/Withdrawn registrations never count. Back-to-back (touching, not
    /// overlapping) windows are allowed.
    /// </summary>
    private Task<bool> HasOverlappingActiveTournamentAsync(Guid horseId, Guid excludeTournamentId, DateTime candidateStart, DateTime candidateEnd)
    {
        return _db.TournamentHorseRegistrations
            .Where(x => x.HorseId == horseId
                && x.TournamentId != excludeTournamentId
                && (x.Status == RegistrationStatus.Pending || x.Status == RegistrationStatus.Approved))
            .Select(x => x.Tournament)
            .Where(t => t != null
                && (t.Status == TournamentStatus.Published || t.Status == TournamentStatus.Ongoing)
                && candidateStart < t.EndDate && candidateEnd > t.StartDate)
            .AnyAsync();
    }

    // ── Admin: từ chối ──
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectTournamentRegistrationRequest r)
    {
        var registration = await _db.TournamentHorseRegistrations.FirstOrDefaultAsync(x => x.Id == id);
        if (registration == null)
            return NotFound(new { message = "Không tìm thấy đăng ký" });

        registration.Status = RegistrationStatus.Rejected;
        registration.Note = r?.Reason ?? "Bị từ chối bởi admin";
        await _unitOfWork.SaveChangesAsync();
        return Ok(new { message = "Đã từ chối." });
    }
}

public class RegisterTournamentHorseRequest
{
    public Guid TournamentId { get; set; }
    public Guid HorseId { get; set; }
}

public class RejectTournamentRegistrationRequest
{
    public string? Reason { get; set; }
}
