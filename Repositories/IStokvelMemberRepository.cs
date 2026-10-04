using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IStokvelMemberRepository
{
    Task<StokvelMember?> GetByStokvelAndUserAsync(
        Guid stokvelId,
        Guid userId);

    Task<bool> ExistsAsync(
        Guid stokvelId,
        Guid userId);

    Task AddAsync(StokvelMember stokvelMember);

    Task RemoveAsync(StokvelMember stokvelMember);
}