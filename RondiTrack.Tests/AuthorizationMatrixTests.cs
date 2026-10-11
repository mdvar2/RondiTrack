using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class AuthorizationMatrixTests(PostgresApiFactory factory)
{
    [Theory]
    [InlineData("GET", "", true)]
    [InlineData("PUT", "", false)]
    [InlineData("DELETE", "", false)]
    [InlineData("GET", "/members", true)]
    [InlineData("POST", "/members/{user}", false)]
    [InlineData("DELETE", "/members/{user}", false)]
    [InlineData("POST", "/members/{user}/contributions", false)]
    [InlineData("GET", "/cycles", true)]
    [InlineData("GET", "/cycles/{cycle}", true)]
    [InlineData("GET", "/cycles/{cycle}/contributions", true)]
    [InlineData("POST", "/cycles", false)]
    [InlineData("PUT", "/cycles/{cycle}", false)]
    [InlineData("DELETE", "/cycles/{cycle}", false)]
    [InlineData("POST", "/cycles/{cycle}/payouts", false)]
    public async Task EveryScopedRoute_RequiresAuthenticationAndLiveMembership(string method, string suffix, bool view)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var path = $"/api/stokvels/{scenario.StokvelId}" + suffix
            .Replace("{user}", scenario.Treasurer.Id.ToString()).Replace("{cycle}", scenario.CycleId.ToString());
        foreach (var (user, expected) in new[] {
            ((User?)null, HttpStatusCode.Unauthorized), (scenario.Outsider, HttpStatusCode.NotFound),
            (scenario.Admin, HttpStatusCode.NotFound),
            (scenario.Member, view ? HttpStatusCode.OK : HttpStatusCode.Forbidden) })
        {
            using var client = user is null ? factory.CreateClient() : factory.CreateClientFor(user);
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method is "POST" or "PUT")
                request.Content = JsonContent.Create(new { name = "Updated", contributionAmount = 500m,
                    role = Roles.Member, period = "2026-11", targetAmount = 500m, amount = 500m,
                    contributionCycleId = scenario.CycleId });
            var response = await client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
            if (expected != HttpStatusCode.OK)
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task PlatformDemotionAndDeletedUser_AffectExistingTokensImmediately()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var user = await context.Users.SingleAsync(user => user.Id == scenario.Admin.Id);
        user.SetPlatformRole(Roles.PlatformUser);
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        context.Users.Remove(user);
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreasurerAndChairperson_CanRecordForOthersAndProcessPayout(bool chairperson)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(chairperson ? scenario.Chairperson : scenario.Treasurer);
        using var contribution = new HttpRequestMessage(HttpMethod.Post,
            $"/api/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions")
        { Content = JsonContent.Create(new { amount = 500m, contributionCycleId = scenario.CycleId }) };
        contribution.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(contribution)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/payouts", new { amount = 500m })).StatusCode);
    }
}
