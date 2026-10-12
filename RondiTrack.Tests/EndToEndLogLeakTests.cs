using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RondiTrack.Auth;
using RondiTrack.Controllers;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class EndToEndLogLeakTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task RegistrationLoginRefreshContributionAndDuplicateRegistration_DoNotLeakSensitiveData()
    {
        Assert.True(string.IsNullOrWhiteSpace(factory.Services.GetRequiredService<IConfiguration>()["OTEL_EXPORTER_OTLP_ENDPOINT"]));
        var nonce = Guid.NewGuid().ToString("N");
        var email = $"popia-{nonce}@tests.example";
        var password = $"Password-{nonce}!";
        var memberName = $"Private member {nonce}";
        var stokvelName = $"Private stokvel {nonce}";
        const decimal amount = 1234.56m;
        var prohibited = new List<string> { email, password, memberName, stokvelName,
            amount.ToString(CultureInfo.InvariantCulture), amount.ToString(CultureInfo.GetCultureInfo("de-DE")) };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        var start = factory.CapturedLogs.Entries.Count;
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new { name = memberName, email, password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var registered = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var userId = registered.RootElement.GetProperty("id").GetGuid();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        async Task<string> CaptureSession(HttpResponseMessage response)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var access = body.RootElement.GetProperty("accessToken").GetString()!;
            var cookie = response.Headers.GetValues("Set-Cookie")
                .Single(value => value.StartsWith(AuthController.RefreshCookieName + "=", StringComparison.Ordinal)).Split(';')[0];
            var raw = Uri.UnescapeDataString(cookie.Split('=', 2)[1]);
            prohibited.Add(access);
            prohibited.Add(raw);
            prohibited.Add(Uri.EscapeDataString(raw));
            prohibited.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
            return cookie;
        }

        var originalCookie = await CaptureSession(login);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", originalCookie);
        using var refresh = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotatedCookie = await CaptureSession(refresh);
        Assert.True(originalCookie != rotatedCookie, "Refresh rotation did not issue a different fixture token.");
        using var accessBody = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            accessBody.RootElement.GetProperty("accessToken").GetString());
        using var stokvelResponse = await client.PostAsJsonAsync("/api/stokvels", new { name = stokvelName, contributionAmount = amount });
        Assert.Equal(HttpStatusCode.Created, stokvelResponse.StatusCode);
        using var stokvelBody = JsonDocument.Parse(await stokvelResponse.Content.ReadAsStringAsync());
        var stokvelId = stokvelBody.RootElement.GetProperty("id").GetGuid();
        using var cycleResponse = await client.PostAsJsonAsync($"/api/stokvels/{stokvelId}/cycles", new { period = "2026-10", targetAmount = amount });
        Assert.Equal(HttpStatusCode.Created, cycleResponse.StatusCode);
        using var cycleBody = JsonDocument.Parse(await cycleResponse.Content.ReadAsStringAsync());
        var cycleId = cycleBody.RootElement.GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var contribution = await client.PostAsJsonAsync($"/api/stokvels/{stokvelId}/members/{userId}/contributions",
            new { amount, contributionCycleId = cycleId });
        Assert.Equal(HttpStatusCode.Created, contribution.StatusCode);
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var persisted = await db.Contributions.SingleAsync(item => item.ContributionCycleId == cycleId && item.UserId == userId);
            Assert.Equal(amount, persisted.Amount);
            prohibited.Add((await db.Users.SingleAsync(item => item.Id == userId)).PasswordHash!);
        }
        var signingKey = factory.Services.GetRequiredService<JwtOptions>().SigningKey;
        prohibited.Add(Convert.ToBase64String(signingKey));
        prohibited.Add(Convert.ToHexString(signingKey));
        var duplicateStart = factory.CapturedLogs.Entries.Count;
        using var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { name = memberName, email, password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var captured = factory.CapturedLogs.Entries.Skip(start).ToArray();
        Assert.NotEmpty(captured);
        Assert.Contains(captured, entry => entry.Category == "RondiTrack.Auth.AuthService" && Equals(entry.Properties.GetValueOrDefault("EventType"), "sign_in"));
        Assert.Contains(captured, entry => entry.Category == "RondiTrack.Auth.AuthService" && Equals(entry.Properties.GetValueOrDefault("EventType"), "refresh"));
        Assert.Contains(captured, entry => entry.Category == "Microsoft.EntityFrameworkCore.Database.Command");
        Assert.Contains(captured, entry => entry.Scopes.Count > 0);
        foreach (var entry in captured)
            Assert.False(LogInspection.ContainsAny(entry, prohibited), "A seeded sensitive fixture value reached captured diagnostics.");
        var duplicateLogs = factory.CapturedLogs.Entries.Skip(duplicateStart).ToArray();
        Assert.NotEmpty(duplicateLogs);
        Assert.DoesNotContain(duplicateLogs, entry => entry.Level >= LogLevel.Error);
        using var problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        var correlation = problem.RootElement.GetProperty("correlationId").GetString();
        Assert.Contains(duplicateLogs, entry => Equals(entry.Properties.GetValueOrDefault("CorrelationId"), correlation)
            && Equals(entry.Properties.GetValueOrDefault("StatusCode"), 409));
    }

    [Fact]
    public void LogCaptureAndLeakInspection_ObserveNestedScopesStateAndExceptionData()
    {
        var provider = new CapturedLogProvider();
        using var loggers = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = loggers.CreateLogger("capture-control");
        using var outer = logger.BeginScope("rendered-scope-control");
        using var inner = logger.BeginScope(new Dictionary<string, object?> { ["ScopedField"] = "structured+scope/control=" });
        var exception = new InvalidOperationException("exception-control");
        exception.Data["DiagnosticField"] = "exception-data-control";
        logger.LogInformation(exception, "Capture {StateField}", "structured-state-control");
        var entry = Assert.Single(provider.Entries);
        Assert.Equal(2, entry.Scopes.Count);
        foreach (var marker in new[] { "rendered-scope-control", "structured+scope/control=", "exception-control", "exception-data-control", "structured-state-control" })
            Assert.True(LogInspection.ContainsAny(entry, [marker]), "Leak detector failed its positive control.");
    }
}
