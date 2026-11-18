using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.DevSeed;

// The mobile app's three seed personas (README "Development seed"), so the
// app can sign in as them in its dev flavour. The ids, names, roles, amounts
// and rotation order are the app's fakes' (RondiTrackApp
// lib/features/stokvels/data/fake_seed.dart), so moving the app from its
// fakes to this API changes nothing on screen but the dates.
//
// Development only, and only when DevSeed:Password is set: the password is
// passed at run time and never committed. Seeding twice changes nothing.
public sealed class DevSeeder(RondiTrackDbContext db, IPasswordHasher<User> hasher, TimeProvider clock)
{
    public const string PasswordKey = "DevSeed:Password";

    public static readonly Guid LeratoId = Guid.Parse("0b6f1a52-6a0e-4b7e-9a51-1c2f6c3e0001");
    public static readonly Guid SiphoId = Guid.Parse("0b6f1a52-6a0e-4b7e-9a51-1c2f6c3e0002");
    public static readonly Guid ThaboId = Guid.Parse("0b6f1a52-6a0e-4b7e-9a51-1c2f6c3e0003");
    public static readonly Guid NomsaId = Guid.Parse("0b6f1a52-6a0e-4b7e-9a51-1c2f6c3e0004");
    public static readonly Guid KagisoId = Guid.Parse("0b6f1a52-6a0e-4b7e-9a51-1c2f6c3e0005");
    public static readonly Guid MabenaFamilyId = Guid.Parse("5d0c9a4e-2b1f-4c8a-8e7d-3a6b9f2e0101");
    public static readonly Guid SowetoTeachersId = Guid.Parse("5d0c9a4e-2b1f-4c8a-8e7d-3a6b9f2e0102");
    // None of the three personas is in it: a real stokvel whose link Lerato
    // can open and get the same 404 as for one that doesn't exist.
    public static readonly Guid MolefeBurialId = Guid.Parse("5d0c9a4e-2b1f-4c8a-8e7d-3a6b9f2e0103");
    public static readonly Guid MabenaCycleId = Guid.Parse("9e8f7a6b-5c4d-4e3f-8a2b-1c0d9e8f0201");
    public static readonly Guid SowetoCycleId = Guid.Parse("9e8f7a6b-5c4d-4e3f-8a2b-1c0d9e8f0202");
    public static readonly Guid MabenaLastCycleId = Guid.Parse("9e8f7a6b-5c4d-4e3f-8a2b-1c0d9e8f0200");

    public const string LeratoEmail = "lerato@ronditrack.example";
    public const string SiphoEmail = "sipho@ronditrack.example";
    public const string ThaboEmail = "thabo@ronditrack.example";

    // Whether the seed runs: in Development, with a password given. Anywhere
    // else a password is ignored, so a production setting can't create
    // accounts with a known password.
    public static bool ShouldRun(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration[PasswordKey]);

    // Returns false when the seed was already there.
    public async Task<bool> SeedAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("A seed password is required.", nameof(password));
        if (await db.Users.AnyAsync(user => user.Id == LeratoId, cancellationToken))
            return false;

        // Cycles follow the Johannesburg calendar month, as /me/home does.
        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Africa/Johannesburg");
        var month = new DateOnly(now.Year, now.Month, 1);
        var lastMonth = month.AddMonths(-1);
        var monthStartUtc = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var lerato = Person(LeratoId, "Lerato Mokoena", LeratoEmail, password);
        var sipho = Person(SiphoId, "Sipho Dlamini", SiphoEmail, password);
        var thabo = Person(ThaboId, "Thabo Nkosi", ThaboEmail, password);
        var nomsa = Person(NomsaId, "Nomsa Mabena", "nomsa@ronditrack.example", password);
        var kagiso = Person(KagisoId, "Kagiso Molefe", "kagiso@ronditrack.example", password);
        db.Users.AddRange(lerato, sipho, thabo, nomsa, kagiso);

        var mabena = WithId(new Stokvel("Mabena Family", 500m), MabenaFamilyId);
        var soweto = WithId(new Stokvel("Soweto Teachers", 1500m), SowetoTeachersId);
        var molefe = WithId(new Stokvel("Molefe Burial Society", 200m), MolefeBurialId);
        db.Stokvels.AddRange(mabena, soweto, molefe);

