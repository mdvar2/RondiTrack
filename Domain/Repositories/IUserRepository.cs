using RondiTrack.Domain.Entities;

namespace RondiTrack.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail);

    Task<IEnumerable<User>> GetAllAsync();

    Task<User?> GetByIdAsync(Guid id);

    Task<User?> GetByIdReadOnlyAsync(Guid id);

    Task AddAsync(User user);

    Task UpdateAsync(User user);

    Task<bool> DeleteAsync(Guid id);
}