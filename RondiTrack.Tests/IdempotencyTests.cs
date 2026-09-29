using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class IdempotencyTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IdempotencyTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SameKeyAndSameRequest_ReturnsSameContribution()
    {
        var stokvelsResponse =
            await _client.GetAsync("/api/stokvels");

        stokvelsResponse.EnsureSuccessStatusCode();

        using var stokvelsJson =
            JsonDocument.Parse(
                await stokvelsResponse.Content.ReadAsStringAsync());

        var stokvelId =
            stokvelsJson.RootElement[0]
                .GetProperty("id")
                .GetGuid();

        var usersResponse =
            await _client.GetAsync("/api/users");

        usersResponse.EnsureSuccessStatusCode();

        using var usersJson =
            JsonDocument.Parse(
                await usersResponse.Content.ReadAsStringAsync());

        var userId =
            usersJson.RootElement[0]
                .GetProperty("id")
                .GetGuid();

        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        var cycleRequest = new
        {
            period = "2026-11",
            targetAmount = 500m
        };

        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                cycleRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            cycleResponse.StatusCode);

        using var cycleJson =
            JsonDocument.Parse(
                await cycleResponse.Content.ReadAsStringAsync());

        var cycleId =
            cycleJson.RootElement
                .GetProperty("id")
                .GetGuid();

        var contributionRequest = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        const string idempotencyKey =
            "same-request-test-001";

        using var firstRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                contributionRequest);

        var firstResponse =
            await _client.SendAsync(firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var firstBody =
            await firstResponse.Content.ReadAsStringAsync();

        using var secondRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                contributionRequest);

        var secondResponse =
            await _client.SendAsync(secondRequest);

        var secondBody =
            await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(
            firstResponse.StatusCode,
            secondResponse.StatusCode);

        Assert.Equal(
            firstBody,
            secondBody);
    }

    [Fact]
    public async Task SameKeyAndDifferentRequest_Returns409Conflict()
    {
        var stokvelsResponse =
            await _client.GetAsync("/api/stokvels");

        stokvelsResponse.EnsureSuccessStatusCode();

        using var stokvelsJson =
            JsonDocument.Parse(
                await stokvelsResponse.Content.ReadAsStringAsync());

        var stokvelId =
            stokvelsJson.RootElement[1]
                .GetProperty("id")
                .GetGuid();

        var usersResponse =
            await _client.GetAsync("/api/users");

        usersResponse.EnsureSuccessStatusCode();

        using var usersJson =
            JsonDocument.Parse(
                await usersResponse.Content.ReadAsStringAsync());

        var userId =
            usersJson.RootElement[1]
                .GetProperty("id")
                .GetGuid();

        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        var cycleRequest = new
        {
            period = "2026-12",
            targetAmount = 500m
        };

        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                cycleRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            cycleResponse.StatusCode);

        using var cycleJson =
            JsonDocument.Parse(
                await cycleResponse.Content.ReadAsStringAsync());

        var cycleId =
            cycleJson.RootElement
                .GetProperty("id")
                .GetGuid();

        const string idempotencyKey =
            "different-request-test-001";

        var firstPayload = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        using var firstRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                firstPayload);

        var firstResponse =
            await _client.SendAsync(firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var differentPayload = new
        {
            amount = 600m,
            contributionCycleId = cycleId
        };

        using var secondRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                differentPayload);

        var secondResponse =
            await _client.SendAsync(secondRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);

        Assert.Equal(
            "application/problem+json",
            secondResponse.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Idempotency-Key was already used with a different request.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());
    }

    private static HttpRequestMessage CreateContributionRequest(
        Guid stokvelId,
        Guid userId,
        string idempotencyKey,
        object payload)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions");

        request.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        request.Content =
            JsonContent.Create(payload);

        return request;
    }
}