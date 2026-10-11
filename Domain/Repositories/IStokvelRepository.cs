using RondiTrack.Domain.Entities;

namespace RondiTrack.Domain.Repositories;

public interface IStokvelRepository
{
    Task<IEnumerable<Stokvel>> GetAllAsync();

    Task<Stokvel?> GetByIdAsync(Guid id);

    Task<Stokvel?> GetByIdReadOnlyAsync(Guid id);

    Task AddAsync(Stokvel stokvel);

    Task AddWithChairpersonAsync(Stokvel stokvel, Guid userId);

    Task UpdateAsync(Stokvel stokvel);

    Task<bool> DeleteAsync(Guid id);
}