using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class NegativePathTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NegativePathTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MalformedRequest_Returns400ProblemJson()
    {
        // Arrange - create a dedicated stokvel
        var stokvelRequest = new
        {
            name = $"Malformed Request Stokvel {Guid.NewGuid()}",
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

        var invalidJson = """
        {
          "period": "2026-09",
          "targetAmount": "hello"
        }
        """;

        var content =
            new StringContent(
                invalidJson,
                Encoding.UTF8,
                "application/json");

        // Act
        var response =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                content);

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

        Assert.True(
            problem.RootElement
                .TryGetProperty(
                    "correlationId",
                    out _));
    }

    [Fact]
    public async Task NonexistentUser_Returns404ProblemJson()
    {
        // Arrange
        var missingUserId =
            Guid.NewGuid();

        // Act
        var response =
            await _client.GetAsync(
                $"/api/users/{missingUserId}");

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var problem =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            404,
            problem.RootElement
                .GetProperty("status")
                .GetInt32());

        Assert.Equal(
            "User not found.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());

        Assert.True(
            problem.RootElement
                .TryGetProperty(
                    "correlationId",
                    out _));
    }

    [Fact]
    public async Task DuplicateMembership_Returns409ProblemJson()
    {
        // Arrange - create a dedicated stokvel
        var stokvelRequest = new
        {
            name = $"Duplicate Membership Stokvel {Guid.NewGuid()}",
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
            name = $"Duplicate Membership User {uniqueUserValue}",
            email = $"duplicate.membership.{uniqueUserValue}@example.com"
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

        // Act - first membership should succeed
        var firstResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            firstResponse.StatusCode);

        // Act - same membership again should fail
        var secondResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

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
            409,
            problem.RootElement
                .GetProperty("status")
                .GetInt32());

        Assert.Equal(
            "User is already a member of this stokvel.",
            problem.RootElement
                .GetProperty("detail")
                .GetString());

        Assert.True(
            problem.RootElement
                .TryGetProperty(
                    "correlationId",
                    out _));
    }
}