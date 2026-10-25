using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class ErrorContractTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task FluentValidation_ReturnsCamelCaseFieldErrorsAndCorrelation()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/stokvels", new { name = "", contributionAmount = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.NotEmpty(errors.GetProperty("name").EnumerateArray());
        Assert.NotEmpty(errors.GetProperty("contributionAmount").EnumerateArray());
        Assert.False(errors.TryGetProperty("Name", out _));
        Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("correlationId").GetString()!);
    }

    [Theory]
    [InlineData("{\"name\":\"Strict\",\"contributionAmount\":\"500\"}")]
    [InlineData("{\"name\":\"Strict\",\"contributionAmount\":true}")]
    [InlineData("{\"name\":")]
    public async Task InvalidJson_Is400WithoutInternalTypeNames(string body)
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/v1/stokvels", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("RondiTrack", text);
        Assert.DoesNotContain("System.", text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("The request body is invalid.", json.RootElement.GetProperty("detail").GetString());
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
    }

    [Theory]
    [InlineData("/api/v1/unknown", "GET", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/me/home", "POST", HttpStatusCode.MethodNotAllowed)]
    public async Task RoutingErrors_UseProblemDetails(string path, string method, HttpStatusCode status)
    {
        using var client = factory.CreateAuthenticatedClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("detail", out _));
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task UnsupportedBodyContentType_UsesSafeProblemContract()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/api/v1/stokvels", new StringContent("unsupported", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("about:blank", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("The request content type is not supported.", json.RootElement.GetProperty("detail").GetString());
        Assert.Equal("/api/v1/stokvels", json.RootElement.GetProperty("instance").GetString());
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
        Assert.False(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task OpenApi_DescribesActualIntegerAndDecimalPropertiesAsNumericOnly()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal("number", schemas.GetProperty("CreateStokvelRequest").GetProperty("properties")
            .GetProperty("contributionAmount").GetProperty("type").GetString());
        Assert.Equal("integer", schemas.GetProperty("UserResponse").GetProperty("properties")
            .GetProperty("version").GetProperty("type").GetString());
        Assert.Equal("integer", schemas.GetProperty("RecordContributionRequest").GetProperty("properties")
            .GetProperty("amountInCents").GetProperty("type").GetString());
        foreach (var schema in schemas.EnumerateObject()) AssertNumericUnionsAbsent(schema.Value);
    }

    private static void AssertNumericUnionsAbsent(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array)
            {
                var types = type.EnumerateArray().Select(item => item.GetString()).ToArray();
                if (types.Contains("number") || types.Contains("integer")) Assert.DoesNotContain("string", types);
            }
            foreach (var property in element.EnumerateObject()) AssertNumericUnionsAbsent(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) AssertNumericUnionsAbsent(item);
    }
}
