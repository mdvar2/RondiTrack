using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class StokvelAuthorizationTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task SameEndpoint_NonMember404_Member403_Treasurer201_Chairperson201()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        foreach (var (user, expected) in new[] { (scenario.Outsider, HttpStatusCode.NotFound),
            (scenario.Member, HttpStatusCode.Forbidden), (scenario.Treasurer, HttpStatusCode.Created),
            (scenario.Chairperson, HttpStatusCode.Created), (scenario.Admin, HttpStatusCode.NotFound) })
        {
            using var client = factory.CreateClientFor(user);
            var response = await client.PostAsJsonAsync($"/api/stokvels/{scenario.StokvelId}/cycles", new { period = "2026-11", targetAmount = 500m });
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.Forbidden)
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Member_CanRecordOwnContribution_ButCannotRecordForAnotherMember()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        foreach (var (userId, expected) in new[] { (scenario.Member.Id, HttpStatusCode.Created), (scenario.Treasurer.Id, HttpStatusCode.Forbidden) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/stokvels/{scenario.StokvelId}/members/{userId}/contributions")
                { Content = JsonContent.Create(new { amount = 500m, contributionCycleId = scenario.CycleId }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task DemotionAndRemoval_TakeEffectWithTheSameAccessToken()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Treasurer);
        var path = $"/api/stokvels/{scenario.StokvelId}/cycles";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path, new { period = "2026-11", targetAmount = 500m })).StatusCode);
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var membership = await context.StokvelMembers.SingleAsync(member => member.StokvelId == scenario.StokvelId && member.UserId == scenario.Treasurer.Id);
        membership.ChangeRole(Roles.Member);
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, new { period = "2026-12", targetAmount = 500m })).StatusCode);
        context.StokvelMembers.Remove(membership);
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task OneUser_CanBeTreasurerInOneStokvel_AndOrdinaryMemberInAnother()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var second = new Stokvel($"Other {Guid.NewGuid()}", 500m);
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Stokvels.Add(second);
        context.StokvelMembers.Add(new StokvelMember(second.Id, scenario.Treasurer.Id, Roles.Member));
        await context.SaveChangesAsync();
        using var client = factory.CreateClientFor(scenario.Treasurer);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/stokvels/{scenario.StokvelId}/cycles", new { period = "2026-11", targetAmount = 500m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/stokvels/{second.Id}/cycles", new { period = "2026-11", targetAmount = 500m })).StatusCode);
    }

    [Fact]
    public async Task PlatformList_IsTheSinglePlainRoleOperation_WithoutMembershipBypass()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var admin = factory.CreateClientFor(scenario.Admin);
        using var member = factory.CreateClientFor(scenario.Member);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/stokvels")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/stokvels")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/stokvels/{scenario.StokvelId}")).StatusCode);
    }

    [Fact]
    public async Task NewStokvel_CreatorIsChairperson_AndCanOpenItsFirstCycle()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Outsider);
        var created = await client.PostAsJsonAsync("/api/stokvels", new { name = $"Created {Guid.NewGuid()}", contributionAmount = 500m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(Roles.Chairperson, (await context.StokvelMembers.SingleAsync(member => member.StokvelId == id && member.UserId == scenario.Outsider.Id)).Role);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/stokvels/{id}/cycles", new { period = "2026-11", targetAmount = 500m })).StatusCode);
    }
}
