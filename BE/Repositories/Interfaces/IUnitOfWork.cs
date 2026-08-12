using System.Threading.Tasks;

namespace HorseRacing.Repositories.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
}
