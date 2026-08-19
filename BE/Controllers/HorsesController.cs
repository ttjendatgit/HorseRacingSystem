using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services.Interfaces;
using HorseRacing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/horses")]
[Authorize(Roles = "HorseOwner,Jockey,Admin")]
public class HorsesController : ControllerBase
{
    private readonly IHorseService _horseService;
    private readonly IRaceEntryService _raceEntryService;
    private readonly ICloudStorageService _cloudStorage;
    private readonly IWebHostEnvironment _environment;

    public HorsesController(IHorseService horseService, IRaceEntryService raceEntryService, ICloudStorageService cloudStorage, IWebHostEnvironment environment)
    {
        _horseService = horseService;
        _raceEntryService = raceEntryService;
        _cloudStorage = cloudStorage;
        _environment = environment;
    }

    [HttpGet("my-entries")]
    public async Task<ActionResult> GetMyRaceEntries()
    {
        var ownerId = GetUserId();
        var horses = await _horseService.GetMyHorsesAsync(ownerId);
        if (horses.StatusCode != 200) return StatusCode(horses.StatusCode, horses.Result);

        var list = (horses.Result.Data as System.Collections.IEnumerable)?.Cast<Horse>() ?? Enumerable.Empty<Horse>();
        var entries = list.SelectMany(h => (h.RaceEntries ?? new List<RaceEntry>()).Select(e => new
        {
            EntryId = e.Id,
            HorseId = h.Id,
            HorseName = h.Name,
            RaceId = e.RaceId,
            RaceName = e.Race?.Name ?? e.RaceId.ToString(),
            TournamentId = e.Race?.Tournament?.Id,
            TournamentName = e.Race?.Tournament?.Name ?? string.Empty,
            RoundNumber = e.Race?.Round?.RoundNumber,
            RoundName = e.Race?.Round?.Name,
            ScheduledAt = e.Race?.ScheduledAt,
            ScheduledEndAt = e.Race?.ScheduledEndAt,
            Location = e.Race?.Track?.Name ?? e.Race?.Location ?? string.Empty,
            Distance = e.Race?.Distance,
            MaxParticipants = e.Race?.MaxParticipants,
            RaceStatus = e.Race?.Status.ToString() ?? string.Empty,
            Status = e.Status.ToString(),
            OwnerConfirmed = e.OwnerConfirmed,
            JockeyId = e.JockeyId,
            JockeyConfirmed = e.JockeyConfirmed,
            JockeyName = e.Jockey?.User?.FullName ?? string.Empty,
            GateNumber = e.GateNumber,
            FinishPosition = e.FinishPosition
        }));
        return Ok(entries);
    }

    [HttpGet]
    public async Task<ActionResult> GetMyHorses()
    {
        var ownerId = GetUserId();
        var result = await _horseService.GetMyHorsesAsync(ownerId);

        if (result.StatusCode == 200 && result.Result?.Data is IEnumerable<Horse> horses)
        {
            var mapped = horses.Select(MapHorseDto);
            return Ok(ApiResult<object>.Ok(mapped));
        }

        return StatusCode(result.StatusCode, result.Result);
    }

