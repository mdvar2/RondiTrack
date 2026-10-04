using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public UserRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _dbContext.Users
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Id == id);
    }

    public async Task<User?> GetByIdReadOnlyAsync(Guid id)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == id);
    }

    public async Task AddAsync(User user)
    {
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    user => user.Id == id);

        if (user is null)
            return false;

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        return true;
    }
}