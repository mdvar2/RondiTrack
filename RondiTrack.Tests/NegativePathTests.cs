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

        var invalidJson = """
        {
          "period": "2026-09",
          "targetAmount": "hello"
        }
        """;

        var content = new StringContent(
            invalidJson,
            Encoding.UTF8,
            "application/json");

        var response =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/cycles",
                content);

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
        var missingUserId =
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111");

        var response =
            await _client.GetAsync(
                $"/api/users/{missingUserId}");

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

        var firstResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            firstResponse.StatusCode);

        var secondResponse =
            await _client.PostAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}",
                null);

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