using System.Threading.Tasks;
using HorseRacing.Data;
using HorseRacing.Repositories.Interfaces;

namespace HorseRacing.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;

    public UnitOfWork(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}
