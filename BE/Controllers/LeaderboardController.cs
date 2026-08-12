using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/leaderboard")]
public class LeaderboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LeaderboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("jockeys")]
    public async Task<ActionResult> GetJockeyRankings()
    {
        var jockeys = await _db.Jockeys
            .Include(j => j.User)
            .Where(j => j.User != null && j.Status == "Đang hoạt động")
            .Select(j => new
            {
                id = j.Id,
                name = j.User!.FullName ?? j.User.Email,
                avatar = j.User.Email ?? "",
                points = j.TotalWins * 10 + j.TotalRaces * 2,
                totalRaces = j.TotalRaces,
                wins = j.TotalWins,
                winRate = j.TotalRaces > 0 ? Math.Round((double)j.TotalWins / j.TotalRaces * 100) : 0,
                rank = j.Rank ?? 0,
                trend = "up",
                nationality = j.Nationality ?? ""
            })
            .OrderByDescending(j => j.points)
            .ToListAsync();

        return Ok(jockeys);
    }

    [HttpGet("horses")]
    public async Task<ActionResult> GetHorseRankings()
    {
        var horses = await _db.Horses
            .Where(h => h.TotalRaces > 0)
            .Select(h => new
            {
                id = h.Id,
                name = h.Name ?? "",
                points = h.TotalWins * 10 + h.TotalRaces * 2,
                totalRaces = h.TotalRaces,
                wins = h.TotalWins,
                winRate = h.TotalRaces > 0 ? Math.Round((double)h.TotalWins / h.TotalRaces * 100) : 0,
                breed = h.Breed ?? "",
                gender = h.Gender ?? "",
                age = h.Age,
                trend = h.TotalWins > h.TotalRaces / 3 ? "up" : "down",
                ownerName = h.Owner != null ? h.Owner.User != null ? h.Owner.User.FullName ?? "" : "" : ""
            })
            .OrderByDescending(h => h.points)
            .ToListAsync();

        return Ok(horses);
    }
}
