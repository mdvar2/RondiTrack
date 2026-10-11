namespace RondiTrack.Domain.Entities;

public class User
{
    private readonly List<StokvelMember> _stokvelMemberships = new();

    public Guid Id { get; private set; }

    public uint Version { get; private set; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    // Existing users remain intact and cannot log in until assigned a password hash.
    public string PlatformRole { get; private set; } = Roles.PlatformUser;

    public void SetPlatformRole(string role)
    {
        if (role is not (Roles.PlatformUser or Roles.PlatformAdmin))
            throw new ArgumentException("Unknown platform role.");
        PlatformRole = role;
    }

    public string? PasswordHash { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.");
        PasswordHash = passwordHash;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public IReadOnlyCollection<StokvelMember> StokvelMemberships =>
        _stokvelMemberships.AsReadOnly();

    public User(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
    }

    public void UpdateDetails(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.");

        Name = name.Trim();
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
    }
}