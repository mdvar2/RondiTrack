using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// The answers to the Assignment 7.4 "Before you start" questions.
[Collection(PostgresCollection.Name)]
public class ContributionIdempotencyContractTests(PostgresApiFactory factory)
{
    private static string Path(AuthScenario scenario) =>
        $"/api/v1/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions";

    private static HttpRequestMessage Post(AuthScenario scenario, string? key, long cents)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path(scenario))
        {
            Content = JsonContent.Create(new { amountInCents = cents, contributionCycleId = scenario.CycleId })
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<string> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString()!;
    }

    // Question 1
    [Fact]
    public async Task SameKeySameRequest_ReplaysTheSame201_WithoutAReplayHeader()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        var key = Guid.NewGuid().ToString();
        using var first = await client.SendAsync(Post(scenario, key, 15050));
        using var second = await client.SendAsync(Post(scenario, key, 15050));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.DoesNotContain(second.Headers, header => header.Key.Contains("replay", StringComparison.OrdinalIgnoreCase));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.Contributions.CountAsync(c => c.ContributionCycleId == scenario.CycleId));
    }

    // Question 2
    [Fact]
    public async Task SameKeyDifferentAmount_Is409_AndRecordsNothingNew()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        var key = Guid.NewGuid().ToString();
        using var first = await client.SendAsync(Post(scenario, key, 15050));
        using var second = await client.SendAsync(Post(scenario, key, 20000));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("different request", await Detail(second));
    }

    // Question 3
    [Fact]
    public async Task NewKeyForAMemberWhoAlreadyContributed_Is409WithAReason()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        using var first = await client.SendAsync(Post(scenario, Guid.NewGuid().ToString(), 15050));
        using var second = await client.SendAsync(Post(scenario, Guid.NewGuid().ToString(), 15050));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already exists", await Detail(second));
    }

    // Question 5
    [Fact]
    public async Task MissingKey_Is400_AndOpenApiDescribesTheHeader()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        using var response = await client.SendAsync(Post(scenario, null, 15050));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var parameters = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/stokvels/{stokvelId}/members/{userId}/contributions")
            .GetProperty("post").GetProperty("parameters").EnumerateArray();
        Assert.Contains(parameters, parameter =>
            parameter.GetProperty("name").GetString() == "Idempotency-Key"
            && parameter.GetProperty("in").GetString() == "header"
            && parameter.GetProperty("required").GetBoolean());
    }

    // The form is only shown for an open cycle, so the API must refuse the rest too.
    [Fact]
    public async Task ClosedCycle_Is409WithAReason()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var cycle = await db.ContributionCycles.SingleAsync(c => c.Id == scenario.CycleId);
            cycle.Close();
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClientFor(scenario.Member);
        using var response = await client.SendAsync(Post(scenario, Guid.NewGuid().ToString(), 15050));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("no longer open", await Detail(response));
    }
}
