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

    /// <summary>
    /// Lấy danh sách tất cả lượt tham gia trận đua (RaceEntries) của các ngựa thuộc sở hữu của Chủ ngựa đang đăng nhập.
    /// </summary>
    /// <returns>Danh sách các lượt đua kèm thông tin ngựa, kỵ sĩ và trạng thái lời mời.</returns>
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
            FinishPosition = e.FinishPosition,
            AcceptedInvitations = (h.JockeyInvitations ?? new List<JockeyInvitation>())
                .Where(i => i.RaceId == e.RaceId && i.Status == JockeyInvitationStatus.Accepted)
                .Select(i => new
                {
                    InvitationId = i.Id,
                    JockeyId = i.JockeyId,
                    JockeyName = i.Jockey?.User?.FullName ?? string.Empty
                })
        }));
        return Ok(entries);
    }

    /// <summary>
    /// Lấy danh sách tất cả các con ngựa thuộc sở hữu của Chủ ngựa đang đăng nhập.
    /// </summary>
    /// <returns>Danh sách thông tin các con ngựa của chủ sở hữu.</returns>
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

    /// <summary>
    /// Lấy toàn bộ danh sách tất cả các con ngựa trong hệ thống (bao gồm cả các ngựa Đang chờ duyệt) dành cho Admin.
    /// </summary>
    /// <returns>Danh sách toàn bộ các con ngựa trong hệ thống.</returns>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetAllHorses()
    {
        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        var horses = await db.Horses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(h => h.Owner).ThenInclude(o => o!.User)
            .Include(h => h.JockeyInvitations).ThenInclude(i => i.Jockey).ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries).ThenInclude(e => e.Jockey).ThenInclude(j => j!.User)
            .Include(h => h.RaceEntries).ThenInclude(e => e.Race)
            .ToListAsync();

        var result = horses.Select(h =>
        {
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
                Owner = h.Owner == null ? null : new
                {
                    h.Owner.Id,
                    h.Owner.UserId,
                    User = h.Owner.User == null ? null : new
                    {
                        h.Owner.User.FullName,
                        h.Owner.User.Email
                    }
                },
                RaceEntries = h.RaceEntries.Select(e => new
                {
                    e.Id,
                    e.RaceId,
                    e.JockeyId,
                    e.GateNumber,
                    e.FinishPosition,
                    e.Status,
                    e.OwnerConfirmed,
                    e.JockeyConfirmed,
                    Race = e.Race == null ? null : new { e.Race.Id, e.Race.Name, e.Race.ScheduledAt, e.Race.Status },
                    Jockey = e.Jockey == null ? null : new
                    {
                        e.Jockey.Id,
                        User = e.Jockey.User == null ? null : new { e.Jockey.User.FullName }
                    }
                }),
                JockeyInvitations = h.JockeyInvitations.Select(i => new
                {
                    i.Id,
                    i.RaceId,
                    i.Status,
                    i.CreatedAt,
                    Jockey = i.Jockey == null ? null : new
                    {
                        i.Jockey.Id,
                        User = i.Jockey.User == null ? null : new { i.Jockey.User.FullName }
                    }
                }),
                AssignedJockeyId = jockey?.Id,
                AssignedJockeyName = jockey?.User?.FullName
            };
        }).ToList();

        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một con ngựa theo mã GUID định danh.
    /// </summary>
    /// <param name="id">Mã GUID định danh của con ngựa.</param>
    /// <returns>Thông tin chi tiết con ngựa.</returns>
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
                e.GateNumber,
                e.FinishPosition,
                e.Status,
                e.OwnerConfirmed,
                e.JockeyConfirmed,
                Jockey = e.Jockey != null ? new
                {
                    e.Jockey.Id,
                    User = e.Jockey.User != null ? new { e.Jockey.User.FullName } : null
                } : null,
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

    /// <summary>
    /// Đăng ký thêm một con ngựa đua mới vào hệ thống (cần Admin duyệt trước khi tham gia thi đấu).
    /// </summary>
    /// <param name="request">Thông tin mô tả con ngựa (tên, giống, tuổi, cân nặng, chiều cao, màu lông).</param>
    /// <returns>Mã trạng thái HTTP và kết quả tạo mới con ngựa.</returns>
    [HttpPost]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> CreateHorse(HorseCreateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.CreateHorseAsync(ownerId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Cập nhật chỉ số và thông tin cá nhân của một con ngựa đua hiện có.
    /// </summary>
    /// <param name="id">Mã GUID con ngựa cần chỉnh sửa.</param>
    /// <param name="request">Thông tin chỉnh sửa con ngựa.</param>
    /// <returns>Dữ liệu con ngựa sau khi được chỉnh sửa.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> UpdateHorse(Guid id, HorseUpdateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.UpdateHorseAsync(ownerId, id, request, isAdmin: User.IsInRole("Admin"));
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Lưu trữ hoặc xóa thông tin một con ngựa khỏi danh sách thi đấu.
    /// </summary>
    /// <param name="id">Mã GUID con ngựa cần xóa.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện xóa.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> DeleteHorse(Guid id)
    {
        var ownerId = GetUserId();
        var result = await _horseService.DeleteHorseAsync(ownerId, id, isAdmin: User.IsInRole("Admin"));
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Gửi lời mời kỵ sĩ (Jockey) tham gia điều khiển con ngựa trong một trận đua cụ thể.
    /// </summary>
    /// <param name="horseId">Mã GUID con ngựa thi đấu.</param>
    /// <param name="request">Thông tin lời mời kỵ sĩ (JockeyId, RaceId, lời nhắn).</param>
    /// <returns>Mã trạng thái HTTP và thông tin lời mời đã gửi.</returns>
    [HttpPost("{horseId:guid}/jockey-invitations")]
    public async Task<ActionResult> InviteJockey(Guid horseId, JockeyInvitationCreateRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.InviteJockeyAsync(ownerId, horseId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Hủy phân công hoặc gỡ bỏ kỵ sĩ khỏi lượt đua của con ngựa.
    /// </summary>
    /// <param name="horseId">Mã GUID con ngựa.</param>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="request">Yêu cầu hủy kỵ sĩ.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả thực hiện.</returns>
    [HttpDelete("{horseId:guid}/races/{raceId:guid}/jockeys")]
    public async Task<ActionResult> RemoveJockey(Guid horseId, Guid raceId, [FromBody] JockeyRemovalRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.RemoveJockeyAsync(ownerId, horseId, raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Đăng ký con ngựa tham gia vào một trận đua công khai đang mở đăng ký.
    /// </summary>
    /// <param name="horseId">Mã GUID con ngựa đăng ký.</param>
    /// <param name="raceId">Mã GUID trận đua mục tiêu.</param>
    /// <param name="request">Dữ liệu đăng ký tham gia trận đua.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả đăng ký.</returns>
    [HttpPost("{horseId:guid}/races/{raceId:guid}/registrations")]
    [Authorize(Roles = "HorseOwner,Admin")]
    public async Task<ActionResult> RegisterHorse(Guid horseId, Guid raceId, RaceRegistrationRequest request)
    {
        var ownerId = GetUserId();
        var result = await _raceEntryService.RegisterAsync(ownerId, horseId, raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Tải lên hình ảnh đại diện cho con ngựa đua lên dịch vụ lưu trữ Cloud / Thư mục cục bộ.
    /// </summary>
    /// <param name="file">File ảnh đại diện con ngựa (PNG, JPG, WEBP).</param>
    /// <returns>Đường dẫn URL hình ảnh sau khi tải lên thành công.</returns>
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

    /// <summary>
    /// Chủ ngựa xác nhận lượt thi đấu chính thức của con ngựa trong trận đua.
    /// </summary>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="entryId">Mã GUID lượt đăng ký thi đấu.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả xác nhận.</returns>
    [HttpPost("races/{raceId:guid}/entries/{entryId:guid}/owner-confirm")]
    public async Task<ActionResult> ConfirmOwner(Guid raceId, Guid entryId)
    {
        var ownerId = GetUserId();
        var result = await _horseService.ConfirmOwnerAsync(ownerId, raceId, entryId);
        return StatusCode(result.StatusCode, result.Result);
    }

    /// <summary>
    /// Chủ ngựa chốt chọn chính thức một kỵ sĩ (Jockey) đã đồng ý lời mời để điều khiển ngựa trong trận đua.
    /// </summary>
    /// <param name="horseId">Mã GUID con ngựa.</param>
    /// <param name="raceId">Mã GUID trận đua.</param>
    /// <param name="request">Yêu cầu chốt kỵ sĩ chính thức.</param>
    /// <returns>Mã trạng thái HTTP báo kết quả chốt kỵ sĩ.</returns>
    [HttpPost("{horseId:guid}/races/{raceId:guid}/jockeys/final-confirm")]
    public async Task<ActionResult> FinalConfirmJockey(Guid horseId, Guid raceId, OwnerFinalConfirmJockeyRequest request)
    {
        var ownerId = GetUserId();
        var result = await _horseService.FinalConfirmJockeyAsync(ownerId, horseId, raceId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return value == null ? Guid.Empty : Guid.Parse(value);
    }
}
