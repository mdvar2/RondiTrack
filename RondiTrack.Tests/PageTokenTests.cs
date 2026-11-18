using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Following a page token to the next page. Before this fix, every second
// page sorted by a date was a 500: the token's round-trip "O" timestamp was
// parsed back with DateTime.TryParse, which makes it Kind=Local, and Npgsql
// refuses a local DateTime for a timestamptz. Amounts were written and read
// in the server's culture, so a comma-decimal culture broke amount tokens.
[Collection(PostgresCollection.Name)]
public class PageTokenTests(PostgresApiFactory factory)
{
    private sealed record Paging(Guid StokvelId, Guid CycleId, User Treasurer);

    private sealed record Page<T>(List<T> Items, string NextPageToken, int PageSize, bool HasMore);

    private sealed record ContributionItem(Guid Id, long Amount);

    private sealed record MemberItem(Guid UserId);

    // Three members, each with one contribution recorded a day apart.
    private async Task<Paging> CreateAsync()
    {
        var stokvel = new Stokvel($"Paging {Guid.NewGuid()}", 500m);
        var cycle = new ContributionCycle(stokvel.Id, "2026-11", 1500m);
        var users = Enumerable.Range(1, 3)
            .Select(i => new User($"Member {i}", $"{Guid.NewGuid()}@tests.example")).ToList();

        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Users.AddRange(users);
        context.Stokvels.Add(stokvel);
        context.ContributionCycles.Add(cycle);
        foreach (var (user, i) in users.Select((user, i) => (user, i)))
        {
            context.StokvelMembers.Add(new StokvelMember(stokvel.Id, user.Id, i == 0 ? Roles.Treasurer : Roles.Member));
            var contribution = new Contribution(stokvel.Id, user.Id, cycle.Id, 100.25m * (i + 1));
            context.Contributions.Add(contribution);
            context.Entry(contribution).Property(c => c.RecordedAtUtc).CurrentValue =
                new DateTime(2026, 11, 1 + i, 8, 0, 0, DateTimeKind.Utc);
        }
        await context.SaveChangesAsync();
        return new(stokvel.Id, cycle.Id, users[0]);
    }

    // Follows nextPageToken until the end marker, asserting each page is 200.
    private static async Task<List<Page<T>>> AllPagesAsync<T>(HttpClient client, string firstUrl)
    {
        var pages = new List<Page<T>>();
        var url = firstUrl;
        while (true)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = (await response.Content.ReadFromJsonAsync<Page<T>>())!;
            pages.Add(page);
            if (!page.HasMore) return pages;
            Assert.True(pages.Count < 10, "The pages never ended.");
            url = $"{firstUrl}&pageToken={Uri.EscapeDataString(page.NextPageToken)}";
        }
    }

    [Theory]
    [InlineData("recordedAtUtc", "desc")]
    [InlineData("recordedAtUtc", "asc")]
    [InlineData("amount", "desc")]
    public async Task Contributions_EveryPageLoads_AndTheLastEndsWithAnEmptyToken(string sortBy, string direction)
    {
        var paging = await CreateAsync();
        using var client = factory.CreateClientFor(paging.Treasurer);

        var pages = await AllPagesAsync<ContributionItem>(client,
            $"/api/v1/stokvels/{paging.StokvelId}/cycles/{paging.CycleId}/contributions" +
            $"?pageSize=1&sortBy={sortBy}&sortDirection={direction}");

        Assert.Equal(3, pages.Count);
        Assert.Equal(3, pages.SelectMany(page => page.Items).Select(item => item.Id).Distinct().Count());
        // The end marker: an empty string, never null or missing.
        Assert.Equal("", pages[^1].NextPageToken);
        Assert.All(pages[..^1], page => Assert.NotEqual("", page.NextPageToken));
    }

    [Fact]
    public async Task Contributions_AmountTokens_DoNotDependOnTheServersCulture()
    {
        var paging = await CreateAsync();
        using var client = factory.CreateClientFor(paging.Treasurer);
        var previous = CultureInfo.CurrentCulture;
        // A decimal comma, as on a South African Windows machine.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var pages = await AllPagesAsync<ContributionItem>(client,
                $"/api/v1/stokvels/{paging.StokvelId}/cycles/{paging.CycleId}/contributions" +
                "?pageSize=1&sortBy=amount&sortDirection=asc");

            Assert.Equal([10025L, 20050L, 30075L], pages.SelectMany(page => page.Items).Select(item => item.Amount));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Members_EveryPageLoads_AndTheLastEndsWithAnEmptyToken()
    {
        var paging = await CreateAsync();
        using var client = factory.CreateClientFor(paging.Treasurer);

        var pages = await AllPagesAsync<MemberItem>(client,
            $"/api/v1/stokvels/{paging.StokvelId}/members?pageSize=1");

        Assert.Equal(3, pages.Count);
        Assert.Equal("", pages[^1].NextPageToken);
    }
}
