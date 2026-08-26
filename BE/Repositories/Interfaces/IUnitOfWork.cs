using System.Threading.Tasks;

namespace HorseRacing.Repositories.Interfaces;

/// <summary>
/// Quản lý giao tác cơ sở dữ liệu (Unit of Work) để lưu tất cả thay đổi trong cùng một giao tác.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Lưu tất cả các thay đổi của thực thể vào cơ sở dữ liệu.
    /// </summary>
    /// <returns>Số lượng bản ghi bị ảnh hưởng.</returns>
    Task<int> SaveChangesAsync();
}
