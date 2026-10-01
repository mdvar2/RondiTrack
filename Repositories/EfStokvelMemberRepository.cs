using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class EfStokvelMemberRepository
    : IStokvelMemberRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public EfStokvelMemberRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StokvelMember?>
        GetByStokvelAndUserAsync(
            Guid stokvelId,
            Guid userId)
    {
        return await _dbContext.StokvelMembers
            .FirstOrDefaultAsync(
                member =>
                    member.StokvelId == stokvelId &&
                    member.UserId == userId);
    }

    public async Task AddAsync(
        StokvelMember stokvelMember)
    {
        await _dbContext.StokvelMembers.AddAsync(
            stokvelMember);

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(
        StokvelMember stokvelMember)
    {
        _dbContext.StokvelMembers.Remove(
            stokvelMember);

        await _dbContext.SaveChangesAsync();
    }
}