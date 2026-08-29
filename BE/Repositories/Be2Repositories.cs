using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;
using HorseRacing.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Repositories;

public class TournamentRepository : ITournamentRepository
{
    private readonly ApplicationDbContext _context;

    public TournamentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Tournament?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Tournament>()
            .Include(t => t.Rounds)
            .Include(t => t.Races)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Tournament>> GetAllAsync()
    {
        return await _context.Set<Tournament>()
            .Include(t => t.Rounds)
            .Include(t => t.Races)
            .ToListAsync();
    }

    public async Task<List<Tournament>> GetAllWithRacesAsync()
    {
        return await _context.Set<Tournament>()
            .Include(t => t.Races)
            .ToListAsync();
    }

    public async Task<Tournament?> GetWithFullRoundsAndResultsAsync(Guid id)
    {
        return await _context.Set<Tournament>()
            .Include(t => t.Rounds)
                .ThenInclude(r => r.Races)
                    .ThenInclude(r => r.Result)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Tournament>> GetActiveAsync()
    {
        return await _context.Set<Tournament>()
            .Where(t => t.IsActive)
            .Include(t => t.Rounds)
            .Include(t => t.Races)
            .ToListAsync();
    }

    public async Task AddAsync(Tournament tournament)
    {
        await _context.Set<Tournament>().AddAsync(tournament);
    }

    public async Task UpdateAsync(Tournament tournament)
    {
        tournament.UpdatedAt = DateTime.UtcNow;
        _context.Set<Tournament>().Update(tournament);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var tournament = await GetByIdAsync(id);
        if (tournament != null)
        {
            _context.Set<Tournament>().Remove(tournament);
        }
        await Task.CompletedTask;
    }
}

public class RoundRepository : IRoundRepository
{
    private readonly ApplicationDbContext _context;

    public RoundRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Round?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Round>()
            .Include(r => r.Tournament)
            .Include(r => r.Races)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Round>> GetByTournamentAsync(Guid tournamentId)
    {
        return await _context.Set<Round>()
            .Where(r => r.TournamentId == tournamentId)
            .Include(r => r.Races)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<Round>> GetAllAsync()
    {
        return await _context.Set<Round>()
            .Include(r => r.Tournament)
            .Include(r => r.Races)
            .OrderBy(r => r.ScheduledStartDate)
            .ToListAsync();
    }

    public async Task AddAsync(Round round)
    {
        await _context.Set<Round>().AddAsync(round);
    }

    public async Task UpdateAsync(Round round)
    {
        _context.Set<Round>().Update(round);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var round = await GetByIdAsync(id);
        if (round != null)
        {
            _context.Set<Round>().Remove(round);
        }
        await Task.CompletedTask;
    }
}

public class RefereeRepository : IRefereeRepository
{
    private readonly ApplicationDbContext _context;

    public RefereeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Referee?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Referee>()
            .Include(r => r.User)
            .Include(r => r.Assignments)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Referee?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Set<Referee>()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId);
    }

    public async Task<IEnumerable<Referee>> GetAllAsync()
    {
        return await _context.Set<Referee>()
            .Include(r => r.User)
            .Include(r => r.Assignments)
            .ToListAsync();
    }

    public async Task<IEnumerable<Referee>> GetActiveAsync()
    {
        return await _context.Set<Referee>()
            .Where(r => r.IsActive && r.LicenseExpiryDate > DateTime.UtcNow)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<Referee>> GetByLicenseExpiryAsync(DateTime expiryDate)
    {
        return await _context.Set<Referee>()
            .Where(r => r.LicenseExpiryDate <= expiryDate)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task AddAsync(Referee referee)
    {
        await _context.Set<Referee>().AddAsync(referee);
    }

    public async Task UpdateAsync(Referee referee)
    {
        referee.UpdatedAt = DateTime.UtcNow;
        _context.Set<Referee>().Update(referee);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var referee = await GetByIdAsync(id);
        if (referee != null)
        {
            _context.Set<Referee>().Remove(referee);
        }
        await Task.CompletedTask;
    }
}

public class RefereeAssignmentRepository : IRefereeAssignmentRepository
{
    private readonly ApplicationDbContext _context;

    public RefereeAssignmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RefereeAssignment?> GetByIdAsync(Guid id)
    {
        return await _context.Set<RefereeAssignment>()
            .Include(a => a.Race)
            .Include(a => a.Referee)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<RefereeAssignment>> GetByRaceAsync(Guid raceId)
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.RaceId == raceId)
            .Include(a => a.Race)
            .Include(a => a.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefereeAssignment>> GetByRefereeAsync(Guid refereeId)
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.RefereeId == refereeId)
            .Include(a => a.Race)!.ThenInclude(r => r!.Tournament)
            .Include(a => a.Race)!.ThenInclude(r => r!.Round)
            .Include(a => a.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefereeAssignment>> GetAllAsync()
    {
        return await _context.Set<RefereeAssignment>()
            .Include(a => a.Race)!.ThenInclude(r => r!.Tournament)
            .Include(a => a.Race)!.ThenInclude(r => r!.Round)
            .Include(a => a.Referee)!.ThenInclude(r => r!.User)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefereeAssignment>> GetPendingAsync()
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.Status == RefereeAssignmentStatus.Assigned)
            .Include(a => a.Race)
            .Include(a => a.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefereeAssignment>> GetByStatusAsync(string status)
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.Status.ToString() == status)
            .Include(a => a.Race)
            .Include(a => a.Referee)
            .ToListAsync();
    }

    // A Confirmed assignment in any Ongoing tournament locks the referee out of new admin
    // assignment pickers until that tournament leaves Ongoing (Finished/Cancelled).
    public async Task<List<Guid>> GetRefereeIdsConfirmedInOngoingTournamentsAsync()
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.Status == RefereeAssignmentStatus.Confirmed
                     && a.Race != null
                     && a.Race.Tournament != null
                     && a.Race.Tournament.Status == TournamentStatus.Ongoing)
            .Select(a => a.RefereeId)
            .Distinct()
            .ToListAsync();
    }

