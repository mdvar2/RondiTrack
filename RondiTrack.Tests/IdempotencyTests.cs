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
        // Arrange - create a dedicated stokvel
        var stokvelRequest = new
        {
            name = $"Same Request Stokvel {Guid.NewGuid()}",
            contributionAmount = 500m
        };

        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                stokvelRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            stokvelResponse.StatusCode);

        using var stokvelJson =
            JsonDocument.Parse(
                await stokvelResponse.Content.ReadAsStringAsync());

        var stokvelId =
            stokvelJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Arrange - create a dedicated user
        var uniqueUserValue =
            Guid.NewGuid();

        var userRequest = new
        {
            name = $"Same Request User {uniqueUserValue}",
            email = $"same.request.{uniqueUserValue}@example.com"
        };

        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                userRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            userResponse.StatusCode);

        using var userJson =
            JsonDocument.Parse(
                await userResponse.Content.ReadAsStringAsync());

        var userId =
            userJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Arrange - create membership
        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        // Arrange - create contribution cycle
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

        var idempotencyKey =
            $"same-request-{Guid.NewGuid()}";

        // Act - first request
        using var firstRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                contributionRequest);

        var firstResponse =
            await _client.SendAsync(
                firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var firstBody =
            await firstResponse.Content.ReadAsStringAsync();

        // Act - exact same request with exact same key
        using var secondRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                contributionRequest);

        var secondResponse =
            await _client.SendAsync(
                secondRequest);

        var secondBody =
            await secondResponse.Content.ReadAsStringAsync();

        // Assert - original result is returned
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
        // Arrange - create a dedicated stokvel
        var stokvelRequest = new
        {
            name = $"Different Request Stokvel {Guid.NewGuid()}",
            contributionAmount = 500m
        };

        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                stokvelRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            stokvelResponse.StatusCode);

        using var stokvelJson =
            JsonDocument.Parse(
                await stokvelResponse.Content.ReadAsStringAsync());

        var stokvelId =
            stokvelJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Arrange - create a dedicated user
        var uniqueUserValue =
            Guid.NewGuid();

        var userRequest = new
        {
            name = $"Different Request User {uniqueUserValue}",
            email = $"different.request.{uniqueUserValue}@example.com"
        };

        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                userRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            userResponse.StatusCode);

        using var userJson =
            JsonDocument.Parse(
                await userResponse.Content.ReadAsStringAsync());

        var userId =
            userJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Arrange - create membership
        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        // Arrange - create contribution cycle
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

        var idempotencyKey =
            $"different-request-{Guid.NewGuid()}";

        var firstPayload = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        // Act - first request
        using var firstRequest =
            CreateContributionRequest(
                stokvelId,
                userId,
                idempotencyKey,
                firstPayload);

        var firstResponse =
            await _client.SendAsync(
                firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        // Act - same key but different payload
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
            await _client.SendAsync(
                secondRequest);

        // Assert
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
            JsonContent.Create(
                payload);

        return request;
    }
}