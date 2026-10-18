using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Auth;
using RondiTrack.Domain.Entities;
using RondiTrack.Idempotency;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class RateLimitContractTests(PostgresApiFactory factory)
{
    private WebApplicationFactory<Program> Limited(string policy, int seconds = 60) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting($"RateLimiting:{policy}:TokenLimit", "2");
        builder.UseSetting($"RateLimiting:{policy}:TokensPerPeriod", "2");
        builder.UseSetting($"RateLimiting:{policy}:ReplenishmentSeconds", seconds.ToString());
    });

    private static HttpClient Client(WebApplicationFactory<Program> app, User user)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            app.Services.GetRequiredService<TokenService>().CreateAccessToken(user).AccessToken);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");
        return client;
    }

    private static async Task<int> AssertRejected(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(int.TryParse(response.Headers.GetValues("Retry-After").Single(), out var seconds) && seconds > 0);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("Retry-After", string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Too Many Requests", json.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
        Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("correlationId").GetString()!);
        return seconds;
    }

    [Fact]
    public async Task Login_FixedWindow_RejectsWithCorrelatedProblem()
    {
        await using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Login:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Login:WindowSeconds", "60");
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");
        var body = new { email = $"{Guid.NewGuid()}@tests.example", password = Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode);
        await AssertRejected(await client.PostAsJsonAsync("/api/v1/auth/login", body));
    }

    [Fact]
    public async Task Contribution429_DoesNotStoreKey_ThenSucceedsAndReplaysExactlyOnce()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using var app = Limited("Contributions", seconds: 3);
        using var client = Client(app, scenario.Treasurer);
        var path = $"/api/v1/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions";
        var body = new { contributionCycleId = scenario.CycleId, amount = 250m };
        // Validly bound requests without a key consume permits but cannot write money.
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode);
        var key = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var wait = await AssertRejected(await client.PostAsJsonAsync(path, body));
        using (var scope = app.Services.CreateScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().GetAsync(key));
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(0, await context.Contributions.CountAsync(c => c.StokvelId == scenario.StokvelId && c.UserId == scenario.Member.Id));
        await Task.Delay(TimeSpan.FromSeconds(wait) + TimeSpan.FromMilliseconds(200));
        var success = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        var replay = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await success.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using (var scope = app.Services.CreateScope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().GetAsync(key));
        Assert.Equal(1, await context.Contributions.CountAsync(c => c.StokvelId == scenario.StokvelId
            && c.UserId == scenario.Member.Id && c.ContributionCycleId == scenario.CycleId));
        // Replays also obey the limiter: stored key survives another rejection.
        await AssertRejected(await client.PostAsJsonAsync(path, body));
        using (var scope = app.Services.CreateScope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().GetAsync(key));
    }

    [Theory]
    [InlineData("Contributions")]
    [InlineData("Payouts")]
    public async Task FinancialPolicies_IsolateUsersEvenAtTheSameIp(string policy)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using var app = Limited(policy);
        using var first = Client(app, scenario.Treasurer);
        using var second = Client(app, scenario.Chairperson);
        var path = policy == "Payouts"
            ? $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/payouts"
            : $"/api/v1/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions";
        object body = policy == "Payouts" ? new { amount = 500m }
            : new { amount = 500m, contributionCycleId = scenario.CycleId };
        var expectedFirst = policy == "Payouts" ? HttpStatusCode.Created : HttpStatusCode.BadRequest;
        Assert.Equal(expectedFirst, (await first.PostAsJsonAsync(path, body)).StatusCode);
        Assert.Equal(policy == "Payouts" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest,
            (await first.PostAsJsonAsync(path, body)).StatusCode);
        await AssertRejected(await first.PostAsJsonAsync(path, body));
        Assert.Equal(policy == "Payouts" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest,
            (await second.PostAsJsonAsync(path, body)).StatusCode);
        if (policy == "Payouts")
        {
            await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
            Assert.Equal(1, await context.Payouts.CountAsync(p => p.ContributionCycleId == scenario.CycleId));
        }
    }
}
