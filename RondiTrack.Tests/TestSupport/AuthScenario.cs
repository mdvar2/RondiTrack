using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Tests.TestSupport;

public record AuthScenario(Guid StokvelId, Guid CycleId, User Member, User Treasurer, User Chairperson, User Outsider, User Admin)
{
    public static async Task<AuthScenario> CreateAsync(PostgresApiFactory factory)
    {
        User NewUser(string name) => new(name, $"{Guid.NewGuid()}@tests.example");
        var member = NewUser("Member");
        var treasurer = NewUser("Treasurer");
        var chairperson = NewUser("Chairperson");
        var outsider = NewUser("Outsider");
        var admin = NewUser("Platform admin");
        admin.SetPlatformRole(Roles.PlatformAdmin);
        var stokvel = new Stokvel($"Security {Guid.NewGuid()}", 500m);
        var cycle = new ContributionCycle(stokvel.Id, "2026-10", 500m);
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Users.AddRange(member, treasurer, chairperson, outsider, admin);
        context.Stokvels.Add(stokvel);
        context.ContributionCycles.Add(cycle);
        context.StokvelMembers.AddRange(new StokvelMember(stokvel.Id, member.Id, Roles.Member),
            new StokvelMember(stokvel.Id, treasurer.Id, Roles.Treasurer),
            new StokvelMember(stokvel.Id, chairperson.Id, Roles.Chairperson));
        await context.SaveChangesAsync();
        return new(stokvel.Id, cycle.Id, member, treasurer, chairperson, outsider, admin);
    }
}
