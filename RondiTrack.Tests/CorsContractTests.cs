using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class CorsContractTests(PostgresApiFactory factory)
{
    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Preflight_AllowsOnlyConfiguredOriginWithoutToken(string origin, bool allowed)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Options, $"/api/v1/stokvels/{Guid.NewGuid()}/members/{Guid.NewGuid()}/contributions");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key,traceparent");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        if (!allowed)
        {
            Assert.DoesNotContain(response.Headers, header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
            return;
        }
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("idempotency-key", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("*", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task ScopedErrors_AreReadableByFrontend(bool member, HttpStatusCode expected)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(member ? scenario.Member : scenario.Outsider);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");
        var response = await client.PostAsJsonAsync($"/api/v1/stokvels/{scenario.StokvelId}/cycles",
            new { period = "2026-11", targetAmount = 500 });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("correlationId", await response.Content.ReadAsStringAsync());
    }
}
