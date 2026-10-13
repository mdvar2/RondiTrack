using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RondiTrack.Auth;
using RondiTrack.Domain.Entities;
using RondiTrack.DTOs.Home;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class MemberHomeTests(PostgresApiFactory factory)
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task TwoMemberships_ReturnOwnRolesAndCurrentMonthTotalsWithoutOtherMembersMoney()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var second = new Stokvel($"Second {Guid.NewGuid()}", 700m);
        var missing = new Stokvel($"No current cycle {Guid.NewGuid()}", 800m);
        var secondCycle = new ContributionCycle(second.Id, "2026-10", 1400m);
        var oldCycle = new ContributionCycle(scenario.StokvelId, "2026-09", 500m);
        await using (var context = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            context.Stokvels.AddRange(second, missing);
            context.StokvelMembers.AddRange(new StokvelMember(second.Id, scenario.Member.Id, Roles.Treasurer),
                new StokvelMember(missing.Id, scenario.Member.Id));
            context.ContributionCycles.AddRange(secondCycle, oldCycle);
            context.Contributions.AddRange(new Contribution(scenario.StokvelId, scenario.Member.Id, scenario.CycleId, 125.10m),
                new Contribution(scenario.StokvelId, scenario.Treasurer.Id, scenario.CycleId, 499m),
                new Contribution(scenario.StokvelId, scenario.Member.Id, oldCycle.Id, 500m),
                new Contribution(second.Id, scenario.Member.Id, secondCycle.Id, 725m));
            secondCycle.MarkPaidOut();
            await context.SaveChangesAsync();
        }
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        }));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            app.Services.GetRequiredService<TokenService>().CreateAccessToken(scenario.Member).AccessToken);
        var response = await client.GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var home = (await response.Content.ReadFromJsonAsync<HomeResponse>())!;
        Assert.Equal(3, home.Stokvels.Count);
        var first = Assert.Single(home.Stokvels, stokvel => stokvel.Role == Roles.Member && stokvel.CurrentCycle is not null);
        Assert.Equal("125.1", first.AmountPaid);
        Assert.Equal("374.9", first.AmountOwed);
        Assert.Equal(new HomeCycleResponse("2026-10", "Open"), first.CurrentCycle);
        var other = Assert.Single(home.Stokvels, stokvel => stokvel.StokvelName == second.Name);
        Assert.Equal(Roles.Treasurer, other.Role);
        Assert.Equal("725", other.AmountPaid);
        Assert.Equal("0", other.AmountOwed);
        Assert.Equal("PaidOut", other.CurrentCycle!.Status);
        var absent = Assert.Single(home.Stokvels, stokvel => stokvel.StokvelName == missing.Name);
        Assert.Null(absent.CurrentCycle);
        Assert.Equal("0", absent.AmountPaid);
        Assert.Equal("0", absent.AmountOwed);
        Assert.All(home.Stokvels, stokvel => Assert.Null(stokvel.NextPayoutDate));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.All(json.RootElement.GetProperty("stokvels").EnumerateArray(), stokvel =>
            Assert.Equal(6, stokvel.EnumerateObject().Count()));
        Assert.DoesNotContain("499", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NoMemberships_ReturnsEmptyList()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Outsider);
        var response = await client.GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<HomeResponse>())!.Stokvels);
    }

    [Fact]
    public async Task AnonymousHome_IsUnauthorized()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/home")).StatusCode);
    }
}
