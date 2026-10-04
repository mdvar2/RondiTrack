using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class StokvelRepository : IStokvelRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public StokvelRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Stokvel>> GetAllAsync()
    {
        return await _dbContext.Stokvels
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Stokvel?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Stokvels
            .FirstOrDefaultAsync(
                stokvel => stokvel.Id == id);
    }

    public async Task<Stokvel?> GetByIdReadOnlyAsync(Guid id)
    {
        return await _dbContext.Stokvels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                stokvel => stokvel.Id == id);
    }

    public async Task AddAsync(Stokvel stokvel)
    {
        await _dbContext.Stokvels.AddAsync(stokvel);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Stokvel stokvel)
    {
        _dbContext.Stokvels.Update(stokvel);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var stokvel =
            await _dbContext.Stokvels
                .FirstOrDefaultAsync(
                    stokvel => stokvel.Id == id);

        if (stokvel is null)
            return false;

        _dbContext.Stokvels.Remove(stokvel);
        await _dbContext.SaveChangesAsync();

        return true;
    }
}