using HorseRacing.Models;
using System;
using System.Threading.Tasks;

namespace HorseRacing.Repositories.Interfaces
{
    public interface IViolationRepository
    {
        Task<ViolationRecord> GetByIdAsync(Guid id);
        Task UpdateAsync(ViolationRecord violation);
    }
}