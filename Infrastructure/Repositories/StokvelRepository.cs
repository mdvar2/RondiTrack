using RondiTrack.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Infrastructure.Repositories;

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

    public async Task AddWithChairpersonAsync(Stokvel stokvel, Guid userId)
    {
        _dbContext.Stokvels.Add(stokvel);
        _dbContext.StokvelMembers.Add(new StokvelMember(stokvel.Id, userId, Roles.Chairperson));
        await _dbContext.SaveChangesAsync();
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