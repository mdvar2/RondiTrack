using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Push;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 12.2 Part 1: a member's phones, and the push after a
// treasurer confirms a contribution. Proved with a recording sender: no
// Firebase project (README "Notifications").
[Collection(PostgresCollection.Name)]
public class DevicesAndPushTests(PostgresApiFactory factory)
{
    private sealed record World(
        Stokvel Stokvel,
        ContributionCycle Cycle,
        User Treasurer,
        User Member,
        User Other,
        Contribution MemberPaid,
        Contribution OtherPaid);

    private static string Phone() => $"fid-{Guid.NewGuid():N}";

    private async Task<World> CreateAsync()
    {
        var stokvel = new Stokvel($"Mabena Family {Guid.NewGuid():N}", 500m);
        var cycle = new ContributionCycle(stokvel.Id, "2026-11", 1500m);
        var treasurer = new User("Lerato Mokoena", $"lerato-{Guid.NewGuid():N}@tests.example");
        var member = new User("Sipho Dlamini", $"sipho-{Guid.NewGuid():N}@tests.example");
        var other = new User("Nomsa Mabena", $"nomsa-{Guid.NewGuid():N}@tests.example");
        var memberPaid = new Contribution(stokvel.Id, member.Id, cycle.Id, 500m);
        var otherPaid = new Contribution(stokvel.Id, other.Id, cycle.Id, 500m);

        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        db.Users.AddRange(treasurer, member, other);
        db.Stokvels.Add(stokvel);
        db.ContributionCycles.Add(cycle);
        db.StokvelMembers.AddRange(
            new StokvelMember(stokvel.Id, treasurer.Id, Roles.Treasurer),
            new StokvelMember(stokvel.Id, member.Id, Roles.Member),
            new StokvelMember(stokvel.Id, other.Id, Roles.Member));
        db.Contributions.AddRange(memberPaid, otherPaid);
        await db.SaveChangesAsync();
        return new(stokvel, cycle, treasurer, member, other, memberPaid, otherPaid);
    }

    private static string ConfirmUrl(World w, Contribution c) =>
        $"/api/v1/stokvels/{w.Stokvel.Id}/cycles/{w.Cycle.Id}/contributions/{c.Id}/confirm";

