using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class UserRepository : IUserRepository
{
    private readonly List<User> _users = new()
    {
        new User("Kelly Ezeji", "kelly@example.com"),
        new User("Yamkela Nomlala", "yamkela@example.com"),
        new User("Mihle Ngcobozi", "mihle@example.com")
    };

    public Task<IEnumerable<User>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_users);
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        var user = _users.FirstOrDefault(user => user.Id == id);
        return Task.FromResult(user);
    }

    public Task AddAsync(User user)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var user = _users.FirstOrDefault(user => user.Id == id);

        if (user is null)
            return Task.FromResult(false);

        _users.Remove(user);
        return Task.FromResult(true);
    }
}