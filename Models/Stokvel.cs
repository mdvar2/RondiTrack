namespace RondiTrack.Models;

public class Stokvel
{
    private readonly List<User> _members = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public IReadOnlyCollection<User> Members => _members.AsReadOnly();

    public Stokvel(string name, decimal contributionAmount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stokvel name is required.");

        if (contributionAmount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        ContributionAmount = contributionAmount;
    }

    public void UpdateDetails(string name, decimal contributionAmount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stokvel name is required.");

        if (contributionAmount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.");

        Name = name.Trim();
        ContributionAmount = contributionAmount;
    }

    public void AddMember(User user)
    {
        if (_members.Any(member => member.Id == user.Id))
            throw new InvalidOperationException("User is already a member of this stokvel.");

        _members.Add(user);
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(member => member.Id == userId);

        if (member is null)
            throw new InvalidOperationException("User is not a member of this stokvel.");

        _members.Remove(member);
    }
}