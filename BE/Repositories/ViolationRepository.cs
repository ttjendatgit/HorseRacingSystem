using System;
using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;

namespace HorseRacing.Repositories
{
    public class ViolationRepository : IViolationRepository
    {
        private readonly ApplicationDbContext _context;

        public ViolationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ViolationRecord> GetByIdAsync(Guid id)
        {
            return await _context.Set<ViolationRecord>().FindAsync(id);
        }

        public async Task UpdateAsync(ViolationRecord violation)
        {
            _context.Set<ViolationRecord>().Update(violation);
            await Task.CompletedTask;
        }
    }
}