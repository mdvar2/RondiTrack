using System.Net;
using System.Net.Http.Json;
using RondiTrack.Domain.Entities;
using RondiTrack.DTOs.Home;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class Assignment72ArrearsTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Home_ReturnsOnlySignedInUsersArrears_InCents()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using (var context = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            context.ContributionArrears.AddRange(
                new ContributionArrear(scenario.StokvelId, scenario.Member.Id, scenario.CycleId, 15000),
                new ContributionArrear(scenario.StokvelId, scenario.Treasurer.Id, scenario.CycleId, 98000));
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClientFor(scenario.Member);
        var response = await client.GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var home = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(home);
        var stokvel = Assert.Single(home.Stokvels, item => item.StokvelId == scenario.StokvelId);
        Assert.Equal(15000L, stokvel.ArrearsCents);
    }

    [Fact]
    public async Task Home_WithNoArrears_ReturnsZero()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        var response = await client.GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var home = await response.Content.ReadFromJsonAsync<HomeResponse>();
        Assert.NotNull(home);
        Assert.Equal(0L, Assert.Single(home.Stokvels).ArrearsCents);
    }
}
