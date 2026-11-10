using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.BackgroundJobs;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Observability;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// Assignment 9.3 Part 2: the API continues the caller's trace, a retry with the
// same Idempotency-Key is visible in its own trace as a replay that writes
// nothing, spans carry no member data, and each cycle-close run is its own trace.
[Collection(PostgresCollection.Name)]
public class DistributedTracingTests(PostgresApiFactory factory)
{
    private sealed class SpanCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public ConcurrentQueue<Activity> Spans { get; } = new();

        public SpanCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name is RondiTrackTelemetry.SourceName or "Npgsql" or "Microsoft.AspNetCore",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Spans.Enqueue
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public Activity[] InTrace(ActivityTraceId traceId) => Spans.Where(span => span.TraceId == traceId).ToArray();

        public void Dispose() => _listener.Dispose();
    }

    private static (string Header, ActivityTraceId TraceId) NewTraceParent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        return ($"00-{traceId}-{ActivitySpanId.CreateRandom()}-01", traceId);
    }

    private static IEnumerable<string> SqlText(IEnumerable<Activity> spans) =>
        spans.Where(span => span.Source.Name == "Npgsql")
            .SelectMany(span => span.TagObjects)
            .Where(tag => tag.Key is "db.query.text" or "db.statement")
            .Select(tag => tag.Value as string ?? string.Empty);

    private static bool InsertsContribution(string sql) =>
        sql.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)
        && sql.Contains("\"Contributions\"", StringComparison.Ordinal);

    private async Task<(AuthScenario Scenario, User Payer)> ScenarioWithNamedPayerAsync(string name)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var payer = new User(name, $"privacy-probe-{Guid.NewGuid():N}@tests.example");
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        db.Users.Add(payer);
        db.StokvelMembers.Add(new StokvelMember(scenario.StokvelId, payer.Id, Roles.Member));
        await db.SaveChangesAsync();
        return (scenario, payer);
    }

    private static HttpRequestMessage Contribution(AuthScenario scenario, User payer, string key, long cents, string traceParent)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/stokvels/{scenario.StokvelId}/members/{payer.Id}/contributions")
        {
            Content = JsonContent.Create(new { amountInCents = cents, contributionCycleId = scenario.CycleId })
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("traceparent", traceParent);
        return request;
    }

    [Fact]
    public async Task RetryWithSameKey_EachRequestContinuesItsCallersTrace_OnlyTheFirstWrites()
    {
        var (scenario, payer) = await ScenarioWithNamedPayerAsync("Thandiwe Tracecheck");
        using var client = factory.CreateClientFor(scenario.Treasurer);
        var key = Guid.NewGuid().ToString();
        var (firstHeader, firstTrace) = NewTraceParent();
        var (retryHeader, retryTrace) = NewTraceParent();
        using var capture = new SpanCapture();

        using var first = await client.SendAsync(Contribution(scenario, payer, key, 123457, firstHeader));
        using var retry = await client.SendAsync(Contribution(scenario, payer, key, 123457, retryHeader));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());

        // The API joined each caller's trace instead of starting its own.
        Assert.Single(capture.InTrace(firstTrace), span => span.Kind == ActivityKind.Server);
        Assert.Single(capture.InTrace(retryTrace), span => span.Kind == ActivityKind.Server);

        var recorded = Assert.Single(capture.InTrace(firstTrace), span => span.OperationName == RondiTrackTelemetry.ContributionRecordSpan);
        var replayed = Assert.Single(capture.InTrace(retryTrace), span => span.OperationName == RondiTrackTelemetry.ContributionRecordSpan);
        Assert.Equal(RondiTrackTelemetry.OutcomeRecorded, recorded.GetTagItem(RondiTrackTelemetry.IdempotencyOutcomeTag));
        Assert.Equal(RondiTrackTelemetry.OutcomeReplayed, replayed.GetTagItem(RondiTrackTelemetry.IdempotencyOutcomeTag));

        // The database spans are in the same traces, and only the first inserts.
        Assert.Contains(SqlText(capture.InTrace(firstTrace)), InsertsContribution);
        Assert.NotEmpty(SqlText(capture.InTrace(retryTrace)));
        Assert.DoesNotContain(SqlText(capture.InTrace(retryTrace)), sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));

        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.Contributions.CountAsync(c => c.UserId == payer.Id && c.ContributionCycleId == scenario.CycleId));
    }

    [Fact]
    public async Task ContributionTraces_CarryNoMemberDataAmountsOrKeys()
    {
        const string name = "Nomvula Privacyprobe";
        var (scenario, payer) = await ScenarioWithNamedPayerAsync(name);
        using var client = factory.CreateClientFor(scenario.Treasurer);
        var key = $"privacy-key-{Guid.NewGuid():N}";
        var (header, traceId) = NewTraceParent();
        using var capture = new SpanCapture();

        using var response = await client.SendAsync(Contribution(scenario, payer, key, 987654, header));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var spans = capture.InTrace(traceId);
        Assert.NotEmpty(spans);
        var text = string.Join("\n", spans.Select(span =>
            span.DisplayName + " " + string.Join(" ", span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
            + " " + string.Join(" ", span.Baggage.Select(item => $"{item.Key}={item.Value}"))));
        foreach (var forbidden in new[] { "Nomvula", "Privacyprobe", payer.Email, "987654", "9876.54", "9 876,54", key, "Bearer" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        Assert.All(spans, span => Assert.Empty(span.Baggage));

        // No infrastructure identity beyond the standard database attributes.
        Assert.Contains(spans, span => span.Source.Name == "Npgsql");
        foreach (var removed in TraceHygiene.DatabaseDetails)
            Assert.DoesNotContain(spans, span => span.TagObjects.Any(tag => tag.Key == removed));
        Assert.DoesNotContain("Username=", text, StringComparison.OrdinalIgnoreCase);
        // Connection-string form only: SQL text legitimately names the "PasswordHash" column.
        Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CycleCloseRun_IsItsOwnRootTrace_WithTheJohannesburgDateInTagAndLog()
    {
        using var capture = new SpanCapture();
        using var testSource = new ActivitySource("RondiTrack.Tests.Ambient");
        using var ambientListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == testSource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(ambientListener);
        var johannesburg = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        string LocalDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, johannesburg).DateTime)
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var before = LocalDate();

        using (var ambient = testSource.StartActivity("ambient-test-work"))
        {
            Assert.NotNull(ambient);
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CycleCloseProcessor>().RunOnceAsync();

            // The ambient activity is restored after the run.
            Assert.Same(ambient, Activity.Current);
            var run = Assert.Single(capture.Spans, span => span.OperationName == RondiTrackTelemetry.CycleCloseRunSpan);
            Assert.Null(run.ParentId);
            Assert.Equal(default, run.ParentSpanId);
            Assert.NotEqual(ambient.TraceId, run.TraceId);
            Assert.Equal("Africa/Johannesburg", run.GetTagItem(RondiTrackTelemetry.JobTimeZoneTag));
            var tagged = Assert.IsType<string>(run.GetTagItem(RondiTrackTelemetry.JobLocalDateTag));
            Assert.Contains(tagged, new[] { before, LocalDate() });

            // Every stokvel this run processed is a child span of the same trace.
            Assert.All(capture.Spans.Where(span => span.OperationName == "stokvel.cycle_close"),
                span => Assert.Equal(run.TraceId, span.TraceId));

            var log = Assert.Single(factory.CapturedLogs.Entries, entry =>
                entry.Category == typeof(CycleCloseProcessor).FullName
                && Equals(entry.Properties.GetValueOrDefault("TraceId"), run.TraceId.ToString()));
            Assert.Equal(tagged, log.Properties["LocalDate"]);
            Assert.Contains($"Johannesburg date {tagged}", log.Message);
        }
    }
}
