using System;
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
    [HttpPost]
    [Authorize(Roles = "HorseOwner,Jockey")]
    public async Task<ActionResult> Register([FromBody] RegisterTournamentHorseRequest r)
    {
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == GetUserId());
        if (owner == null)
            return NotFound(new { message = "Không tìm thấy hồ sơ chủ ngựa" });

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == r.TournamentId);
        if (tournament == null)
            return NotFound(new { message = "Không tìm thấy giải đấu" });

        var horse = await _db.Horses.FirstOrDefaultAsync(h => h.Id == r.HorseId && h.OwnerId == owner.Id);
        if (horse == null)
            return NotFound(new { message = "Không tìm thấy ngựa của bạn" });
        if (horse.ApprovalStatus != ApprovalStatus.Approved)
            return BadRequest(new { message = "Ngựa chưa được admin phê duyệt" });

        var exists = await _db.TournamentHorseRegistrations.AnyAsync(x =>
            x.TournamentId == r.TournamentId && x.HorseId == r.HorseId && x.Status != RegistrationStatus.Rejected);
        if (exists)
            return BadRequest(new { message = "Ngựa này đã được đăng ký vào giải đấu" });

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
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Approve(Guid id)
    {
        var registration = await _db.TournamentHorseRegistrations.FirstOrDefaultAsync(x => x.Id == id);
        if (registration == null)
            return NotFound(new { message = "Không tìm thấy đăng ký" });

        registration.Status = RegistrationStatus.Approved;
        registration.ApprovedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return Ok(new { message = "Đã duyệt ngựa vào giải." });
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
