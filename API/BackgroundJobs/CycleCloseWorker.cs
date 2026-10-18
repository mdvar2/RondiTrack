
using Microsoft.Extensions.Options;

namespace RondiTrack.BackgroundJobs;

public sealed class CycleCloseWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CycleCloseOptions> options,
    TimeProvider clock,
    ILogger<CycleCloseWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            options.Value.IntervalSeconds);

        if (interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "CycleClose:IntervalSeconds must be greater than zero.");
        }

        // Process overdue cycles immediately when the API starts.
        await RunTickAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunTickAsync(stoppingToken);
        }
    }

    private async Task RunTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Background services are singletons, so each run
            // must create its own scope for the scoped DbContext.
            using var scope = scopeFactory.CreateScope();

            var processor = scope.ServiceProvider
                .GetRequiredService<CycleCloseProcessor>();

            await processor.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Cycle-close worker failed. Processing will retry on the next tick.");
        }
    }
}
