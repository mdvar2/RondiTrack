using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RondiTrack.Tests;

public class MembershipBusinessRuleTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MembershipBusinessRuleTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RemoveNonMember_Returns409Conflict()
    {
        // Arrange - create an isolated stokvel
        var stokvelResponse =
            await _client.PostAsJsonAsync(
                "/api/stokvels",
                new
                {
                    name = "Remove Non Member Test Stokvel",
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
                    name = "Remove Non Member User",
                    email = "removenonmember@test.com"
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

        // Important:
        // The user is deliberately NOT added to the stokvel.

        // Act
        var response =
            await _client.DeleteAsync(
                $"/api/stokvels/{stokvelId}/members/{userId}");

        // Assert
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
}