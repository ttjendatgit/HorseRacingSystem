using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface ITournamentRepository
{
    Task<Tournament?> GetByIdAsync(Guid id);
    Task<List<Tournament>> GetAllWithRacesAsync();
    Task<IEnumerable<Tournament>> GetAllAsync();
    Task<IEnumerable<Tournament>> GetActiveAsync();
    Task AddAsync(Tournament tournament);
    Task UpdateAsync(Tournament tournament);
    Task DeleteAsync(Guid id);
}
