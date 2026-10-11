using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class SecureDefaultsTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task NoToken_Returns401ProblemResponse()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", problem.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Documentation_RemainsAnonymous(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }
}
