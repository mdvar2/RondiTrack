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
                            await _dbContext.StokvelMembers
                                .Where(
                                    member =>
                                        member.StokvelId ==
                                            stokvelId &&
                                        !paidRecipientUserIds.Contains(
                                            member.UserId))
                                .OrderBy(
                                    member =>
                                        member.JoinedAtUtc)
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
}
