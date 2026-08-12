using System;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface IOwnerRepository
{
    Task<bool> ExistsAsync(Guid ownerId);
    Task<Owner?> GetByIdAsync(Guid ownerId);
    Task<Owner?> GetByUserIdAsync(Guid userId);
    Task AddAsync(Owner owner);
    Task UpdateAsync(Owner owner);
}
