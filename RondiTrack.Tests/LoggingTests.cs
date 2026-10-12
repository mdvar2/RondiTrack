using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RondiTrack.Controllers;
using RondiTrack.Auth;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Handlers;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class LoggingTests(PostgresApiFactory factory)
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static string SetTrace(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("traceparent");
        var id = ActivityTraceId.CreateRandom().ToString();
        client.DefaultRequestHeaders.Add("traceparent", $"00-{id}-00f067aa0ba902b7-01");
        return id;
    }

    private CapturedLog[] Logs(string id) => factory.CapturedLogs.Entries
        .Where(entry => Equals(entry.Properties.GetValueOrDefault("CorrelationId"), id)).ToArray();

    private static void AssertNoLeaks(IEnumerable<CapturedLog> entries, params string[] prohibited)
    {
        foreach (var entry in entries)
        {
            var text = entry.Message + string.Join(" ", entry.Properties.Select(pair => pair.Key + "=" + pair.Value))
                + entry.Exception?.ToString();
            // Boolean assertions avoid printing secret values or raw log output on failure.
            Assert.True(prohibited.All(value => !text.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "Captured diagnostic data contained a prohibited fixture value.");
        }
    }

    private async Task AssertExpectedError(HttpResponseMessage response, HttpStatusCode status, string id, string type, int baseline)
    {
        Assert.Equal(status, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, problem.RootElement.GetProperty("correlationId").GetString());
        var logs = Logs(id);
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, entry => entry.Level >= LogLevel.Error);
        Assert.DoesNotContain(factory.CapturedLogs.Entries.Skip(baseline), entry => entry.Level >= LogLevel.Error);
        var handler = Assert.Single(logs, entry => entry.Category == "RondiTrack.Handlers.GlobalExceptionHandler");
        Assert.Equal(LogLevel.Information, handler.Level);
        Assert.Equal(type, handler.Properties["ExceptionType"]);
        Assert.Equal((int)status, handler.Properties["StatusCode"]);
        Assert.Null(handler.Exception);
        Assert.All(logs, entry => Assert.Null(entry.Exception));
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task FrameworkRefusals_AreInformationWithMatchingCorrelation(bool authenticated, HttpStatusCode status)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = authenticated ? factory.CreateClientFor(scenario.Member) : Client();
        var id = SetTrace(client);
        var baseline = factory.CapturedLogs.Entries.Count;
        using var response = await client.GetAsync("/api/users");
        Assert.Equal(status, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, problem.RootElement.GetProperty("correlationId").GetString());
        var entry = Assert.Single(Logs(id), log => log.Category == "RondiTrack.ProblemDetails");
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal((int)status, entry.Properties["StatusCode"]);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Logs(id), log => log.Level >= LogLevel.Error);
        Assert.DoesNotContain(factory.CapturedLogs.Entries.Skip(baseline), log => log.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task UnknownAccount_FailedSignInDoesNotRetainTypedEmail()
    {
        using var client = Client();
        var email = $"{Guid.NewGuid()}@tests.example";
        const string password = "unknown-account-fixture";
        var baseline = factory.CapturedLogs.Entries.Count;
        var id = SetTrace(client);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        await AssertExpectedError(response, HttpStatusCode.Unauthorized, id, nameof(AuthenticationFailedException), baseline);
        var entry = Assert.Single(Logs(id), log => Equals(log.Properties.GetValueOrDefault("EventType"), "sign_in"));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("failure", entry.Properties["Outcome"]);
        AssertNoLeaks(factory.CapturedLogs.Entries.Skip(baseline), email, password);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignIn_UsesSafeEventFieldsAndInformationLevel(bool success)
    {
        using var client = Client();
        var email = $"{Guid.NewGuid()}@tests.example";
        var password = Guid.NewGuid().ToString("N");
        var baseline = factory.CapturedLogs.Entries.Count;
        using var registered = await client.PostAsJsonAsync("/api/auth/register", new { name = "Logging fixture", email, password });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        const string wrong = "wrong-password-fixture";
        var id = SetTrace(client);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = success ? password : wrong });
        Assert.Equal(success ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(Logs(id), log => Equals(log.Properties.GetValueOrDefault("EventType"), "sign_in"));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(success ? "success" : "failure", entry.Properties["Outcome"]);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(entry.Properties.Keys, key => key is "UserId" or "FamilyId" or "Email" or "Password");
        if (!success) await AssertExpectedError(response, HttpStatusCode.Unauthorized, id, nameof(AuthenticationFailedException), baseline);
        var excluded = new List<string> { email, password, wrong };
        var signingKey = factory.Services.GetRequiredService<JwtOptions>().SigningKey;
        excluded.Add(Convert.ToBase64String(signingKey));
        excluded.Add(Convert.ToHexString(signingKey));
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
            excluded.Add((await db.Users.SingleAsync(user => user.Email == email)).PasswordHash!);
        if (success)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            excluded.Add(json.RootElement.GetProperty("accessToken").GetString()!);
            var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
            var raw = Uri.UnescapeDataString(cookie.Split('=', 2)[1]);
            excluded.Add(raw);
            excluded.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
        }
        AssertNoLeaks(factory.CapturedLogs.Entries.Skip(baseline), excluded.ToArray());
    }

    [Fact]
    public async Task RefreshReuse_IsWarning_WithCorrelationAndNoSessionIdentifiers()
    {
        using var client = Client();
        var email = $"{Guid.NewGuid()}@tests.example";
        var password = Guid.NewGuid().ToString("N");
        var baseline = factory.CapturedLogs.Entries.Count;
        using var registered = await client.PostAsJsonAsync("/api/auth/register", new { name = "Logging fixture", email, password });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        async Task<HttpResponseMessage> Refresh()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            request.Headers.Add("Cookie", cookie);
            return await client.SendAsync(request);
        }
        SetTrace(client);
        using var rotation = await Refresh();
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        var id = SetTrace(client);
        using var reuse = await Refresh();
        await AssertExpectedError(reuse, HttpStatusCode.Unauthorized, id, nameof(AuthenticationFailedException), baseline);
        var entry = Assert.Single(Logs(id), log => Equals(log.Properties.GetValueOrDefault("EventType"), "refresh_reuse"));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain(entry.Properties.Keys, key => key is "UserId" or "FamilyId" or "TokenHash");
        var excluded = new List<string> { email, password };
        foreach (var response in new[] { login, rotation })
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            excluded.Add(json.RootElement.GetProperty("accessToken").GetString()!);
            var raw = Uri.UnescapeDataString(response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1]);
            excluded.Add(raw);
            excluded.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
        }
        AssertNoLeaks(factory.CapturedLogs.Entries.Skip(baseline), excluded.ToArray());
    }

    [Theory]
    [InlineData(true, "not_found", HttpStatusCode.NotFound, nameof(NotFoundException))]
    [InlineData(false, "forbidden", HttpStatusCode.Forbidden, nameof(ForbiddenException))]
    public async Task StokvelRefusal_LogsOnlySafeResultAtInformation(bool outsider, string result, HttpStatusCode status, string type)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(outsider ? scenario.Outsider : scenario.Member);
        var id = SetTrace(client);
        var baseline = factory.CapturedLogs.Entries.Count;
        using var response = await client.PostAsJsonAsync(
            $"/api/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/payouts", new { amount = 500m });
        await AssertExpectedError(response, status, id, type, baseline);
        var entry = Assert.Single(Logs(id), log => Equals(log.Properties.GetValueOrDefault("EventType"), "stokvel_refusal"));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(result, entry.Properties["Result"]);
        AssertNoLeaks(Logs(id), scenario.StokvelId.ToString(), scenario.Member.Id.ToString(), scenario.Outsider.Id.ToString());
    }

    [Fact]
    public async Task DuplicateRegistration409_DoesNotLogMessageOrExceptionAtError()
    {
        using var client = Client();
        var body = new { name = "Logging fixture", email = $"{Guid.NewGuid()}@tests.example", password = Guid.NewGuid().ToString("N") };
        using var first = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = SetTrace(client);
        var baseline = factory.CapturedLogs.Entries.Count;
        using var duplicate = await client.PostAsJsonAsync("/api/auth/register", body);
        await AssertExpectedError(duplicate, HttpStatusCode.Conflict, id, nameof(BusinessRuleException), baseline);
        var logs = factory.CapturedLogs.Entries.Skip(baseline).ToArray();
        Assert.DoesNotContain(logs, entry => entry.Level >= LogLevel.Error);
        AssertNoLeaks(logs, body.email, body.password, "An account with this email already exists.");
    }

    [Fact]
    public async Task Unexpected500_KeepsFullExceptionInternally_AndGenericClientDetail()
    {
        using var activity = new Activity("logging-handler-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var provider = new CapturedLogProvider();
        using var loggers = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var handler = new GlobalExceptionHandler(loggers.CreateLogger<GlobalExceptionHandler>());
        var context = new DefaultHttpContext();
        using var stream = new MemoryStream();
        context.Response.Body = stream;
        Exception exception;
        try { throw new InvalidOperationException("Synthetic diagnostic failure."); }
        catch (InvalidOperationException failure) { exception = failure; }
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        var entry = Assert.Single(provider.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.NotNull(entry.Exception!.StackTrace);
        Assert.Equal(500, entry.Properties["StatusCode"]);
        Assert.Equal(activity.TraceId.ToString(), entry.Properties["CorrelationId"]);
        stream.Position = 0;
        using var problem = await JsonDocument.ParseAsync(stream);
        Assert.Equal("An unexpected error occurred.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(activity.TraceId.ToString(), problem.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain(exception.Message, problem.RootElement.GetRawText());
    }

    [Fact]
    public async Task NpgsqlSpansAndEfLogs_DoNotContainSubmittedParameterValues()
    {
        var spans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span => spans.Enqueue(span)
        };
        ActivitySource.AddActivityListener(listener);
        using var client = Client();
        var email = $"{Guid.NewGuid()}@tests.example";
        var password = Guid.NewGuid().ToString("N");
        var baseline = factory.CapturedLogs.Entries.Count;
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { name = "Logging fixture", email, password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var id = SetTrace(client);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var raw = Uri.UnescapeDataString(login.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1]);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var queries = spans.Where(span => span.TraceId.ToString() == id).ToArray();
        Assert.NotEmpty(queries);
        foreach (var span in queries)
        {
            var text = span.DisplayName + string.Join(" ", span.TagObjects.Select(tag => tag.Key + "=" + tag.Value))
                + string.Join(" ", span.Events.Select(item => item.Name + string.Join(" ", item.Tags.Select(tag => tag.Key + "=" + tag.Value))));
            Assert.True(new[] { email, password, raw, hash }.All(value => !text.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "Npgsql activity contained a prohibited fixture value.");
            Assert.DoesNotContain(span.TagObjects, tag => tag.Key.StartsWith("db.query.parameter", StringComparison.Ordinal));
        }
        AssertNoLeaks(factory.CapturedLogs.Entries.Skip(baseline), email, password, raw, hash);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var settings = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.False(settings.LogParameters);
        Assert.False(settings.IncludeErrorDetail);
    }

    [Fact]
    public async Task FinancialParameterValue_IsAbsentFromNpgsqlTagsAndCapturedLogs()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var spans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span => spans.Enqueue(span)
        };
        ActivitySource.AddActivityListener(listener);
        using var client = factory.CreateClientFor(scenario.Member);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var id = SetTrace(client);
        const decimal amount = 9876.54m;
        var marker = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var baseline = factory.CapturedLogs.Entries.Count;
        using var response = await client.PostAsJsonAsync(
            $"/api/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions",
            new { amount, contributionCycleId = scenario.CycleId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var queries = spans.Where(span => span.TraceId.ToString() == id).ToArray();
        Assert.NotEmpty(queries);
        foreach (var span in queries)
        {
            var text = string.Join(" ", span.TagObjects.Select(tag => tag.Key + "=" + tag.Value))
                + string.Join(" ", span.Events.Select(item => item.Name + string.Join(" ", item.Tags.Select(tag => tag.Key + "=" + tag.Value))));
            Assert.True(!text.Contains(marker, StringComparison.Ordinal), "Financial fixture value reached database tracing.");
        }
        AssertNoLeaks(factory.CapturedLogs.Entries.Skip(baseline), marker);
    }
}
