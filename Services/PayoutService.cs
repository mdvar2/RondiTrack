using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Exceptions;
using RondiTrack.Models;

namespace RondiTrack.Services;

public class PayoutService
{
    private readonly RondiTrackDbContext _dbContext;

    public PayoutService(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayoutResponse> CreatePayoutAsync(
        Guid stokvelId,
        Guid contributionCycleId,
        decimal amount)
    {
        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
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

                    var paidMemberIds =
                        await _dbContext.Payouts
                            .Where(
                                payout =>
                                    payout.StokvelId ==
                                        stokvelId)
                            .Select(
                                payout =>
                                    payout.StokvelMemberId)
                            .ToListAsync();

                    var nextMember =
                        await _dbContext.StokvelMembers
                            .Where(
                                member =>
                                    member.StokvelId ==
                                        stokvelId &&
                                    !paidMemberIds.Contains(
                                        member.Id))
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
                        nextMember.Id,
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
    }
}