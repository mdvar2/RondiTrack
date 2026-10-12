using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RondiTrack.Health;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class HealthProbeTests(PostgresApiFactory factory)
{
    private static HttpClient AnonymousClient(PostgresApiFactory host) => host.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthyProbes_AreAnonymousAndReturnExactMinimalBody(string path)
    {
        using var client = AnonymousClient(factory);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HealthRequests_AreExcludedFromExportedHttpTracing()
    {
        var completed = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => completed.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        using var client = AnonymousClient(factory);
        var healthIds = new List<string>();
        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            var id = ActivityTraceId.CreateRandom().ToString();
            healthIds.Add(id);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("traceparent", $"00-{id}-00f067aa0ba902b7-01");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        // ASP.NET creates an activity regardless; the OTel filter marks it unrecorded.
        var probes = completed.Where(activity => healthIds.Contains(activity.TraceId.ToString())).ToArray();
        Assert.Equal(2, probes.Length);
        Assert.All(probes, activity => Assert.False(activity.IsAllDataRequested));
        Assert.All(probes, activity => Assert.False(activity.Recorded));
        var controlId = ActivityTraceId.CreateRandom().ToString();
        using var control = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        control.Headers.Add("traceparent", $"00-{controlId}-00f067aa0ba902b7-01");
        using var controlResponse = await client.SendAsync(control);
        Assert.Equal(HttpStatusCode.Unauthorized, controlResponse.StatusCode);
        Assert.Contains(completed, activity => activity.TraceId.ToString() == controlId && activity.IsAllDataRequested);
    }

    [Fact]
    public async Task IsolatedDatabaseOutage_BoundsReadiness_PreservesLiveness_AndRecovers()
    {
        // This host has its own disposable PostgreSQL; never stop the shared fixture.
        await using var isolated = PostgresApiFactory.CreateForOutage();
        await isolated.InitializeAsync();
        using var client = AnonymousClient(isolated);
        var registration = Assert.Single(isolated.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal(HealthProbes.DatabaseCheckName, registration.Name);
        Assert.Equal(HealthProbes.ReadinessTimeout, registration.Timeout);
        var livenessPredicate = HealthProbes.Options(readiness: false).Predicate;
        Assert.NotNull(livenessPredicate);
        Assert.False(livenessPredicate(registration));

        async Task<double> Measure(string path, HttpStatusCode status, string body)
        {
            var timer = Stopwatch.StartNew();
            using var response = await client.GetAsync(path);
            var actual = await response.Content.ReadAsStringAsync();
            timer.Stop();
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(body, actual);
            return timer.Elapsed.TotalMilliseconds;
        }

        var before = await Measure("/health/ready", HttpStatusCode.OK, "Healthy");
        using var authenticated = isolated.CreateAuthenticatedClient();
        await isolated.StopDatabaseAsync();
        double down, live, uncapped, after;
        try
        {
            live = await Measure("/health/live", HttpStatusCode.OK, "Healthy");
            // A valid token must not cause user-existence queries before liveness.
            client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
            await Measure("/health/live", HttpStatusCode.OK, "Healthy");
            client.DefaultRequestHeaders.Authorization = null;
            down = await Measure("/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");
            Assert.InRange(down, 0, HealthProbes.ReadinessTimeout.TotalMilliseconds + 2000);
            // Test-local baseline only; never remove the deployed timeout.
            registration.Timeout = Timeout.InfiniteTimeSpan;
            try { uncapped = await Measure("/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy"); }
            finally { registration.Timeout = HealthProbes.ReadinessTimeout; }
        }
        finally { await isolated.StartDatabaseAsync(); }
        after = await Measure("/health/ready", HttpStatusCode.OK, "Healthy");
        var evidence = new
        {
            method = "Separate disposable PostgreSQL 17 container stopped and restarted; same API host remained running.",
            timeoutMilliseconds = HealthProbes.ReadinessTimeout.TotalMilliseconds,
            maxRetryCount = 3, maxRetryDelayMilliseconds = 2000,
            beforeMilliseconds = before, outageMilliseconds = down,
            livenessDuringOutageMilliseconds = live, uncappedOutageMilliseconds = uncapped,
            recoveredMilliseconds = after
        };
        var outputDirectory = Environment.GetEnvironmentVariable("RONDITRACK_HEALTH_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "health-outage-timings.json"),
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "HEALTH_TIMINGS_MS before={0:F2} outage={1:F2} live_down={2:F2} uncapped={3:F2} recovered={4:F2}",
            before, down, live, uncapped, after));
    }
}
