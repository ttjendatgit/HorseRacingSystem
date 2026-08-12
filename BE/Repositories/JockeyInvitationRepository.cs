using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class JockeyInvitationRepository : IJockeyInvitationRepository
{
    private readonly ApplicationDbContext _db;

    public JockeyInvitationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<JockeyInvitation>> GetByJockeyAsync(Guid jockeyId)
    {
        return _db.JockeyInvitations
            .Include(i => i.Horse)
                .ThenInclude(h => h!.Owner)
                .ThenInclude(o => o!.User)
            .Include(i => i.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(i => i.Race)
                .ThenInclude(r => r!.Track)
            .Where(i =>
                i.JockeyId == jockeyId &&
                (i.Status == JockeyInvitationStatus.Pending ||
                 i.Status == JockeyInvitationStatus.Accepted ||
                 i.Status == JockeyInvitationStatus.Declined ||
                 i.Status == JockeyInvitationStatus.Withdrawn))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public Task<JockeyInvitation?> GetByIdAsync(Guid invitationId, Guid jockeyId)
    {
        return _db.JockeyInvitations
            .Include(i => i.Horse)
                .ThenInclude(h => h!.Owner)
                .ThenInclude(o => o!.User)
            .Include(i => i.Jockey)
                .ThenInclude(j => j!.User)
            .Include(i => i.Race)
                .ThenInclude(r => r!.Tournament)
            .Include(i => i.Race)
                .ThenInclude(r => r!.Track)
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.JockeyId == jockeyId);
    }

    public Task<JockeyInvitation?> GetActiveByHorseAsync(Guid horseId)
    {
        return _db.JockeyInvitations
            .Include(i => i.Jockey)
                .ThenInclude(j => j!.User)
            .Where(i =>
                i.HorseId == horseId &&
                (i.Status == JockeyInvitationStatus.Pending ||
                 i.Status == JockeyInvitationStatus.Accepted))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<JockeyInvitation?> GetByHorseAndJockeyAsync(Guid horseId, Guid jockeyId)
    {
        return _db.JockeyInvitations
            .Include(i => i.Jockey)
                .ThenInclude(j => j!.User)
            .Where(i => i.HorseId == horseId && i.JockeyId == jockeyId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task AddAsync(JockeyInvitation invitation)
    {
        _db.JockeyInvitations.Add(invitation);
        return Task.CompletedTask;
    }
}
