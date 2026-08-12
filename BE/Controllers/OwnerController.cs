using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/owner")]
[Authorize(Roles = "HorseOwner,Jockey")]
public class OwnerController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public OwnerController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("performance")]
    public async Task<ActionResult> GetPerformance()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        // Get entries for this owner's horses
        var entries = await _db.RaceEntries
            .Include(e => e.Race)
            .Include(e => e.Horse)
            .Where(e => e.Horse != null && e.Horse.OwnerId != null && e.Horse.Owner.UserId == uid)
            .ToListAsync();

        var finished = entries.Where(e => e.FinishPosition != null).ToList();
        var wins = finished.Count(e => e.FinishPosition == 1);
        var total = entries.Count;

        // Weekly performance (last 4 weeks - using race date)
        var weeklyData = Enumerable.Range(0, 4).Select(weekOffset =>
        {
            var end = DateTime.UtcNow.AddDays(-(weekOffset * 7));
            var start = DateTime.UtcNow.AddDays(-(weekOffset * 7 + 7));
            var weekEntries = finished.Where(e => e.Race != null && e.Race.ScheduledAt >= start && e.Race.ScheduledAt < end);
            return new
            {
                week = $"W{DateTime.UtcNow.AddDays(-weekOffset * 7):MM/dd}",
                races = weekEntries.Count(),
                wins = weekEntries.Count(e => e.FinishPosition == 1)
            };
        }).Reverse().ToList();

        // Horse stats
        var horseStats = entries.Where(e => e.Horse != null).GroupBy(e => e.HorseId)
            .Select(g =>
            {
                var horse = g.First().Horse;
                var horseEntries = g.ToList();
                var horseWins = horseEntries.Count(e => e.FinishPosition == 1);
                return new
                {
                    horseId = g.Key,
                    horseName = horse?.Name ?? "Không xác định",
                    totalRaces = horseEntries.Count,
                    wins = horseWins,
                    winRate = horseEntries.Count > 0 ? Math.Round((double)horseWins / horseEntries.Count * 100) : 0
                };
            })
            .OrderByDescending(h => h.winRate)
            .ToList();

        return Ok(new
        {
            total,
            finished = finished.Count,
            wins,
            winRate = total > 0 ? Math.Round((double)wins / total * 100) : 0,
            weeklyPerformance = weeklyData,
            horseStats
        });
    }

    [HttpGet("upcoming")]
    public async Task<ActionResult> GetUpcoming()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var upcoming = await _db.RaceEntries
            .Include(e => e.Race)
            .Include(e => e.Horse)
            .Where(e => e.Horse != null && e.Horse.Owner.UserId == uid && e.FinishPosition == null)
            .OrderBy(e => e.Race != null ? e.Race.ScheduledAt : DateTime.MaxValue)
            .Take(10)
            .Select(e => new
            {
                entryId = e.Id,
                raceName = e.Race != null ? e.Race.Name : null,
                raceDate = e.Race != null ? e.Race.ScheduledAt : (DateTime?)null,
                horseName = e.Horse != null ? e.Horse.Name : null,
                status = e.Status.ToString(),
                ownerConfirmed = e.OwnerConfirmed,
                gateNumber = e.GateNumber,
                location = e.Race != null ? e.Race.Location : null
            })
            .ToListAsync();

        return Ok(upcoming);
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var uid)) return uid;
        return null;
    }
}
