using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IHorseRepository
{
    Task<Horse?> GetByIdAsync(Guid horseId);
    Task<List<Horse>> GetByOwnerAsync(Guid ownerId);
    Task<Horse?> GetOwnedHorseAsync(Guid horseId, Guid ownerId);
    Task<List<Horse>> GetAllApprovedAsync();
    Task AddAsync(Horse horse);
    Task UpdateAsync(Horse horse);
    Task RemoveAsync(Horse horse);
}
