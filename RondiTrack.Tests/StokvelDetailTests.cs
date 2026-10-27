using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RondiTrack.Auth;
using RondiTrack.Domain.Entities;
using RondiTrack.DTOs.Stokvels;
using RondiTrack.DTOs.Users;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 8.1: GET /stokvels/{id} must fit the Next.js detail page, and
// GET /me must say who is signed in without any role.
[Collection(PostgresCollection.Name)]
public class StokvelDetailTests(PostgresApiFactory factory)
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset October =
        new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private HttpClient ClientAt(DateTimeOffset now, User user)
    {
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(now));
        }));

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            app.Services.GetRequiredService<TokenService>().CreateAccessToken(user).AccessToken);

        return client;
    }

    [Fact]
    public async Task Member_GetsMemberCountRoleAndCurrentCycleSummary()
    {
        var scenario = await AuthScenario.CreateAsync(factory);

        // Arrange: two of the three members have paid into the October cycle
        await using (var context = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            context.Contributions.AddRange(
                new Contribution(scenario.StokvelId, scenario.Member.Id, scenario.CycleId, 125.10m),
                new Contribution(scenario.StokvelId, scenario.Treasurer.Id, scenario.CycleId, 500m));
            await context.SaveChangesAsync();
        }

        using var client = ClientAt(October, scenario.Member);

        var response = await client.GetAsync($"/api/v1/stokvels/{scenario.StokvelId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<StokvelDetailResponse>())!;

        Assert.Equal(scenario.StokvelId, detail.Id);
        Assert.Equal(3, detail.MemberCount);
        Assert.Equal(Roles.Member, detail.Role);
        Assert.Equal(500m, detail.ContributionAmount);
        Assert.Equal(50000L, detail.ContributionAmountCents);
        Assert.Equal(
            new StokvelCycleSummaryResponse("2026-10", "Open", null, 2, 62510L, 50000L),
            detail.CurrentCycle);
        Assert.Equal($"\"{detail.Version}\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Role_IsTheCallersOwnRoleInThisStokvel()
    {
        var scenario = await AuthScenario.CreateAsync(factory);

        using var treasurer = ClientAt(October, scenario.Treasurer);
        using var chairperson = ClientAt(October, scenario.Chairperson);

        var asTreasurer = await treasurer.GetFromJsonAsync<StokvelDetailResponse>(
            $"/api/v1/stokvels/{scenario.StokvelId}");
        var asChairperson = await chairperson.GetFromJsonAsync<StokvelDetailResponse>(
            $"/api/v1/stokvels/{scenario.StokvelId}");

        Assert.Equal(Roles.Treasurer, asTreasurer!.Role);
        Assert.Equal(Roles.Chairperson, asChairperson!.Role);
    }

    [Fact]
    public async Task NoCycleInCurrentMonth_ReturnsNullCurrentCycle()
    {
        var scenario = await AuthScenario.CreateAsync(factory);

        // The scenario only has a 2026-10 cycle.
        using var client = ClientAt(new DateTimeOffset(2026, 12, 2, 12, 0, 0, TimeSpan.Zero), scenario.Member);

        var detail = await client.GetFromJsonAsync<StokvelDetailResponse>(
            $"/api/v1/stokvels/{scenario.StokvelId}");

        Assert.Equal(3, detail!.MemberCount);
        Assert.Null(detail.CurrentCycle);
    }

    [Fact]
    public async Task ExistingFields_KeepTheirNamesForExistingCallers()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = ClientAt(October, scenario.Member);

        using var json = JsonDocument.Parse(
            await client.GetStringAsync($"/api/v1/stokvels/{scenario.StokvelId}"));

        foreach (var name in new[] { "id", "name", "contributionAmount", "memberCount", "version" })
            Assert.True(json.RootElement.TryGetProperty(name, out _), $"missing {name}");
    }

    [Fact]
    public async Task NonMemberAndUnknownId_GetTheSameNotFound()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = ClientAt(October, scenario.Outsider);

        var existing = await client.GetAsync($"/api/v1/stokvels/{scenario.StokvelId}");
        var unknown = await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        using var existingJson = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
        using var unknownJson = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        Assert.Equal(
            unknownJson.RootElement.GetProperty("detail").GetString(),
            existingJson.RootElement.GetProperty("detail").GetString());
        Assert.False(existingJson.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Me_ReturnsTheCallerWithoutARole()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = ClientAt(October, scenario.Treasurer);

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(scenario.Treasurer.Id, me.Id);
        Assert.Equal(scenario.Treasurer.Email, me.Email);
        Assert.Equal(scenario.Treasurer.Name, me.Name);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            json.RootElement.EnumerateObject(),
            property => property.Name.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
