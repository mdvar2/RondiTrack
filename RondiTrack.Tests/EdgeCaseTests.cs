using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class EdgeCaseTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EdgeCaseTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ZeroContributionAmount_Returns400BadRequest()
    {
        // Arrange - create an isolated stokvel
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Zero Contribution Test Stokvel",
                    contributionAmount = 500m
                });

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

        // Arrange - create an isolated user
        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                new
                {
                    name = "Zero Contribution User",
                    email = "zerocontribution@test.com"
                });

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

        // Arrange - create a valid contribution cycle
        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                new
                {
                    period = "2027-05",
                    targetAmount = 500m
                });

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

        // Edge case:
        // zero is a decimal value, but it is not a valid
        // contribution amount according to the validator.
        var contributionRequest = new
        {
            amount = 0m,
            contributionCycleId = cycleId
        };

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions");

        request.Headers.Add(
            "Idempotency-Key",
            "zero-contribution-test-001");

        request.Content =
            JsonContent.Create(contributionRequest);

        // Act
        var response =
            await _client.SendAsync(request);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            400,
            problem.RootElement
                .GetProperty("status")
                .GetInt32());

        var detail =
            problem.RootElement
                .GetProperty("detail")
                .GetString();

        Assert.Contains(
            "Contribution amount must be greater than zero.",
            detail);
    }

    [Fact]
    public async Task InvalidContributionCyclePeriod_Returns400BadRequest()
    {
        // Arrange - create an isolated stokvel
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Invalid Period Test Stokvel",
                    contributionAmount = 500m
                });

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

        // Edge case:
        // this looks like YYYY-MM,
        // but month 13 is not a valid calendar month.
        var cycleRequest = new
        {
            period = "2027-13",
            targetAmount = 500m
        };

        // Act
        var response =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                cycleRequest);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            400,
            problem.RootElement
                .GetProperty("status")
                .GetInt32());

        var detail =
            problem.RootElement
                .GetProperty("detail")
                .GetString();

        Assert.Contains(
            "Period must use the YYYY-MM format.",
            detail);
    }
}