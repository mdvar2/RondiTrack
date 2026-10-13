using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RondiTrack.Handlers;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class CorrelationTests(PostgresApiFactory factory)
{
    private static string AddTrace(HttpClient client)
    {
        var id = ActivityTraceId.CreateRandom().ToString();
        client.DefaultRequestHeaders.Add("traceparent", $"00-{id}-00f067aa0ba902b7-01");
        return id;
    }

    private async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status,
        string? expectedTrace = null, string category = "RondiTrack.Handlers.GlobalExceptionHandler")
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        var id = json.RootElement.GetProperty("correlationId").GetString()!;
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.NotEqual(new string('0', 32), id);
        if (expectedTrace is not null) Assert.Equal(expectedTrace, id);
        Assert.Contains(factory.CorrelationLogs.Entries,
            entry => entry.Category == category && entry.CorrelationId == id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Anonymous401_HasW3cCorrelationAndMatchingLog(bool supplyTrace)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var id = supplyTrace ? AddTrace(client) : null;
        using var response = await client.GetAsync("/api/v1/users");
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, id, "RondiTrack.ProblemDetails");
    }

    [Fact]
    public async Task Framework403_PreservesCallerTraceAndLogsIt()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        var id = AddTrace(client);
        using var response = await client.GetAsync("/api/v1/users");
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, id, "RondiTrack.ProblemDetails");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonMember404_PreservesCallerTrace_IncludingPlatformAdmin(bool admin)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(admin ? scenario.Admin : scenario.Outsider);
        var id = AddTrace(client);
        using var response = await client.GetAsync($"/api/v1/stokvels/{scenario.StokvelId}");
        await AssertProblemAsync(response, HttpStatusCode.NotFound, id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginFailures_PreserveCallerTraceAndGenericDetail(bool knownAccount)
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@tests.example";
        if (knownAccount)
        {
            using var registration = await client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "Correlation fixture", email, password = Guid.NewGuid().ToString("N") });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        }
        var id = AddTrace(client);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "incorrect-password" });
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, id);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Email or password is incorrect.", json.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("incorrect-password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Application404_WithoutTraceparent_GetsIndependentW3cIds()
    {
        using var client = factory.CreateAuthenticatedClient();
        using var first = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");
        using var second = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");
        await AssertProblemAsync(first, HttpStatusCode.NotFound);
        await AssertProblemAsync(second, HttpStatusCode.NotFound);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.NotEqual(firstJson.RootElement.GetProperty("correlationId").GetString(),
            secondJson.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvalidBinding400_DoesNotEchoSubmittedValue_AndPreservesTrace()
    {
        using var client = factory.CreateAuthenticatedClient();
        var id = AddTrace(client);
        const string marker = "private-submitted-value";
        using var body = new StringContent("{\"contributionAmount\":\"" + marker + "\",\"name\":\"Fixture\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/stokvels", body);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, id);
        Assert.DoesNotContain(marker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Framework401_WithNonJsonAccept_StillReturnsCorrelatedProblem()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var id = AddTrace(client);
        using var response = await client.GetAsync("/api/v1/users");
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, id, "RondiTrack.ProblemDetails");
    }

    [Fact]
    public async Task Framework415_PreservesTraceThroughProblemDetailsCustomization()
    {
        using var client = factory.CreateAuthenticatedClient();
        var id = AddTrace(client);
        using var body = new StringContent("unsupported body", Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync("/api/v1/stokvels", body);
        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, id, "RondiTrack.ProblemDetails");
    }

    [Fact]
    public async Task Member403_PreservesTraceThroughApplicationHandler()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        var id = AddTrace(client);
        using var response = await client.DeleteAsync($"/api/v1/stokvels/{scenario.StokvelId}");
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, id);
    }

    [Fact]
    public async Task DuplicateRegistration409_PreservesTraceAndSafeDetail()
    {
        using var client = factory.CreateClient();
        var registration = new { name = "Correlation fixture", email = $"{Guid.NewGuid()}@tests.example", password = Guid.NewGuid().ToString("N") };
        using var created = await client.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = AddTrace(client);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", registration);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, id);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(registration.email, body);
        Assert.DoesNotContain(registration.password, body);
    }

    [Fact]
    public async Task Unexpected500_ReturnsGenericDetailAndMatchingStructuredCorrelation()
    {
        using var activity = new Activity("handler-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var logs = new CorrelationLogProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var handler = new GlobalExceptionHandler(loggerFactory.CreateLogger<GlobalExceptionHandler>());
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        Assert.True(await handler.TryHandleAsync(context,
            new InvalidOperationException("private-internal-diagnostic"), CancellationToken.None));
        Assert.Equal(500, context.Response.StatusCode);
        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(body);
        Assert.Equal("An unexpected error occurred.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(activity.TraceId.ToString(), problem.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("private-internal-diagnostic", problem.RootElement.GetRawText());
        Assert.Contains(logs.Entries, entry => entry.CorrelationId == activity.TraceId.ToString());
    }
}
