using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Observability;
using RondiTrack.Services;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class BusinessTelemetryTests(PostgresApiFactory factory)
{
    private sealed class Capture : IDisposable
    {
        public ConcurrentQueue<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> Measurements { get; } = new();
        public ConcurrentQueue<Activity> Activities { get; } = new();
        private readonly MeterListener _metrics = new();
        private readonly ActivityListener _traces;

        public Capture()
        {
            _metrics.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == RondiTrackTelemetry.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Measurements.Enqueue((instrument.Name, value, tags.ToArray())));
            _metrics.Start();
            _traces = new ActivityListener
            {
                ShouldListenTo = source => source.Name is RondiTrackTelemetry.SourceName or "Npgsql" or "Microsoft.AspNetCore",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_traces);
        }

        public void AssertMeasurement(string name, string? key = null, string? value = null)
        {
            var measurement = Assert.Single(Measurements, item => item.Name == name);
            Assert.Equal(1, measurement.Value);
            if (key is null) Assert.Empty(measurement.Tags);
            else Assert.Equal(new KeyValuePair<string, object?>(key, value), Assert.Single(measurement.Tags));
        }

        public void Dispose() { _metrics.Dispose(); _traces.Dispose(); }
    }

    private static void AssertSafePayout(Activity span, string outcome)
    {
        Assert.Equal(RondiTrackTelemetry.SourceName, span.Source.Name);
        Assert.Equal("payout.process", span.OperationName);
        Assert.Equal(new KeyValuePair<string, object?>("outcome", outcome), Assert.Single(span.TagObjects));
        Assert.Empty(span.Events);
        Assert.Null(span.StatusDescription);
    }

    [Fact]
    public async Task SuccessfulPayout_CountsOnceAfterCommit_AndNestsSqlUnderBusinessSpan()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Treasurer);
        var traceId = ActivityTraceId.CreateRandom().ToString();
        client.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");
        // Check the SDK subscription before the test listener can create activities itself.
        Assert.True(factory.Services.GetRequiredService<RondiTrackTelemetry>().Activities.HasListeners());
        using var capture = new Capture();
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/payouts", new { amount = 500m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        capture.AssertMeasurement(RondiTrackTelemetry.PayoutsName, "outcome", "success");
        var payout = Assert.Single(capture.Activities, span => span.Source.Name == RondiTrackTelemetry.SourceName);
        AssertSafePayout(payout, "success");
        Assert.NotEqual(ActivityStatusCode.Error, payout.Status);
        Assert.Equal(traceId, payout.TraceId.ToString());
        var request = Assert.Single(capture.Activities, span => span.Kind == ActivityKind.Server && span.TraceId == payout.TraceId);
        Assert.Equal(request.SpanId, payout.ParentSpanId);
        var sql = capture.Activities.Where(span => span.Source.Name == "Npgsql" && span.ParentSpanId == payout.SpanId).ToArray();
        Assert.NotEmpty(sql);
        Assert.All(sql, span => Assert.Equal(payout.TraceId, span.TraceId));
        var payoutCommands = capture.Activities.Where(span => span.Source.Name == "Npgsql"
            && span.TraceId == payout.TraceId && span.TagObjects.Any(tag =>
                tag.Key is "db.query.text" or "db.statement" && tag.Value is string command
                && (command.Contains("\"Payouts\"", StringComparison.Ordinal)
                    || command.Contains("\"ContributionCycles\"", StringComparison.Ordinal)))).ToArray();
        Assert.NotEmpty(payoutCommands);
        Assert.All(payoutCommands, span => Assert.Equal(payout.SpanId, span.ParentSpanId));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.Payouts.CountAsync(item => item.ContributionCycleId == scenario.CycleId));
        Assert.Equal("PaidOut", (await db.ContributionCycles.SingleAsync(item => item.Id == scenario.CycleId)).Status);
    }

    [Fact]
    public async Task FailedPayout_CountsFailureOnce_AndMarksSafeSpanError()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Treasurer);
        using var capture = new Capture();
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{Guid.NewGuid()}/payouts", new { amount = 500m });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        capture.AssertMeasurement(RondiTrackTelemetry.PayoutsName, "outcome", "failure");
        var payout = Assert.Single(capture.Activities, span => span.Source.Name == RondiTrackTelemetry.SourceName);
        AssertSafePayout(payout, "failure");
        Assert.Equal(ActivityStatusCode.Error, payout.Status);
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.False(await db.Payouts.AnyAsync(item => item.StokvelId == scenario.StokvelId));
    }

    [Fact]
    public async Task ContributionPersistence_CountsOnce_AndIdempotentReplayDoesNotCount()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(scenario.Member);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var body = new { amount = 500m, contributionCycleId = scenario.CycleId };
        using var capture = new Capture();
        var path = $"/api/v1/stokvels/{scenario.StokvelId}/members/{scenario.Member.Id}/contributions";
        using var first = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        capture.AssertMeasurement(RondiTrackTelemetry.ContributionsName);
        using var replay = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        capture.AssertMeasurement(RondiTrackTelemetry.ContributionsName);
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.Contributions.CountAsync(item => item.ContributionCycleId == scenario.CycleId));
    }

    [Theory]
    [InlineData(true, "not_found", HttpStatusCode.NotFound)]
    [InlineData(false, "forbidden", HttpStatusCode.Forbidden)]
    public async Task AuthorizationRefusal_HasOnlyBoundedResult(bool outsider, string result, HttpStatusCode status)
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var client = factory.CreateClientFor(outsider ? scenario.Outsider : scenario.Member);
        using var capture = new Capture();
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/payouts", new { amount = 500m });
        Assert.Equal(status, response.StatusCode);
        capture.AssertMeasurement(RondiTrackTelemetry.RefusalsName, "result", result);
        Assert.DoesNotContain(capture.Measurements, item => item.Name == RondiTrackTelemetry.PayoutsName);
        Assert.DoesNotContain(capture.Activities, span => span.Source.Name == RondiTrackTelemetry.SourceName);
    }

    private sealed class FailFirstQuery : DbCommandInterceptor
    {
        public int Failures { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Failures == 0)
            {
                Failures++;
                throw new NpgsqlException("Synthetic transient test failure.", new TimeoutException());
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class RejectCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic commit refusal.");
    }

    [Fact]
    public async Task CommitFailure_RollsBackWrites_AndRecordsOnlyFailure()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var options = new DbContextOptionsBuilder<RondiTrackDbContext>(factory.CreateDbContextOptions())
            .AddInterceptors(new RejectCommit()).Options;
        await using var db = new RondiTrackDbContext(options);
        using var capture = new Capture();
        var service = new PayoutService(db, factory.Services.GetRequiredService<RondiTrackTelemetry>());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayoutAsync(scenario.StokvelId, scenario.CycleId, 500m));
        capture.AssertMeasurement(RondiTrackTelemetry.PayoutsName, "outcome", "failure");
        var span = Assert.Single(capture.Activities, item => item.Source.Name == RondiTrackTelemetry.SourceName);
        AssertSafePayout(span, "failure");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        await using var verify = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.False(await verify.Payouts.AnyAsync(item => item.ContributionCycleId == scenario.CycleId));
        Assert.Equal("Open", (await verify.ContributionCycles.SingleAsync(item => item.Id == scenario.CycleId)).Status);
    }

    [Fact]
    public async Task TransientRetry_ProducesOneSpanAndOneSuccessfulMeasurement()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        var fault = new FailFirstQuery();
        var options = new DbContextOptionsBuilder<RondiTrackDbContext>(factory.CreateDbContextOptions())
            .UseNpgsql(config => config.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(10), null))
            .AddInterceptors(fault).Options;
        await using var db = new RondiTrackDbContext(options);
        using var capture = new Capture();
        var service = new PayoutService(db, factory.Services.GetRequiredService<RondiTrackTelemetry>());
        await service.CreatePayoutAsync(scenario.StokvelId, scenario.CycleId, 500m);
        Assert.Equal(1, fault.Failures);
        capture.AssertMeasurement(RondiTrackTelemetry.PayoutsName, "outcome", "success");
        AssertSafePayout(Assert.Single(capture.Activities, span => span.Source.Name == RondiTrackTelemetry.SourceName), "success");
        await using var verify = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await verify.Payouts.CountAsync(item => item.ContributionCycleId == scenario.CycleId));
    }
}
