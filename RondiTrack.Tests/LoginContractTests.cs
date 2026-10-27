using System.Net;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 8.1: the Next.js server generates its types from this document.
// Login listed only its 429, so the generated success body was `undefined`.
[Collection(PostgresCollection.Name)]
public class LoginContractTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task OpenApi_LoginDescribesItsSuccessAndFailureBodies()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responses = json.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/login")
            .GetProperty("post")
            .GetProperty("responses");

        Assert.EndsWith("/AccessTokenResponse", SchemaRef(responses, "200"));
        Assert.EndsWith("/ProblemDetails", SchemaRef(responses, "401"));
        Assert.EndsWith("/ProblemDetails", SchemaRef(responses, "429"));
    }

    private static string SchemaRef(JsonElement responses, string status) =>
        responses.GetProperty(status)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString()!;
}
