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
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task TwoMemberships_ReturnOwnRolesAndCurrentMonthTotalsWithoutOtherMembersMoney()
    {
        var scenario = await AuthScenario.CreateAsync(factory);

        var second = new Stokvel(
            $"Second {Guid.NewGuid()}",
            700m);

        var missing = new Stokvel(
            $"No current cycle {Guid.NewGuid()}",
            800m);

        var secondCycle = new ContributionCycle(
            second.Id,
            "2026-10",
            1400m);

        var oldCycle = new ContributionCycle(
            scenario.StokvelId,
            "2026-09",
            500m);

        // Arrange: create stokvel memberships and contributions
        await using (var context = new RondiTrackDbContext(
            factory.CreateDbContextOptions()))
        {
            context.Stokvels.AddRange(second, missing);

            context.StokvelMembers.AddRange(
                new StokvelMember(
                    second.Id,
                    scenario.Member.Id,
                    Roles.Treasurer),

                new StokvelMember(
                    missing.Id,
                    scenario.Member.Id)
            );

            context.ContributionCycles.AddRange(
                secondCycle,
                oldCycle);

            context.Contributions.AddRange(
                new Contribution(
                    scenario.StokvelId,
                    scenario.Member.Id,
                    scenario.CycleId,
                    125.10m),

                new Contribution(
                    scenario.StokvelId,
                    scenario.Treasurer.Id,
                    scenario.CycleId,
                    499m),

                new Contribution(
                    scenario.StokvelId,
                    scenario.Member.Id,
                    oldCycle.Id,
                    500m),

                new Contribution(
                    second.Id,
                    scenario.Member.Id,
                    secondCycle.Id,
                    725m)
            );

            secondCycle.MarkPaidOut();

            await context.SaveChangesAsync();
        }

        // Arrange: use a fixed clock for predictable results
        await using var app = factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();

                services.AddSingleton<TimeProvider>(
                    new FixedClock());
            }));

        using var client = app.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                app.Services
                    .GetRequiredService<TokenService>()
                    .CreateAccessToken(scenario.Member)
                    .AccessToken);

        // Act: request the member's home dashboard
        var response = await client.GetAsync("/api/v1/me/home");

        // Assert: successful response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var home = (await response.Content
            .ReadFromJsonAsync<HomeResponse>())!;

        Assert.Equal(3, home.Stokvels.Count);

        // Verify the first stokvel
        var first = Assert.Single(
            home.Stokvels,
            stokvel =>
                stokvel.Role == Roles.Member &&
                stokvel.CurrentCycle is not null);

        Assert.Equal(12510L, first.AmountPaid);
        Assert.Equal(37490L, first.AmountOwed);

        Assert.Equal(
            new HomeCycleResponse("2026-10", "Open", null),
            first.CurrentCycle);

        // Verify the second stokvel
        var other = Assert.Single(
            home.Stokvels,
            stokvel => stokvel.StokvelName == second.Name);

        Assert.Equal(Roles.Treasurer, other.Role);
        Assert.Equal(72500L, other.AmountPaid);
        Assert.Equal(0L, other.AmountOwed);
        Assert.Equal("PaidOut", other.CurrentCycle!.Status);

        // Verify the stokvel without a current cycle
        var absent = Assert.Single(
            home.Stokvels,
            stokvel => stokvel.StokvelName == missing.Name);

        Assert.Null(absent.CurrentCycle);
        Assert.Equal(0L, absent.AmountPaid);
        Assert.Equal(0L, absent.AmountOwed);

        // No payout dates should be available
        Assert.All(
            home.Stokvels,
            stokvel => Assert.Null(stokvel.NextPayoutDate));

        // Verify the JSON response structure
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Single(json.RootElement.EnumerateObject());

        Assert.All(
            json.RootElement
                .GetProperty("stokvels")
                .EnumerateArray(),
            stokvel =>
                Assert.Equal(
                    6,
                    stokvel.EnumerateObject().Count()));

        // Verify that another member's payment is not
        // exposed in the current member's financial totals.
        // Check financial fields rather than the entire JSON,
        // because randomly generated GUIDs may contain "499".
        Assert.DoesNotContain(
            home.Stokvels,
            stokvel =>
                stokvel.AmountPaid == 49900L ||
                stokvel.AmountOwed == 49900L);
    }

    [Fact]
    public async Task NoMemberships_ReturnsEmptyList()
    {
        var scenario = await AuthScenario.CreateAsync(factory);

        using var client = factory.CreateClientFor(
            scenario.Outsider);

        var response = await client.GetAsync("/api/v1/me/home");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Empty(
            (await response.Content
                .ReadFromJsonAsync<HomeResponse>())!
                .Stokvels);
    }

    [Fact]
    public async Task AnonymousHome_IsUnauthorized()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var response = await client.GetAsync("/api/v1/me/home");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