        // Rotation order is JoinedAtUtc: Mabena pays Nomsa, Lerato, Sipho;
        // Soweto pays Kagiso, Sipho, Lerato.
        var joined = monthStartUtc.AddMonths(-6);
        Member(mabena, nomsa, Roles.Chairperson, joined);
        Member(mabena, lerato, Roles.Treasurer, joined.AddDays(1));
        Member(mabena, sipho, Roles.Member, joined.AddDays(2));
        Member(soweto, kagiso, Roles.Treasurer, joined);
        Member(soweto, sipho, Roles.Member, joined.AddDays(1));
        Member(soweto, lerato, Roles.Member, joined.AddDays(2));
        Member(molefe, kagiso, Roles.Chairperson, joined);
        Member(molefe, nomsa, Roles.Member, joined.AddDays(1));

        // Mabena Family's last month: closed with Sipho R 250,00 short, then
        // paid out to Nomsa.
        var mabenaLast = WithId(new ContributionCycle(mabena.Id, Period(lastMonth), 1500m, LastDay(lastMonth)),
            MabenaLastCycleId);
        var mabenaNow = WithId(new ContributionCycle(mabena.Id, Period(month), 1500m, LastDay(month)), MabenaCycleId);
        var sowetoNow = WithId(new ContributionCycle(soweto.Id, Period(month), 4500m, month.AddDays(24)),
            SowetoCycleId);
        db.ContributionCycles.AddRange(mabenaLast, mabenaNow, sowetoNow);

        Contribution(mabenaNow, lerato, 500m, "Paid", "7a6b5c4d-3e2f-4a1b-9c8d-7e6f5a4b0301",
            monthStartUtc.AddDays(4).AddHours(8.5));
        Contribution(mabenaNow, nomsa, 500m, "Confirmed", "7a6b5c4d-3e2f-4a1b-9c8d-7e6f5a4b0302",
            monthStartUtc.AddDays(1).AddHours(17).AddMinutes(5));
        Contribution(sowetoNow, lerato, 750m, "Paid", "7a6b5c4d-3e2f-4a1b-9c8d-7e6f5a4b0303",
            monthStartUtc.AddDays(5).AddHours(12));
        Contribution(sowetoNow, kagiso, 1500m, "Confirmed", "7a6b5c4d-3e2f-4a1b-9c8d-7e6f5a4b0304",
            monthStartUtc.AddDays(0).AddHours(9).AddMinutes(15));

        db.ContributionArrears.Add(new ContributionArrear(mabena.Id, sipho.Id, mabenaLast.Id, 25000));
        mabenaLast.Close();
        var payout = new Payout(mabena.Id, nomsa.Id, mabenaLast.Id, 1500m);
        db.Payouts.Add(payout);
        db.Entry(payout).Property(p => p.PaidAtUtc).CurrentValue = monthStartUtc.AddHours(-9);
        mabenaLast.MarkPaidOut();

        // The invite codes the app's fakes use (README "Deep links").
        Invite(mabena, "MABENA26");
        Invite(soweto, "SOWETO26");

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private User Person(Guid id, string name, string email, string password)
    {
        var user = WithId(new User(name, email), id);
        user.SetPasswordHash(hasher.HashPassword(user, password));
        return user;
    }

    private T WithId<T>(T entity, Guid id) where T : class
    {
        db.Entry(entity).Property("Id").CurrentValue = id;
        return entity;
    }

    private void Member(Stokvel stokvel, User user, string role, DateTime joinedAtUtc)
    {
        var member = new StokvelMember(stokvel.Id, user.Id, role);
        db.StokvelMembers.Add(member);
        db.Entry(member).Property(m => m.JoinedAtUtc).CurrentValue = joinedAtUtc;
    }

    private void Contribution(ContributionCycle cycle, User user, decimal amount, string status, string id,
        DateTime recordedAtUtc)
    {
        var contribution = WithId(new Contribution(cycle.StokvelId, user.Id, cycle.Id, amount), Guid.Parse(id));
        contribution.UpdateStatus(status);
        db.Contributions.Add(contribution);
        db.Entry(contribution).Property(c => c.RecordedAtUtc).CurrentValue = recordedAtUtc;
    }

    private void Invite(Stokvel stokvel, string code)
    {
        var invite = new StokvelInvite(stokvel.Id, clock.GetUtcNow().UtcDateTime.AddYears(1));
        db.StokvelInvites.Add(invite);
        db.Entry(invite).Property(i => i.Code).CurrentValue = code;
    }

    private static string Period(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static DateOnly LastDay(DateOnly month) => month.AddMonths(1).AddDays(-1);
}
