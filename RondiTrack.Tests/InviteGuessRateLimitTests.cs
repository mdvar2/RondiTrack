using System.Net;
using System.Text.Json;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 9.3 Part 3, 429: a signed-in account guessing invite codes is
// slowed per account, with Retry-After; another account is not affected.
[Collection(PostgresCollection.Name)]
public class InviteGuessRateLimitTests(PostgresApiFactory factory)
{
    private async Task<User> NewUserAsync()
    {
        var user = new User("Invite guesser", $"guess-{Guid.NewGuid():N}@tests.example");
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task EleventhGuessInAMinute_Is429WithRetryAfter_AndOnlyForThatAccount()
    {
        var guesser = await NewUserAsync();
        var other = await NewUserAsync();
        using var client = factory.CreateClientFor(guesser);

        for (var i = 0; i < 10; i++)
        {
            using var miss = await client.GetAsync($"/api/v1/invites/NOPE{i:D4}");
            Assert.Equal(HttpStatusCode.NotFound, miss.StatusCode);
        }

        using var limited = await client.GetAsync("/api/v1/invites/NOPE9999");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var retryAfter = Assert.Single(limited.Headers.GetValues("Retry-After"));
        Assert.True(int.Parse(retryAfter) is > 0 and <= 60);
        using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("correlationId", out _));

        // Joining with a guessed code shares the same allowance.
        using var join = await client.PostAsync("/api/v1/invites/NOPE9999/join", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, join.StatusCode);

        using var otherClient = factory.CreateClientFor(other);
        using var unaffected = await otherClient.GetAsync("/api/v1/invites/NOPE0000");
        Assert.Equal(HttpStatusCode.NotFound, unaffected.StatusCode);
    }
}
