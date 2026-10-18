using Microsoft.Extensions.Options;

namespace RondiTrack.BackgroundJobs;

public sealed class HousekeepingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<HousekeepingOptions> options,
    TimeProvider clock,
    ILogger<HousekeepingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunTickAsync(stoppingToken);
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(options.Value.IntervalHours), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunTickAsync(stoppingToken);
    }

    private async Task RunTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<HousekeepingProcessor>()
                .RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Housekeeping failed; will retry on next tick.");
        }
    }
}
