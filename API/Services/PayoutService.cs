using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Entities;
using System.Diagnostics;
using RondiTrack.Observability;

namespace RondiTrack.Services;

public class PayoutService
{
    private readonly RondiTrackDbContext _dbContext;
    private readonly RondiTrackTelemetry _telemetry;

    public PayoutService(
        RondiTrackDbContext dbContext,
        RondiTrackTelemetry telemetry)
    {
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public async Task<PayoutResponse> CreatePayoutAsync(
        Guid stokvelId,
        Guid contributionCycleId,
        decimal amount)
    {
        using var activity = _telemetry.Activities.StartActivity("payout.process");
        try
        {
            var strategy =
                _dbContext.Database.CreateExecutionStrategy();

            var response = await strategy.ExecuteAsync(
                async () =>
                {
                    await using var transaction =
                        await _dbContext.Database
                            .BeginTransactionAsync();

                    try
                    {
                        var cycle =
                            await _dbContext.ContributionCycles
                                .FirstOrDefaultAsync(
                                    cycle =>
                                        cycle.Id ==
                                            contributionCycleId &&
                                        cycle.StokvelId ==
                                            stokvelId);

                        if (cycle is null)
                        {
                            throw new NotFoundException(
                                "Contribution cycle not found.");
                        }

                        if (cycle.Status != "Open")
                        {
                            throw new BusinessRuleException(
                                "Contribution cycle is not open for payout.");
                        }

                        if (amount <= 0)
                        {
                            throw new BusinessRuleException(
                                "Payout amount must be greater than zero.");
                        }

                        var paidRecipientUserIds =
                            await _dbContext.Payouts
                                .Where(
                                    payout =>
                                        payout.StokvelId ==
                                            stokvelId)
                                .Select(
                                    payout =>
                                        payout.RecipientUserId)
                                .ToListAsync();

                        var nextMember =
                            await InRotationOrder(
                                    _dbContext.StokvelMembers
                                        .Where(
                                            member =>
                                                member.StokvelId ==
                                                    stokvelId &&
                                                !paidRecipientUserIds.Contains(
                                                    member.UserId)))
                                .FirstOrDefaultAsync();

                        if (nextMember is null)
                        {
                            throw new BusinessRuleException(
                                "No eligible member is available for payout.");
                        }

                        var payout = new Payout(
                            stokvelId,
                            nextMember.UserId,
                            contributionCycleId,
                            amount);

                        await _dbContext.Payouts.AddAsync(
                            payout);

                        await _dbContext.SaveChangesAsync();

                        cycle.MarkPaidOut();

                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();

                        return PayoutResponse.FromEntity(
                            payout);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            // ExecuteAsync has completed successfully after CommitAsync, including any retries.
            activity?.SetTag("outcome", "success");
            _telemetry.PayoutCompleted(success: true);
            return response;
        }
        catch
        {
            activity?.SetTag("outcome", "failure");
            activity?.SetStatus(ActivityStatusCode.Error);
            _telemetry.PayoutCompleted(success: false);
            throw;
        }
    }

    // The rotation: members in the order they joined (user id breaks a tie).
    // CreatePayoutAsync pays the first unpaid member in this order, and
    // GetOrderAsync marks that same member Next, so the two cannot disagree.
    private static IQueryable<StokvelMember> InRotationOrder(
        IQueryable<StokvelMember> members) =>
        members
            .OrderBy(member => member.JoinedAtUtc)
            .ThenBy(member => member.UserId);

    // The payout order as seen from one cycle. Callers must have passed the
    // StokvelView policy first. A cycle that isn't open and had no payout
    // has no payout order: nobody was paid in it, and nobody can be.
    public async Task<PayoutOrderResponse> GetOrderAsync(
        Guid stokvelId,
        Guid contributionCycleId,
        CancellationToken cancellationToken = default)
    {
        var cycle =
            await _dbContext.ContributionCycles
                .AsNoTracking()
                .Where(
                    cycle =>
                        cycle.Id == contributionCycleId &&
                        cycle.StokvelId == stokvelId)
                .Select(cycle => new { cycle.Status, cycle.TargetAmount })
                .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                "Contribution cycle not found.");

        var payouts =
            await _dbContext.Payouts
                .AsNoTracking()
                .Where(payout => payout.StokvelId == stokvelId)
                .OrderBy(payout => payout.PaidAtUtc)
                .Select(
                    payout => new
                    {
                        payout.RecipientUserId,
                        payout.ContributionCycleId,
                        payout.Amount,
                        payout.PaidAtUtc
                    })
                .ToListAsync(cancellationToken);

        var paidInThisCycle = payouts.Any(
            payout => payout.ContributionCycleId == contributionCycleId);

        if (cycle.Status != "Open" && !paidInThisCycle)
        {
            return new PayoutOrderResponse(
                stokvelId,
                contributionCycleId,
                []);
        }

        var members =
            await InRotationOrder(
                    _dbContext.StokvelMembers
                        .AsNoTracking()
                        .Where(member => member.StokvelId == stokvelId))
                .Select(member => new { member.UserId, member.User.Name })
                .ToListAsync(cancellationToken);

        var paidByMember = payouts
            .GroupBy(payout => payout.RecipientUserId)
            .ToDictionary(group => group.Key, group => group.First());

        var potCents = ToCents(cycle.TargetAmount);
        var nextGiven = false;
        var items = new List<PayoutOrderEntryResponse>(members.Count);

        foreach (var (member, index) in members.Select((member, index) => (member, index)))
        {
            string status;
            long amountCents;
            DateTime? paidAtUtc = null;

            if (paidByMember.TryGetValue(member.UserId, out var payout))
            {
                status = PayoutStatuses.Paid;
                amountCents = ToCents(payout.Amount);
                paidAtUtc = payout.PaidAtUtc;
            }
            else
            {
                status = nextGiven ? PayoutStatuses.Waiting : PayoutStatuses.Next;
                nextGiven = true;
                amountCents = potCents;
            }

            items.Add(new PayoutOrderEntryResponse(
                index + 1,
                member.UserId,
                member.Name,
                amountCents,
                paidAtUtc,
                status));
        }

        return new PayoutOrderResponse(stokvelId, contributionCycleId, items);
    }

    private static long ToCents(decimal amount)
    {
        var cents = amount * 100m;
        if (cents != decimal.Truncate(cents))
            throw new InvalidOperationException("Money must have no more than two decimal places.");
        return checked((long)cents);
    }
}
