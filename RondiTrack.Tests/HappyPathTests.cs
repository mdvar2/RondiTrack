using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class HappyPathTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HappyPathTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_CreateThenGet_ReturnsCreatedUser()
    {
        // Arrange
        var request = new
        {
            name = "Test User",
            email = "test.user@example.com"
        };

        // Act - create the user
        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                request);

        // Assert - creation succeeded
        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createdJson =
            JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

        var userId =
            createdJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Act - retrieve the created user
        var getResponse =
            await _client.GetAsync(
                $"/api/users/{userId}");

        // Assert - retrieval succeeded
        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var userJson =
            JsonDocument.Parse(
                await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Test User",
            userJson.RootElement
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            "test.user@example.com",
            userJson.RootElement
                .GetProperty("email")
                .GetString());
    }

    [Fact]
    public async Task Stokvel_CreateThenGet_ReturnsCreatedStokvel()
    {
        // Arrange
        var request = new
        {
            name = "Test Savings Club",
            contributionAmount = 500m
        };

        // Act - create the stokvel
        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                request);

        // Assert - creation succeeded
        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createdJson =
            JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

        var stokvelId =
            createdJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Act - retrieve the created stokvel
        var getResponse =
            await _client.GetAsync(
                $"/api/stokvels/{stokvelId}");

        // Assert - retrieval succeeded
        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var stokvelJson =
            JsonDocument.Parse(
                await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Test Savings Club",
            stokvelJson.RootElement
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            500m,
            stokvelJson.RootElement
                .GetProperty("contributionAmount")
                .GetDecimal());
    }

    [Fact]
    public async Task ContributionCycle_CreateThenGet_ReturnsCreatedCycle()
    {
        // Arrange - get an existing stokvel
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

        var request = new
        {
            period = "2026-09",
            targetAmount = 1500m
        };

        // Act - create the contribution cycle
        var createResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                request);

        // Assert - creation succeeded
        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createdJson =
            JsonDocument.Parse(
                await createResponse.Content.ReadAsStringAsync());

        var cycleId =
            createdJson.RootElement
                .GetProperty("id")
                .GetGuid();

        // Act - retrieve the created cycle
        var getResponse =
            await _client.GetAsync(
                $"/api/stokvels/{stokvelId}/cycles/{cycleId}");

        // Assert - retrieval succeeded
        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var cycleJson =
            JsonDocument.Parse(
                await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "2026-09",
            cycleJson.RootElement
                .GetProperty("period")
                .GetString());

        Assert.Equal(
            1500m,
            cycleJson.RootElement
                .GetProperty("targetAmount")
                .GetDecimal());
    }

    [Fact]
    public async Task Contribution_Record_ReturnsCreatedContribution()
    {
        // Arrange - get an existing stokvel
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

        // Arrange - get an existing user
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

        // Arrange - add the user as a member
        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        // Arrange - create a contribution cycle
        var cycleRequest = new
        {
            period = "2026-10",
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

        // Act - record the contribution
        var contributionRequest = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions");

        request.Headers.Add(
            "Idempotency-Key",
            "happy-path-contribution-001");

        request.Content =
            JsonContent.Create(contributionRequest);

        var contributionResponse =
            await _client.SendAsync(request);

        // Assert - contribution was created
        Assert.Equal(
            HttpStatusCode.Created,
            contributionResponse.StatusCode);
    }
}


