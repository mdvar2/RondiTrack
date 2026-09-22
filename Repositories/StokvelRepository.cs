using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class StokvelRepository : IStokvelRepository
{
    private readonly List<Stokvel> _stokvels = new()
    {
        new Stokvel("Ubuntu Savings Club", 500m),
        new Stokvel("Siyakhula Stokvel", 1000m)
    };

    public Task<IEnumerable<Stokvel>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Stokvel>>(_stokvels);
    }

    public Task<Stokvel?> GetByIdAsync(Guid id)
    {
        var stokvel = _stokvels.FirstOrDefault(stokvel => stokvel.Id == id);
        return Task.FromResult(stokvel);
    }

    public Task AddAsync(Stokvel stokvel)
    {
        _stokvels.Add(stokvel);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var stokvel = _stokvels.FirstOrDefault(stokvel => stokvel.Id == id);

        if (stokvel is null)
            return Task.FromResult(false);

        _stokvels.Remove(stokvel);
        return Task.FromResult(true);
    }
}
