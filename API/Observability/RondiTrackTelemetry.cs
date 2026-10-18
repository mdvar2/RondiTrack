using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RondiTrack.Observability;

public sealed class RondiTrackTelemetry : IDisposable
{
    public const string SourceName = "RondiTrack.Business";
    public const string MeterName = "RondiTrack.Business";
    public const string ContributionsName = "ronditrack.contributions.recorded";
    public const string PayoutsName = "ronditrack.payouts.processed";
    public const string RefusalsName = "ronditrack.authorization.refusals";

    public ActivitySource Activities { get; } = new(SourceName);
    private readonly Counter<long> _contributions;
    private readonly Counter<long> _payouts;
    private readonly Counter<long> _refusals;
    private readonly Counter<long> _closedCycles;
    private readonly Counter<long> _arrearsCreated;

    public RondiTrackTelemetry(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _contributions = meter.CreateCounter<long>(ContributionsName, "{contribution}", "New contributions persisted.");
        _payouts = meter.CreateCounter<long>(PayoutsName, "{operation}", "Completed payout operations by outcome.");
        _refusals = meter.CreateCounter<long>(RefusalsName, "{refusal}", "Stokvel authorization refusals by result.");
        _closedCycles = meter.CreateCounter<long>("ronditrack.cycles.closed");
        _arrearsCreated = meter.CreateCounter<long>("ronditrack.arrears.created");
    }

    public void ContributionRecorded() => _contributions.Add(1);
    public void PayoutCompleted(bool success) =>
        _payouts.Add(1, new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));
    public void AuthorizationRefused(bool notFound) =>
        _refusals.Add(1, new KeyValuePair<string, object?>("result", notFound ? "not_found" : "forbidden"));

    public void CyclesClosed(long count) => _closedCycles.Add(count);
    public void ArrearsCreated(long count) => _arrearsCreated.Add(count);

    // IMeterFactory owns the meter; DI owns this singleton and its activity source.
    public void Dispose() => Activities.Dispose();
}
