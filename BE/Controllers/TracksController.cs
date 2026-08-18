using System;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Dtos;
using HorseRacing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/tracks")]
public class TracksController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TracksController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetAll()
    {
        var tracks = await _db.Tracks.OrderBy(t => t.Name).ToListAsync();
        return Ok(new
        {
            success = true,
            data = tracks.Select(t => new TrackResponse
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Length = t.Length,
                Capacity = t.Capacity,
                CreatedAt = t.CreatedAt
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Create([FromBody] CreateTrackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { success = false, message = "Tên đường đua không được để trống." });

        var track = new Track
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Length = request.Length,
            Capacity = request.Capacity,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tracks.Add(track);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new TrackResponse
            {
                Id = track.Id,
                Name = track.Name,
                Description = track.Description,
                Length = track.Length,
                Capacity = track.Capacity,
                CreatedAt = track.CreatedAt
            }
        });
    }
}