    // Task C1 UI correction: the primary Admin Horse management screen (/admin/horses) needs
    // every Horse — Pending ones especially, since those are exactly what Admin needs to act on —
    // not just already-Approved ones. This is Admin-only and this action's sole FE consumer.
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetAllHorses()
    {
        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        var horses = await db.Horses
            .AsSplitQuery()
            .Include(h => h.Owner).ThenInclude(o => o!.User)
            .Include(h => h.JockeyInvitations).ThenInclude(i => i.Jockey).ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries).ThenInclude(e => e.Jockey).ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries).ThenInclude(e => e.Race)
            .ToListAsync();

        var result = horses.Select(h =>
        {
            // Kỵ sĩ hiện tại: lời mời mới nhất đang Pending/Accepted, fallback kỵ sĩ của cuộc đua gần nhất
            var activeInv = h.JockeyInvitations
                .Where(i => i.Status == Models.JockeyInvitationStatus.Accepted || i.Status == Models.JockeyInvitationStatus.Pending)
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
                h.DateOfBirth,
                h.Age,
                h.Weight,
                h.Height,
                h.Color,
                h.TotalRaces,
                h.TotalWins,
                h.ImageUrl,
                h.OwnerId,
                h.ApprovalStatus,
                h.ApprovalNote,
                h.IsArchived,
                h.Owner,
                h.RaceEntries,
                h.JockeyInvitations,
                AssignedJockeyId = jockey?.Id,
                AssignedJockeyName = jockey?.User?.FullName
            };
        }).ToList();

        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetHorse(Guid id)
    {
        var ownerId = GetUserId();
        var result = await _horseService.GetHorseAsync(ownerId, id);

        if (result.StatusCode == 200 && result.Result?.Data is Horse horse)
        {
            return Ok(ApiResult<object>.Ok(MapHorseDto(horse)));
        }

        return StatusCode(result.StatusCode, result.Result);
    }

    private static object MapHorseDto(Horse h)
    {
        return new
        {
            h.Id,
            h.Name,
            h.Breed,
            h.Gender,
            h.DateOfBirth,
            h.Age,
            h.Weight,
            h.Height,
            h.Color,
            h.TotalRaces,
            h.TotalWins,
            h.ImageUrl,
            h.OwnerId,
            h.ApprovalStatus,
            h.ApprovalNote,
            h.IsArchived,
            Owner = h.Owner != null ? new
            {
                h.Owner.Id,
                User = h.Owner.User != null ? new { h.Owner.User.FullName } : null
            } : null,
            RaceEntries = h.RaceEntries?.Select(e => new
            {
                e.Id,
                e.RaceId,
                e.JockeyId,
                e.Status,
                e.OwnerConfirmed,
                e.JockeyConfirmed,
                Race = e.Race != null ? new
                {
                    e.Race.Id,
                    e.Race.Name,
                    e.Race.ScheduledAt,
                    e.Race.Status,
                    Tournament = e.Race.Tournament != null ? new
                    {
                        e.Race.Tournament.Id,
                        e.Race.Tournament.Name,
                        e.Race.Tournament.Status
                    } : null
                } : null
            }),
            JockeyInvitations = h.JockeyInvitations?.Select(i => new
            {
                i.Id,
                i.RaceId,
                i.JockeyId,
                i.Status,
                i.Message,
                Jockey = i.Jockey != null ? new
                {
                    i.Jockey.Id,
                    User = i.Jockey.User != null ? new
                    {
                        i.Jockey.User.FullName,
                        i.Jockey.User.Email
                    } : null
                } : null
            })
        };
    }

    // Task B Final Correction: Horse Create/Update/Delete is Owner-only business territory —
    // Jockey must not create/manage Horses (full Jockey invitation/license flow is a separate,
    // later feature). Admin's existing access here is preserved unchanged, not newly granted.
    // Task C1 correction: Create still resolves an Owner profile from the caller — a Horse always
    // needs an OwnerId and the request has no admin-target field, so an Admin caller without an
    // Owner row still gets the same 404 here as before (unchanged; see report — inventing an
    // OwnerId selector would be new privilege, out of scope for this correction pass).
    [HttpPost]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> CreateHorse(HorseCreateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.CreateHorseAsync(ownerId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> UpdateHorse(Guid id, HorseUpdateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.UpdateHorseAsync(ownerId, id, request, isAdmin: User.IsInRole("Admin"));
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> DeleteHorse(Guid id)
    {
        var ownerId = GetUserId();
        var result = await _horseService.DeleteHorseAsync(ownerId, id, isAdmin: User.IsInRole("Admin"));
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("{horseId:guid}/jockey-invitations")]
    public async Task<ActionResult> InviteJockey(Guid horseId, JockeyInvitationCreateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.InviteJockeyAsync(ownerId, horseId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpDelete("{horseId:guid}/races/{raceId:guid}/jockeys")]
    public async Task<ActionResult> RemoveJockey(Guid horseId, Guid raceId, [FromBody] JockeyRemovalRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.RemoveJockeyAsync(ownerId, horseId, raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    // Legacy Race-level self-registration (item 4 audit: still reachable, still RaceStatus.RegistrationOpen-gated
    // for lifecycle compatibility) — Owner-only for the same reason as Create/Update/Delete above.
    [HttpPost("{horseId:guid}/races/{raceId:guid}/registrations")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> RegisterHorse(Guid horseId, Guid raceId, RaceRegistrationRequest request)
    {
        var ownerId = GetUserId();
        var result = await _raceEntryService.RegisterAsync(ownerId, horseId, raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpPost("upload-image")]
    public async Task<ActionResult> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Không có file nào được tải lên" });

        try
        {
            var url = await _cloudStorage.UploadAsync(file, "horses");
            if (!string.IsNullOrWhiteSpace(url))
            {
                return Ok(new { url });
            }
        }
        catch
        {
            // Ignore cloud upload errors and fallback to local storage
        }

        try
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "horses");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = Path.GetRandomFileName() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            var relativeUrl = $"/uploads/horses/{Uri.EscapeDataString(fileName)}";
            return Ok(new { url = relativeUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("races/{raceId:guid}/entries/{entryId:guid}/owner-confirm")]
    public async Task<ActionResult> ConfirmOwner(Guid raceId, Guid entryId)
    {
        var ownerId = GetUserId();
        var result = await _horseService.ConfirmOwnerAsync(ownerId, raceId, entryId);
        return StatusCode(result.StatusCode, result.Result);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return value == null ? Guid.Empty : Guid.Parse(value);
    }
}
