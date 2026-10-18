
using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Observability;

namespace RondiTrack.BackgroundJobs;

public sealed class CycleCloseProcessor(
    RondiTrackDbContext db,
    TimeProvider clock,
    ILogger<CycleCloseProcessor> logger,
    RondiTrackTelemetry telemetry)
{
    private static readonly TimeZoneInfo Johannesburg =
        TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    public async Task RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                clock.GetUtcNow(),
                Johannesburg).DateTime);

        // Find stokvels with overdue, open cycles.
        var stokvelIds = await db.ContributionCycles
            .AsNoTracking()
            .Where(cycle =>
                cycle.Status == "Open" &&
                cycle.EndDate != null &&
                cycle.EndDate < localToday)
            .Select(cycle => cycle.StokvelId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var stokvelId in stokvelIds)
        {
            try
            {
                // A failed stokvel must not prevent others
                // from being processed.
                await ProcessStokvelAsync(
                    stokvelId,
                    localToday,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Clear tracked changes from a failed transaction.
                db.ChangeTracker.Clear();

                logger.LogError(
                    exception,
                    "Cycle-close processing failed. The next run will retry.");
            }
        }
    }

    private async Task ProcessStokvelAsync(
        Guid stokvelId,
        DateOnly localToday,
        CancellationToken cancellationToken)
    {
        // EF Core's retrying Npgsql execution strategy requires the entire
        // user-managed transaction to execute inside its retry delegate.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await ProcessStokvelTransactionAsync(stokvelId, localToday, cancellationToken);
        });
    }

    private async Task ProcessStokvelTransactionAsync(
        Guid stokvelId,
        DateOnly localToday,
        CancellationToken cancellationToken)
    {
        using var activity = telemetry.Activities.StartActivity("stokvel.cycle_close");
        long closedCount = 0;
        long arrearsCount = 0;
        // Each stokvel is processed in its own transaction.
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // PostgreSQL transaction-scoped advisory lock.
        // Concurrent workers processing the same stokvel
        // must wait for one another.
        var connection = db.Database.GetDbConnection();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction =
                db.Database.CurrentTransaction!.GetDbTransaction();

            command.CommandText =
                "SELECT pg_advisory_xact_lock(hashtextextended(@stokvelId, 0))";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "stokvelId";
            parameter.Value = stokvelId.ToString();

            command.Parameters.Add(parameter);

            await command.ExecuteScalarAsync(cancellationToken);
        }

        var stokvel = await db.Stokvels
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == stokvelId,
                cancellationToken);

        var members = await db.StokvelMembers
            .AsNoTracking()
            .Where(member => member.StokvelId == stokvelId)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);

        // Process one overdue cycle at a time.
        // Newly generated overdue cycles can also be
        // processed during the same run.
        while (true)
        {
            var cycle = await db.ContributionCycles
                .Where(item =>
                    item.StokvelId == stokvelId &&
                    item.Status == "Open" &&
                    item.EndDate != null &&
                    item.EndDate < localToday)
                .OrderBy(item => item.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (cycle is null)
                break;

            var contributions = await db.Contributions
                .AsNoTracking()
                .Where(contribution =>
                    contribution.ContributionCycleId == cycle.Id)
                .Select(contribution => new
                {
                    contribution.UserId,
                    contribution.Amount
                })
                .ToListAsync(cancellationToken);

            var paidByMember = contributions
                .GroupBy(item => item.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.Amount));

            foreach (var memberId in members)
            {
                var paid = paidByMember.GetValueOrDefault(memberId);

                var shortfall = Math.Max(
                    0m,
                    stokvel.ContributionAmount - paid);

                // Store financial shortfalls as integer cents.
                var cents = checked((long)decimal.Round(
                    shortfall * 100m,
                    0,
                    MidpointRounding.AwayFromZero));

                if (cents > 0)
                {
                    arrearsCount++;
                    db.ContributionArrears.Add(
                        new ContributionArrear(
                            stokvelId,
                            memberId,
                            cycle.Id,
                            cents));
                }
            }

            cycle.Close();
            closedCount++;

            // The next cycle begins the day after
            // the previous cycle ends.
            var nextStart = cycle.EndDate!.Value.AddDays(1);
            var nextEnd = nextStart.AddMonths(1).AddDays(-1);

            var nextPeriod = nextStart.ToString(
                "yyyy-MM",
                CultureInfo.InvariantCulture);

            var nextCycleExists =
                await db.ContributionCycles.AnyAsync(
                    candidate =>
                        candidate.StokvelId == stokvelId &&
                        candidate.Period == nextPeriod,
                    cancellationToken);

            if (!nextCycleExists)
            {
                db.ContributionCycles.Add(
                    new ContributionCycle(
                        stokvelId,
                        nextPeriod,
                        cycle.TargetAmount,
                        nextEnd));
            }

            // Save within the transaction so the next
            // iteration can see newly created cycles.
            await db.SaveChangesAsync(cancellationToken);
        }

        // Commit closes, arrears and next cycles together.
        await transaction.CommitAsync(cancellationToken);

        telemetry.CyclesClosed(closedCount);
        telemetry.ArrearsCreated(arrearsCount);

        // Avoid retaining tracked entities between stokvels.
        db.ChangeTracker.Clear();
    }
}