    public async Task AddAsync(RefereeAssignment assignment)
    {
        await _context.Set<RefereeAssignment>().AddAsync(assignment);
    }

    public async Task UpdateAsync(RefereeAssignment assignment)
    {
        _context.Set<RefereeAssignment>().Update(assignment);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var assignment = await GetByIdAsync(id);
        if (assignment != null)
        {
            _context.Set<RefereeAssignment>().Remove(assignment);
        }
        await Task.CompletedTask;
    }
}

public class HealthCheckRepository : IHealthCheckRepository
{
    private readonly ApplicationDbContext _context;

    public HealthCheckRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HorseHealthCheck?> GetByIdAsync(Guid id)
    {
        return await _context.Set<HorseHealthCheck>()
            .Include(h => h.Horse)
            .Include(h => h.Race)
            .Include(h => h.Referee)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<IEnumerable<HorseHealthCheck>> GetByRaceAsync(Guid raceId)
    {
        return await _context.Set<HorseHealthCheck>()
            .Where(h => h.RaceId == raceId)
            .Include(h => h.Horse)
            .Include(h => h.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<HorseHealthCheck>> GetByHorseAsync(Guid horseId)
    {
        return await _context.Set<HorseHealthCheck>()
            .Where(h => h.HorseId == horseId)
            .Include(h => h.Race)
            .Include(h => h.Referee)
            .OrderByDescending(h => h.CheckedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<HorseHealthCheck>> GetByRefereeAsync(Guid refereeId)
    {
        return await _context.Set<HorseHealthCheck>()
            .Where(h => h.RefereeId == refereeId)
            .Include(h => h.Horse)
            .Include(h => h.Race)
            .ToListAsync();
    }

    public async Task<HorseHealthCheck?> GetLatestByHorseAndRaceAsync(Guid horseId, Guid raceId)
    {
        return await _context.Set<HorseHealthCheck>()
            .Where(h => h.HorseId == horseId && h.RaceId == raceId)
            .Include(h => h.Referee)
            .OrderByDescending(h => h.CheckedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(HorseHealthCheck healthCheck)
    {
        await _context.Set<HorseHealthCheck>().AddAsync(healthCheck);
    }

    public async Task UpdateAsync(HorseHealthCheck healthCheck)
    {
        _context.Set<HorseHealthCheck>().Update(healthCheck);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var healthCheck = await GetByIdAsync(id);
        if (healthCheck != null)
        {
            _context.Set<HorseHealthCheck>().Remove(healthCheck);
        }
        await Task.CompletedTask;
    }
}

public class ViolationRecordRepository : IViolationRecordRepository
{
    private readonly ApplicationDbContext _context;

    public ViolationRecordRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ViolationRecord?> GetByIdAsync(Guid id)
    {
        return await _context.Set<ViolationRecord>()
            .Include(v => v.Race)
            .Include(v => v.RaceEntry)
            .Include(v => v.Referee)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<IEnumerable<ViolationRecord>> GetByRaceAsync(Guid raceId)
    {
        return await _context.Set<ViolationRecord>()
            .Where(v => v.RaceId == raceId)
            .Include(v => v.RaceEntry)
            .Include(v => v.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<ViolationRecord>> GetByRaceEntryAsync(Guid raceEntryId)
    {
        return await _context.Set<ViolationRecord>()
            .Where(v => v.RaceEntryId == raceEntryId)
            .Include(v => v.Race)
            .Include(v => v.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<ViolationRecord>> GetByRefereeAsync(Guid refereeId)
    {
        return await _context.Set<ViolationRecord>()
            .Where(v => v.RefereeId == refereeId)
            .Include(v => v.Race)
            .Include(v => v.RaceEntry)
            .ToListAsync();
    }

    public async Task<int> GetTotalByHorseAsync(Guid horseId)
    {
        return await _context.Set<ViolationRecord>()
            .Where(v => v.RaceEntry!.HorseId == horseId)
            .CountAsync();
    }

    public async Task AddAsync(ViolationRecord violation)
    {
        await _context.Set<ViolationRecord>().AddAsync(violation);
    }

    public async Task UpdateAsync(ViolationRecord violation)
    {
        _context.Set<ViolationRecord>().Update(violation);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var violation = await GetByIdAsync(id);
        if (violation != null)
        {
            _context.Set<ViolationRecord>().Remove(violation);
        }
        await Task.CompletedTask;
    }
}

public class RaceReportRepository : IRaceReportRepository
{
    private readonly ApplicationDbContext _context;

    public RaceReportRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RaceReport?> GetByIdAsync(Guid id)
    {
        return await _context.Set<RaceReport>()
            .Include(r => r.Race)
            .Include(r => r.Referee)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<RaceReport?> GetByRaceAsync(Guid raceId)
    {
        return await _context.Set<RaceReport>()
            .Where(r => r.RaceId == raceId)
            .Include(r => r.Referee)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<RaceReport>> GetByRefereeAsync(Guid refereeId)
    {
        return await _context.Set<RaceReport>()
            .Where(r => r.RefereeId == refereeId)
            .Include(r => r.Race)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RaceReport>> GetAllAsync()
    {
        return await _context.Set<RaceReport>()
            .Include(r => r.Race)
            .Include(r => r.Referee)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(RaceReport report)
    {
        await _context.Set<RaceReport>().AddAsync(report);
    }

    public async Task UpdateAsync(RaceReport report)
    {
        report.UpdatedAt = DateTime.UtcNow;
        _context.Set<RaceReport>().Update(report);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var report = await GetByIdAsync(id);
        if (report != null)
        {
            _context.Set<RaceReport>().Remove(report);
        }
        await Task.CompletedTask;
    }
}

public class UserRegistrationRepository : IUserRegistrationRepository
{
    private readonly ApplicationDbContext _context;

    public UserRegistrationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserRegistration?> GetByIdAsync(Guid id)
    {
        return await _context.Set<UserRegistration>()
            .Include(u => u.ReviewedByUser)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UserRegistration?> GetByEmailAsync(string email)
    {
        return await _context.Set<UserRegistration>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<UserRegistration>> GetByStatusAsync(string status)
    {
        return await _context.Set<UserRegistration>()
            .Where(u => u.Status.ToString() == status)
            .Include(u => u.ReviewedByUser)
            .OrderByDescending(u => u.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRegistration>> GetPendingAsync()
    {
        return await _context.Set<UserRegistration>()
            .Where(u => u.Status == RegistrationStatus.Pending)
            .OrderByDescending(u => u.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRegistration>> GetAllAsync()
    {
        return await _context.Set<UserRegistration>()
            .Include(u => u.ReviewedByUser)
            .OrderByDescending(u => u.SubmittedAt)
            .ToListAsync();
    }

    public async Task AddAsync(UserRegistration registration)
    {
        await _context.Set<UserRegistration>().AddAsync(registration);
    }

    public async Task UpdateAsync(UserRegistration registration)
    {
        _context.Set<UserRegistration>().Update(registration);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var registration = await GetByIdAsync(id);
        if (registration != null)
        {
            _context.Set<UserRegistration>().Remove(registration);
        }
        await Task.CompletedTask;
    }
}

public class RaceManagementRepository : IRaceManagementRepository
{
    private readonly ApplicationDbContext _context;

    public RaceManagementRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RaceEntry>> GetRaceEntriesAsync(Guid raceId)
    {
        return await _context.Set<RaceEntry>()
            .Where(e => e.RaceId == raceId)
            .Include(e => e.Horse)
            .Include(e => e.Jockey)
            .ToListAsync();
    }

    public async Task<RaceResult?> GetRaceResultAsync(Guid raceId)
    {
        return await _context.Set<RaceResult>()
            .Where(r => r.RaceId == raceId)
            .Include(r => r.WinningHorse)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetRaceParticipantCountAsync(Guid raceId)
    {
        return await _context.Set<RaceEntry>()
            .Where(e => e.RaceId == raceId)
            .CountAsync();
    }

    public async Task<IEnumerable<HorseHealthCheck>> GetHealthChecksByRaceAsync(Guid raceId)
    {
        return await _context.Set<HorseHealthCheck>()
            .Where(h => h.RaceId == raceId)
            .Include(h => h.Horse)
            .Include(h => h.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<ViolationRecord>> GetViolationsByRaceAsync(Guid raceId)
    {
        return await _context.Set<ViolationRecord>()
            .Where(v => v.RaceId == raceId)
            .Include(v => v.RaceEntry)
            .Include(v => v.Referee)
            .ToListAsync();
    }

    public async Task<IEnumerable<RefereeAssignment>> GetAssignedRefereesAsync(Guid raceId)
    {
        return await _context.Set<RefereeAssignment>()
            .Where(a => a.RaceId == raceId)
            .Include(a => a.Referee)
            .ToListAsync();
    }
}
