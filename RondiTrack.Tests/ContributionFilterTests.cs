using System.Net;
using System.Net.Http.Json;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// A cycle's contributions filtered by status and by a date range of two
// instants, half-open. The mobile app asks for "1 November" as South African
// days: [2026-11-01T00:00+02:00, 2026-11-02T00:00+02:00).
[Collection(PostgresCollection.Name)]
public class ContributionFilterTests(PostgresApiFactory factory)
{
    private sealed record Seeded(Guid StokvelId, Guid CycleId, User Treasurer, Dictionary<string, Guid> Ids);

    private sealed record Page(List<Item> Items, string NextPageToken, bool HasMore);

    private sealed record Item(Guid Id, string Status);

    // 00:30 SAST on 1 November is 22:30 UTC on 31 October.
    private static readonly (string Name, DateTime RecordedAtUtc, string Status)[] Rows =
    [
        ("lateOctoberSast", new DateTime(2026, 10, 31, 21, 59, 0, DateTimeKind.Utc), "Paid"),
        ("halfPastMidnightSast", new DateTime(2026, 10, 31, 22, 30, 0, DateTimeKind.Utc), "Confirmed"),
        ("noonFirst", new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc), "Paid"),
        ("midnightSecondSast", new DateTime(2026, 11, 1, 22, 0, 0, DateTimeKind.Utc), "Pending"),
    ];

    private async Task<Seeded> CreateAsync()
    {
        var stokvel = new Stokvel($"Filters {Guid.NewGuid()}", 500m);
        var cycle = new ContributionCycle(stokvel.Id, "2026-11", 2000m);
        var users = Rows.Select((_, i) => new User($"Member {i}", $"{Guid.NewGuid()}@tests.example")).ToList();
        var ids = new Dictionary<string, Guid>();

        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Users.AddRange(users);
        context.Stokvels.Add(stokvel);
        context.ContributionCycles.Add(cycle);
        for (var i = 0; i < Rows.Length; i++)
        {
            context.StokvelMembers.Add(new StokvelMember(stokvel.Id, users[i].Id, i == 0 ? Roles.Treasurer : Roles.Member));
            var contribution = new Contribution(stokvel.Id, users[i].Id, cycle.Id, 500m);
            context.Contributions.Add(contribution);
            context.Entry(contribution).Property(c => c.RecordedAtUtc).CurrentValue = Rows[i].RecordedAtUtc;
            context.Entry(contribution).Property(c => c.Status).CurrentValue = Rows[i].Status;
            ids[Rows[i].Name] = contribution.Id;
        }
        await context.SaveChangesAsync();
        return new(stokvel.Id, cycle.Id, users[0], ids);
    }

    private static string Url(Seeded s, string query) =>
        $"/api/v1/stokvels/{s.StokvelId}/cycles/{s.CycleId}/contributions?{query}";

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Page>())!.Items.Select(i => i.Id).ToList();
    }

    [Fact]
    public async Task ASouthAfricanDay_IsHalfOpen_AndHolds0030OnTheFirst()
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);
        var from = Uri.EscapeDataString("2026-11-01T00:00:00+02:00");
        var before = Uri.EscapeDataString("2026-11-02T00:00:00+02:00");

        var ids = await IdsAsync(client, Url(s, $"recordedFrom={from}&recordedBefore={before}&sortDirection=asc"));

        Assert.Equal([s.Ids["halfPastMidnightSast"], s.Ids["noonFirst"]], ids);
    }

    [Fact]
    public async Task TheSameDayInUtc_IsADifferentDay()
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);
        var from = Uri.EscapeDataString("2026-11-01T00:00:00Z");
        var before = Uri.EscapeDataString("2026-11-02T00:00:00Z");

        var ids = await IdsAsync(client, Url(s, $"recordedFrom={from}&recordedBefore={before}&sortDirection=asc"));

        Assert.Equal([s.Ids["noonFirst"], s.Ids["midnightSecondSast"]], ids);
    }

    [Fact]
    public async Task Status_Filters()
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);

        var ids = await IdsAsync(client, Url(s, "status=Paid&sortDirection=asc"));

        Assert.Equal([s.Ids["lateOctoberSast"], s.Ids["noonFirst"]], ids);
    }

    [Fact]
    public async Task APageToken_WithOtherFilters_Is400()
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);
        var first = await client.GetFromJsonAsync<Page>(Url(s, "pageSize=1&status=Paid"));
        Assert.True(first!.HasMore);

        var response = await client.GetAsync(
            Url(s, $"pageSize=1&status=Confirmed&pageToken={Uri.EscapeDataString(first.NextPageToken)}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FilteredPages_AllLoad_AndEnd()
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);
        var from = Uri.EscapeDataString("2026-10-31T00:00:00+02:00");
        var query = $"pageSize=1&recordedFrom={from}&status=Paid";
        var seen = new List<Guid>();
        var url = Url(s, query);
        for (var i = 0; i < 5; i++)
        {
            var page = (await client.GetFromJsonAsync<Page>(url))!;
            seen.AddRange(page.Items.Select(item => item.Id));
            if (!page.HasMore) break;
            url = Url(s, $"{query}&pageToken={Uri.EscapeDataString(page.NextPageToken)}");
        }

        Assert.Equal(2, seen.Count);
    }

    [Theory]
    [InlineData("status=Late")]
    [InlineData("recordedFrom=2026-11-02T00:00:00%2B02:00&recordedBefore=2026-11-01T00:00:00%2B02:00")]
    public async Task ABadFilter_Is400(string query)
    {
        var s = await CreateAsync();
        using var client = factory.CreateClientFor(s.Treasurer);

        var response = await client.GetAsync(Url(s, query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
