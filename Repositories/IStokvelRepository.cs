using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IStokvelRepository
{
    Task<IEnumerable<Stokvel>> GetAllAsync();

    Task<Stokvel?> GetByIdAsync(Guid id);

    Task<Stokvel?> GetByIdReadOnlyAsync(Guid id);

    Task AddAsync(Stokvel stokvel);

    Task UpdateAsync(Stokvel stokvel);

    Task<bool> DeleteAsync(Guid id);
}