using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRoundRepository
{
    Task<Round?> GetByIdAsync(Guid id);
    Task<IEnumerable<Round>> GetByTournamentAsync(Guid tournamentId);
    Task<IEnumerable<Round>> GetAllAsync();
    Task AddAsync(Round round);
    Task UpdateAsync(Round round);
    Task DeleteAsync(Guid id);
}

public interface IRefereeRepository
{
    Task<Referee?> GetByIdAsync(Guid id);
    Task<Referee?> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Referee>> GetAllAsync();
    Task<IEnumerable<Referee>> GetActiveAsync();
    Task<IEnumerable<Referee>> GetByLicenseExpiryAsync(DateTime expiryDate);
    Task AddAsync(Referee referee);
    Task UpdateAsync(Referee referee);
    Task DeleteAsync(Guid id);
}

public interface IRefereeAssignmentRepository
{
    Task<RefereeAssignment?> GetByIdAsync(Guid id);
    Task<IEnumerable<RefereeAssignment>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<RefereeAssignment>> GetByRefereeAsync(Guid refereeId);
    Task<IEnumerable<RefereeAssignment>> GetAllAsync();
    Task<IEnumerable<RefereeAssignment>> GetPendingAsync();
    Task<IEnumerable<RefereeAssignment>> GetByStatusAsync(string status);
    Task<List<Guid>> GetRefereeIdsConfirmedInOngoingTournamentsAsync();
    Task AddAsync(RefereeAssignment assignment);
    Task UpdateAsync(RefereeAssignment assignment);
    Task DeleteAsync(Guid id);
}

public interface IHealthCheckRepository
{
    Task<HorseHealthCheck?> GetByIdAsync(Guid id);
    Task<IEnumerable<HorseHealthCheck>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<HorseHealthCheck>> GetByHorseAsync(Guid horseId);
    Task<IEnumerable<HorseHealthCheck>> GetByRefereeAsync(Guid refereeId);
    Task<HorseHealthCheck?> GetLatestByHorseAndRaceAsync(Guid horseId, Guid raceId);
    Task AddAsync(HorseHealthCheck healthCheck);
    Task UpdateAsync(HorseHealthCheck healthCheck);
    Task DeleteAsync(Guid id);
}

public interface IViolationRecordRepository
{
    Task<ViolationRecord?> GetByIdAsync(Guid id);
    Task<IEnumerable<ViolationRecord>> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<ViolationRecord>> GetByRaceEntryAsync(Guid raceEntryId);
    Task<IEnumerable<ViolationRecord>> GetByRefereeAsync(Guid refereeId);
    Task<int> GetTotalByHorseAsync(Guid horseId);
    Task AddAsync(ViolationRecord violation);
    Task UpdateAsync(ViolationRecord violation);
    Task DeleteAsync(Guid id);
}

public interface IRaceReportRepository
{
    Task<RaceReport?> GetByIdAsync(Guid id);
    Task<RaceReport?> GetByRaceAsync(Guid raceId);
    Task<IEnumerable<RaceReport>> GetByRefereeAsync(Guid refereeId);
    Task<IEnumerable<RaceReport>> GetAllAsync();
    Task AddAsync(RaceReport report);
    Task UpdateAsync(RaceReport report);
    Task DeleteAsync(Guid id);
}

public interface IUserRegistrationRepository
{
    Task<UserRegistration?> GetByIdAsync(Guid id);
    Task<UserRegistration?> GetByEmailAsync(string email);
    Task<IEnumerable<UserRegistration>> GetByStatusAsync(string status);
    Task<IEnumerable<UserRegistration>> GetPendingAsync();
    Task<IEnumerable<UserRegistration>> GetAllAsync();
    Task AddAsync(UserRegistration registration);
    Task UpdateAsync(UserRegistration registration);
    Task DeleteAsync(Guid id);
}

public interface IRaceManagementRepository
{
    Task<IEnumerable<RaceEntry>> GetRaceEntriesAsync(Guid raceId);
    Task<RaceResult?> GetRaceResultAsync(Guid raceId);
    Task<int> GetRaceParticipantCountAsync(Guid raceId);
    Task<IEnumerable<HorseHealthCheck>> GetHealthChecksByRaceAsync(Guid raceId);
    Task<IEnumerable<ViolationRecord>> GetViolationsByRaceAsync(Guid raceId);
    Task<IEnumerable<RefereeAssignment>> GetAssignedRefereesAsync(Guid raceId);
}
