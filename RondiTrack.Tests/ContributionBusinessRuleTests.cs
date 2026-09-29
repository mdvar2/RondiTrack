using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class ContributionBusinessRuleTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ContributionBusinessRuleTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task NonMemberContribution_Returns409Conflict()
    {
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Non Member Test Stokvel",
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

        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                new
                {
                    name = "Non Member User",
                    email = "nonmember@test.com"
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

        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                new
                {
                    period = "2027-01",
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
            "non-member-test-001");

        request.Content =
            JsonContent.Create(contributionRequest);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "User is not a member of this stokvel.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());
    }

    [Fact]
    public async Task CycleFromDifferentStokvel_Returns409Conflict()
    {
        var firstStokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Contribution Test Stokvel",
                    contributionAmount = 500m
                });

        Assert.Equal(
            HttpStatusCode.Created,
            firstStokvelResponse.StatusCode);

        using var firstStokvelJson =
            JsonDocument.Parse(
                await firstStokvelResponse.Content.ReadAsStringAsync());

        var firstStokvelId =
            firstStokvelJson.RootElement
                .GetProperty("id")
                .GetGuid();

        var secondStokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Different Cycle Stokvel",
                    contributionAmount = 500m
                });

        Assert.Equal(
            HttpStatusCode.Created,
            secondStokvelResponse.StatusCode);

        using var secondStokvelJson =
            JsonDocument.Parse(
                await secondStokvelResponse.Content.ReadAsStringAsync());

        var secondStokvelId =
            secondStokvelJson.RootElement
                .GetProperty("id")
                .GetGuid();

        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                new
                {
                    name = "Different Cycle User",
                    email = "differentcycle@test.com"
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

        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{firstStokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{secondStokvelId}/cycles",
                new
                {
                    period = "2027-02",
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

        var contributionRequest = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{firstStokvelId}/members/{userId}/contributions");

        request.Headers.Add(
            "Idempotency-Key",
            "wrong-cycle-stokvel-test-001");

        request.Content =
            JsonContent.Create(contributionRequest);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Contribution cycle does not belong to this stokvel.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());
    }

    [Fact]
    public async Task DuplicateContributionWithDifferentKey_Returns409Conflict()
    {
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Duplicate Contribution Stokvel",
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

        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                new
                {
                    name = "Duplicate Contribution User",
                    email = "duplicatecontribution@test.com"
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

        var membershipResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            membershipResponse.StatusCode);

        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                new
                {
                    period = "2027-03",
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

        var contributionRequest = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "duplicate-contribution-key-001");

        firstRequest.Content =
            JsonContent.Create(contributionRequest);

        var firstResponse =
            await _client.SendAsync(firstRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "duplicate-contribution-key-002");

        secondRequest.Content =
            JsonContent.Create(contributionRequest);

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
            "A contribution already exists for this member and cycle.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());
    }

    [Fact]
    public async Task MissingIdempotencyKey_Returns400BadRequest()
    {
        // Arrange - create isolated stokvel
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Missing Key Test Stokvel",
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

        // Arrange - create isolated user
        var userResponse =
            await _client.PostAsJsonAsync(
                "/api/users",
                new
                {
                    name = "Missing Key User",
                    email = "missingkey@test.com"
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

        // Arrange - create a cycle
        var cycleResponse =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                new
                {
                    period = "2027-04",
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

        var contributionRequest = new
        {
            amount = 500m,
            contributionCycleId = cycleId
        };

        // Act - deliberately send NO Idempotency-Key header
        var response =
            await _client.PostAsJsonAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}/contributions",
                contributionRequest);

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
            "Idempotency-Key header is required.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());
    }
}