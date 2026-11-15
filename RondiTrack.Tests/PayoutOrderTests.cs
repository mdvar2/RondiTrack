using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// The mobile app shows the payout order exactly as the API sends it: it
// doesn't sort, count or decide who is next. So the API has to say all of it.
[Collection(PostgresCollection.Name)]
public class PayoutOrderTests(PostgresApiFactory factory)
{
    private static readonly DateTime Joined = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);

    // Three members who joined a day apart: first, second, third. The first
    // was paid R 1 500,00 in an earlier, paid-out cycle. The current cycle
    // is open with a R 1 500,00 target.
    private sealed record Rotation(
        Guid StokvelId, Guid CycleId, Guid EarlierCycleId,
        User First, User Second, User Third, User Outsider);

    private async Task<Rotation> CreateRotationAsync()
    {
        User NewUser(string name) => new(name, $"{Guid.NewGuid()}@tests.example");
        var first = NewUser("First Member");
        var second = NewUser("Second Member");
        var third = NewUser("Third Member");
        var outsider = NewUser("Outsider");
        var stokvel = new Stokvel($"Rotation {Guid.NewGuid()}", 500m);
        var earlier = new ContributionCycle(stokvel.Id, "2026-10", 1500m);
        var current = new ContributionCycle(stokvel.Id, "2026-11", 1500m);

        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Users.AddRange(first, second, third, outsider);
        context.Stokvels.Add(stokvel);
        context.ContributionCycles.AddRange(earlier, current);

        // Added out of order on purpose: the rotation follows JoinedAtUtc.
        foreach (var (user, role, day) in new[]
                 {
                     (third, Roles.Member, 2), (first, Roles.Chairperson, 0), (second, Roles.Treasurer, 1)
                 })
        {
            var member = new StokvelMember(stokvel.Id, user.Id, role);
            context.StokvelMembers.Add(member);
            context.Entry(member).Property(m => m.JoinedAtUtc).CurrentValue = Joined.AddDays(day);
        }

        context.Payouts.Add(new Payout(stokvel.Id, first.Id, earlier.Id, 1500m));
        earlier.MarkPaidOut();
        await context.SaveChangesAsync();

        return new(stokvel.Id, current.Id, earlier.Id, first, second, third, outsider);
    }

    private async Task<HttpResponseMessage> GetOrderAsync(User caller, Guid stokvelId, Guid cycleId)
    {
        using var client = factory.CreateClientFor(caller);
        return await client.GetAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts");
    }

    [Fact]
    public async Task Member_GetsTheRotationWithPaidNextAndWaiting()
    {
        var rotation = await CreateRotationAsync();

        var response = await GetOrderAsync(rotation.Third, rotation.StokvelId, rotation.CycleId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<PayoutOrderResponse>())!;
        Assert.Equal(rotation.StokvelId, order.StokvelId);
        Assert.Equal(rotation.CycleId, order.CycleId);

        Assert.Equal([1, 2, 3], order.Items.Select(item => item.Position));
        Assert.Equal(
            [rotation.First.Id, rotation.Second.Id, rotation.Third.Id],
            order.Items.Select(item => item.UserId));
        Assert.Equal(
            ["First Member", "Second Member", "Third Member"],
            order.Items.Select(item => item.MemberName));
        Assert.Equal(
            [PayoutStatuses.Paid, PayoutStatuses.Next, PayoutStatuses.Waiting],
            order.Items.Select(item => item.Status));
        Assert.All(order.Items, item => Assert.Equal(150000L, item.AmountCents));

        Assert.NotNull(order.Items[0].PaidAtUtc);
        Assert.Null(order.Items[1].PaidAtUtc);
        Assert.Null(order.Items[2].PaidAtUtc);
    }

    [Fact]
    public async Task Next_IsExactlyWhomTheNextPayoutPays()
    {
        var rotation = await CreateRotationAsync();

        var before = (await (await GetOrderAsync(rotation.First, rotation.StokvelId, rotation.CycleId))
            .Content.ReadFromJsonAsync<PayoutOrderResponse>())!;
        var next = before.Items.Single(item => item.Status == PayoutStatuses.Next);

        using var treasurer = factory.CreateClientFor(rotation.Second);
        var created = await treasurer.PostAsJsonAsync(
            $"/api/v1/stokvels/{rotation.StokvelId}/cycles/{rotation.CycleId}/payouts",
            new { amount = 1500m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payout = (await created.Content.ReadFromJsonAsync<PayoutResponse>())!;
        Assert.Equal(next.UserId, payout.RecipientUserId);

        // The cycle is now PaidOut, and still shows its order: the second
        // member is Paid, and the third is Next.
        var after = (await (await GetOrderAsync(rotation.First, rotation.StokvelId, rotation.CycleId))
            .Content.ReadFromJsonAsync<PayoutOrderResponse>())!;
        Assert.Equal(
            [PayoutStatuses.Paid, PayoutStatuses.Paid, PayoutStatuses.Next],
            after.Items.Select(item => item.Status));
    }

    [Fact]
    public async Task ClosedCycleWithNoPayout_HasNoPayoutOrder()
    {
        var rotation = await CreateRotationAsync();
        await using (var context = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var cycle = await context.ContributionCycles.SingleAsync(c => c.Id == rotation.CycleId);
            cycle.Close();
            await context.SaveChangesAsync();
        }

        var response = await GetOrderAsync(rotation.Second, rotation.StokvelId, rotation.CycleId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<PayoutOrderResponse>())!;
        Assert.Empty(order.Items);
    }

    [Fact]
    public async Task PaidOutCycle_StillHasItsOrder()
    {
        var rotation = await CreateRotationAsync();

        var response = await GetOrderAsync(rotation.Second, rotation.StokvelId, rotation.EarlierCycleId);

        var order = (await response.Content.ReadFromJsonAsync<PayoutOrderResponse>())!;
        Assert.Equal(3, order.Items.Count);
    }

    [Fact]
    public async Task NonMember_GetsNotFound_AsForTheStokvelItself()
    {
        var rotation = await CreateRotationAsync();

        var order = await GetOrderAsync(rotation.Outsider, rotation.StokvelId, rotation.CycleId);
        using var client = factory.CreateClientFor(rotation.Outsider);
        var stokvel = await client.GetAsync($"/api/v1/stokvels/{rotation.StokvelId}");

        Assert.Equal(HttpStatusCode.NotFound, order.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stokvel.StatusCode);
        Assert.Equal("application/problem+json", order.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownCycle_OrCycleOfAnotherStokvel_IsNotFound()
    {
        var rotation = await CreateRotationAsync();
        var other = await CreateRotationAsync();

        var unknown = await GetOrderAsync(rotation.First, rotation.StokvelId, Guid.NewGuid());
        var elsewhere = await GetOrderAsync(rotation.First, rotation.StokvelId, other.CycleId);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
    }

    [Fact]
    public async Task MemberMayReadTheOrder_ButNotRecordAPayout()
    {
        var rotation = await CreateRotationAsync();

        using var member = factory.CreateClientFor(rotation.Third);
        var read = await member.GetAsync(
            $"/api/v1/stokvels/{rotation.StokvelId}/cycles/{rotation.CycleId}/payouts");
        var record = await member.PostAsJsonAsync(
            $"/api/v1/stokvels/{rotation.StokvelId}/cycles/{rotation.CycleId}/payouts",
            new { amount = 1500m });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, record.StatusCode);
    }
}
