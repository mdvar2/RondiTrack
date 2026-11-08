using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 9.2: the API side of the invite link preview (Part 6) and the
// contribution maximum the web form shares (Part 1).
[Collection(PostgresCollection.Name)]
public sealed class Assignment92ApiTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task InviteStatus_IsAnonymous_AndSaysOnlyValidOrExpired()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var treasurer = factory.CreateClientFor(scenario.Treasurer);
        using var anonymous = factory.CreateClient();

        var created = await treasurer.PostAsync($"/api/v1/stokvels/{scenario.StokvelId}/invites", null);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

        var response = await anonymous.GetAsync($"/api/v1/invites/{code}/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["status"], json.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("valid", json.GetProperty("status").GetString());

        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var stokvelName = (await db.Stokvels.FindAsync(scenario.StokvelId))!.Name;
        Assert.DoesNotContain(stokvelName, body, StringComparison.OrdinalIgnoreCase);

        var expired = new StokvelInvite(scenario.StokvelId, DateTime.UtcNow.AddMinutes(-1));
        db.StokvelInvites.Add(expired);
        await db.SaveChangesAsync();
        var expiredJson = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/invites/{expired.Code}/status");
        Assert.Equal("expired", expiredJson.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/invites/no-such-code/status")).StatusCode);
        // The full preview still needs a signed-in person.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/invites/{code}")).StatusCode);
    }

    [Theory]
    [InlineData(100_000_000L, HttpStatusCode.Created)]
    [InlineData(100_000_001L, HttpStatusCode.BadRequest)]
    public async Task Contribution_AboveTheMaximum_IsAFieldErrorOnAmountInCents(long amountInCents, HttpStatusCode expected)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var member = factory.CreateClientFor(scenario.Member);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions")
        {
            Content = JsonContent.Create(new { amountInCents, contributionCycleId = scenario.CycleId }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await member.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(problem.GetProperty("errors").TryGetProperty("amountInCents", out _));
        }
    }
}
