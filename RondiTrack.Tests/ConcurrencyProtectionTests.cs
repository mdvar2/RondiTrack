using RondiTrack.Tests.TestSupport;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class ConcurrencyProtectionTests
{
    private readonly PostgresApiFactory _factory;

    private readonly HttpClient _client;

    public ConcurrencyProtectionTests(
        PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task SaveChanges_WhenTwoContextsModifySamePayout_ThrowsDbUpdateConcurrencyException()
    {
        var options = _factory.CreateDbContextOptions();

        var user = new User(
            $"Concurrency User {Guid.NewGuid()}",
            $"{Guid.NewGuid()}@example.com");

        var stokvel = new Stokvel(
            $"Concurrency Stokvel {Guid.NewGuid()}",
            2000m);

        var cycle = new ContributionCycle(
            stokvel.Id,
            "2026-12",
            2000m);

        var member = new StokvelMember(
            stokvel.Id,
            user.Id);

        var payout = new Payout(
            stokvel.Id,
            user.Id,
            cycle.Id,
            2000m);

        await using (var setupContext = new RondiTrackDbContext(options))
        {
            await setupContext.Users.AddAsync(user);
            await setupContext.Stokvels.AddAsync(stokvel);
            await setupContext.ContributionCycles.AddAsync(cycle);
            await setupContext.StokvelMembers.AddAsync(member);
            await setupContext.Payouts.AddAsync(payout);
            await setupContext.SaveChangesAsync();
        }

        // Two independent DbContext instances are required so each one sees a different
        // version snapshot of the same row; a single context can never produce this conflict.
        await using var firstContext = new RondiTrackDbContext(options);
        var firstPayout = await firstContext.Payouts
            .SingleAsync(row => row.Id == payout.Id);

        await using var secondContext = new RondiTrackDbContext(options);
        var secondPayout = await secondContext.Payouts
            .SingleAsync(row => row.Id == payout.Id);

        firstPayout.UpdateAmount(2500m);
        await firstContext.SaveChangesAsync();

        secondPayout.UpdateAmount(2600m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () => await secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task User_Update_WithStaleIfMatchHeader_Returns412PreconditionFailed()
    {
        var createRequest = new
        {
            name = $"ETag User {Guid.NewGuid()}",
            email = $"etag.{Guid.NewGuid()}@example.com"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/users",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var userId = System.Text.Json.JsonDocument.Parse(createdJson)
            .RootElement
            .GetProperty("id")
            .GetGuid();

        var getResponse = await _client.GetAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var etag = getResponse.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(etag));

        var updateRequest = new
        {
            name = $"Updated ETAG name {Guid.NewGuid()}",
            email = $"updated.{Guid.NewGuid()}@example.com"
        };

        var firstUpdateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/users/{userId}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        firstUpdateRequest.Headers.Add("If-Match", etag);

        var firstUpdate = await _client.SendAsync(firstUpdateRequest);
        Assert.Equal(HttpStatusCode.NoContent, firstUpdate.StatusCode);

        var staleUpdateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/users/{userId}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        staleUpdateRequest.Headers.Add("If-Match", etag);

        var staleUpdate = await _client.SendAsync(staleUpdateRequest);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleUpdate.StatusCode);
    }
}