    private async Task RegisterAsync(User who, string phone)
    {
        using var client = factory.CreateClientFor(who);
        var response = await client.PostAsJsonAsync("/api/v1/me/devices", new { installationId = phone });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<HttpResponseMessage> ConfirmAsync(World w, Contribution c, User? by = null)
    {
        using var client = factory.CreateClientFor(by ?? w.Treasurer);
        return await client.PostAsync(ConfirmUrl(w, c), null);
    }

    private async Task<Guid?> OwnerAsync(string phone)
    {
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        return await db.DeviceInstallations.Where(d => d.InstallationId == phone)
            .Select(d => (Guid?)d.UserId).SingleOrDefaultAsync();
    }

    private async Task<string> StatusAsync(Contribution c)
    {
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        return await db.Contributions.Where(x => x.Id == c.Id).Select(x => x.Status).SingleAsync();
    }

    // ---- Devices -----------------------------------------------------------

    [Fact]
    public async Task Register_AnswersNoContent_AndTheCallerOwnsThePhone()
    {
        var w = await CreateAsync();
        var phone = Phone();

        await RegisterAsync(w.Member, phone);

        Assert.Equal(w.Member.Id, await OwnerAsync(phone));
    }

    [Fact]
    public async Task ASharedPhone_MovesToWhoeverRegistersItNow()
    {
        var w = await CreateAsync();
        var phone = Phone();

        await RegisterAsync(w.Member, phone);
        await RegisterAsync(w.Other, phone);

        Assert.Equal(w.Other.Id, await OwnerAsync(phone));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.DeviceInstallations.CountAsync(d => d.InstallationId == phone));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has spaces in it")]
    [InlineData("semi;colon")]
    public async Task Register_RefusesAnIdThatIsNotOne(string installationId)
    {
        var w = await CreateAsync();
        using var client = factory.CreateClientFor(w.Member);

        var response = await client.PostAsJsonAsync("/api/v1/me/devices", new { installationId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RefusesAnIdLongerThan255()
    {
        var w = await CreateAsync();
        using var client = factory.CreateClientFor(w.Member);

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/devices", new { installationId = new string('a', 256) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Devices_NeedSigningIn()
    {
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/v1/me/devices", new { installationId = Phone() });
        var delete = await client.DeleteAsync($"/api/v1/me/devices/{Phone()}");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task Forget_RemovesOnlyTheCallersOwnPhone()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);

        using (var someoneElse = factory.CreateClientFor(w.Other))
        {
            var response = await someoneElse.DeleteAsync($"/api/v1/me/devices/{phone}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        Assert.Equal(w.Member.Id, await OwnerAsync(phone));

        using (var owner = factory.CreateClientFor(w.Member))
        {
            var response = await owner.DeleteAsync($"/api/v1/me/devices/{phone}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        Assert.Null(await OwnerAsync(phone));
    }

    // ---- The push after a confirmation -------------------------------------

    [Fact]
    public async Task Confirm_TellsTheMembersPhones_AndNobodyElse()
    {
        var w = await CreateAsync();
        var (memberPhone, memberTablet, otherPhone, treasurerPhone) = (Phone(), Phone(), Phone(), Phone());
        await RegisterAsync(w.Member, memberPhone);
        await RegisterAsync(w.Member, memberTablet);
        await RegisterAsync(w.Other, otherPhone);
        await RegisterAsync(w.Treasurer, treasurerPhone);

        var response = await ConfirmAsync(w, w.MemberPaid);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Confirmed", await StatusAsync(w.MemberPaid));
        Assert.Equal(
            new[] { memberPhone, memberTablet }.Order(StringComparer.Ordinal),
            factory.Push.To(memberPhone, memberTablet)
                .Select(s => s.InstallationId)
                .Order(StringComparer.Ordinal));
        Assert.Empty(factory.Push.To(otherPhone, treasurerPhone));
    }

    [Fact]
    public async Task ThePayload_IsExactlyTheReadmeTable()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);

        await ConfirmAsync(w, w.MemberPaid);

        var message = Assert.Single(factory.Push.To(phone)).Message;
        Assert.Equal("RondiTrack", message.Title);
        Assert.Equal("There's an update on your stokvel.", message.Body);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["type"] = "contribution_update",
                ["stokvelId"] = w.Stokvel.Id.ToString(),
                ["cycleId"] = w.Cycle.Id.ToString(),
            },
            message.Data);
    }

    // The first twist: the lock screen is public. Nothing in any field may
    // say who, how much, which stokvel, or what was decided.
    [Fact]
    public async Task ThePush_CarriesNoNameEmailStokvelNameAmountOrDecision()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);

        await ConfirmAsync(w, w.MemberPaid);

        var message = Assert.Single(factory.Push.To(phone)).Message;
        var fields = new List<string> { message.Title, message.Body };
        fields.AddRange(message.Data.Keys);
        fields.AddRange(message.Data.Values);
        var forbidden = new[]
        {
            w.Member.Name, "Sipho", "Dlamini",
            w.Member.Email,
            w.Stokvel.Name, "Mabena",
            "50000",                  // the amount in cents
            "R 500,00",          // as the app shows it, with its no-break space
            "R 500,00", "500,00", "500.00", "R500",
            "confirm", "paid", "accepted",
        };
        foreach (var field in fields)
        {
            foreach (var word in forbidden)
            {
                Assert.False(
                    field.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"The push field \"{field}\" carries \"{word}\".");
            }
        }
    }

    [Fact]
    public async Task OnASharedPhone_OnlyTheMemberSignedInNowIsTold()
    {
        var w = await CreateAsync();
        var shared = Phone();
        await RegisterAsync(w.Member, shared);
        await RegisterAsync(w.Other, shared); // Nomsa signs in on Sipho's phone

        await ConfirmAsync(w, w.MemberPaid);
        Assert.Empty(factory.Push.To(shared));

        await ConfirmAsync(w, w.OtherPaid);
        Assert.Single(factory.Push.To(shared));
    }

    [Fact]
    public async Task ThePush_IsSentAfterTheSave_NeverBefore()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);
        string? statusWhenSent = null;
        factory.Push.OnSend = async ids =>
        {
            if (ids.Contains(phone))
                statusWhenSent = await StatusAsync(w.MemberPaid);
        };
        try
        {
            await ConfirmAsync(w, w.MemberPaid);
        }
        finally
        {
            factory.Push.OnSend = null;
        }

        Assert.Equal("Confirmed", statusWhenSent);
    }

