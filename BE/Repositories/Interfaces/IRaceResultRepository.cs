using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IRaceResultRepository
{
    Task<RaceResult?> GetByRaceIdAsync(Guid raceId);
    Task AddAsync(RaceResult raceResult);
    Task UpdateAsync(RaceResult raceResult);
}