    [Fact]
    public async Task AFailingPush_NeverFailsTheConfirmation()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);
        factory.Push.Failing[phone] = true;

        var response = await ConfirmAsync(w, w.MemberPaid);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Confirmed", await StatusAsync(w.MemberPaid));
    }

    [Fact]
    public async Task IdsFirebaseReportsGone_AreDeleted()
    {
        var w = await CreateAsync();
        var (gone, alive) = (Phone(), Phone());
        await RegisterAsync(w.Member, gone);
        await RegisterAsync(w.Member, alive);
        factory.Push.Gone[gone] = true;

        await ConfirmAsync(w, w.MemberPaid);

        Assert.Null(await OwnerAsync(gone));
        Assert.Equal(w.Member.Id, await OwnerAsync(alive));
    }

    // ---- Who may confirm ----------------------------------------------------

    [Fact]
    public async Task AMember_CannotConfirm_AndNobodyIsTold()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);

        var response = await ConfirmAsync(w, w.MemberPaid, by: w.Other);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Paid", await StatusAsync(w.MemberPaid));
        Assert.Empty(factory.Push.To(phone));
    }

    [Fact]
    public async Task SomeoneNotInTheStokvel_GetsNotFound()
    {
        var w = await CreateAsync();
        var stranger = new User("Stranger", $"stranger-{Guid.NewGuid():N}@tests.example");
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            db.Users.Add(stranger);
            await db.SaveChangesAsync();
        }

        var response = await ConfirmAsync(w, w.MemberPaid, by: stranger);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmingTwice_TellsTheMemberOnce()
    {
        var w = await CreateAsync();
        var phone = Phone();
        await RegisterAsync(w.Member, phone);

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(w, w.MemberPaid)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(w, w.MemberPaid)).StatusCode);

        Assert.Single(factory.Push.To(phone));
    }

    [Fact]
    public async Task AContributionFromAnotherCycle_IsNotFound()
    {
        var w = await CreateAsync();
        using var client = factory.CreateClientFor(w.Treasurer);

        var response = await client.PostAsync(
            $"/api/v1/stokvels/{w.Stokvel.Id}/cycles/{Guid.NewGuid()}/contributions/{w.MemberPaid.Id}/confirm",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

// What the Firebase sender hands to Firebase, without Firebase.
public class FirebasePushMessageTests
{
    [Fact]
    public void AFirebaseMessage_IsTimedNow_OnTheAppsChannel_AndPrivate()
    {
        var now = new DateTime(2026, 11, 26, 2, 31, 0, DateTimeKind.Utc);
        var push = RondiTrack.Push.PushMessages.ContributionUpdate(Guid.NewGuid(), Guid.NewGuid());

        var message = RondiTrack.Push.FirebasePushSender.BuildMessage("fid-1", push, now);

        Assert.Equal("fid-1", message.Fid);
        Assert.Equal(now, message.Android.Notification.EventTimestamp);
        Assert.Equal("stokvel_updates", message.Android.Notification.ChannelId);
        Assert.Equal(FirebaseAdmin.Messaging.NotificationVisibility.PRIVATE, message.Android.Notification.Visibility);
        Assert.Equal(push.Title, message.Notification.Title);
        Assert.Equal(push.Body, message.Notification.Body);
        Assert.Equal(push.Data, message.Data);
    }
}
