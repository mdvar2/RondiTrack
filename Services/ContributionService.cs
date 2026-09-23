using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Idempotency;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Services;

public class ContributionService
{
    private readonly IContributionRepository _contributionRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;
    private readonly IIdempotencyStore _idempotencyStore;

    public ContributionService(
        IContributionRepository contributionRepository,
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository,
        IIdempotencyStore idempotencyStore)
    {
        _contributionRepository = contributionRepository;
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<ContributionResult> RecordContributionAsync(
        Guid stokvelId,
        Guid userId,
        string idempotencyKey,
        RecordContributionRequest request)
    {
        var requestHash = CreateRequestHash(
            stokvelId,
            userId,
            request);

        // 1. Check whether this Idempotency-Key was already used.
        var existingRecord =
            await _idempotencyStore.GetAsync(idempotencyKey);

        if (existingRecord is not null)
        {
            // Same key but different request = conflict.
            if (existingRecord.RequestHash != requestHash)
            {
                return new ContributionResult(
                    ContributionOutcome.IdempotencyKeyConflict);
            }

            // Same key and same request = return original result.
            return new ContributionResult(
                ContributionOutcome.Replayed,
                existingRecord.ResponseBody,
                existingRecord.ResponseStatus);
        }

        // 2. Check that the stokvel exists.
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
        {
            return new ContributionResult(
                ContributionOutcome.StokvelNotFound);
        }

        // 3. Check that the user exists.
        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user is null)
        {
            return new ContributionResult(
                ContributionOutcome.UserNotFound);
        }

        // 4. Check that the user belongs to this stokvel.
        var isMember =
            stokvel.Members.Any(member => member.Id == userId);

        if (!isMember)
        {
            return new ContributionResult(
                ContributionOutcome.NotMember);
        }

        // 5. Check that the amount is valid.
        if (request.Amount <= 0)
        {
            return new ContributionResult(
                ContributionOutcome.InvalidAmount);
        }

        // 6. Check that the cycle follows YYYY-MM.
        if (!IsValidCycle(request.Cycle))
        {
            return new ContributionResult(
                ContributionOutcome.InvalidCycle);
        }

        // 7. Prevent the same member from paying
        //    for the same contribution cycle twice.
        var existingContribution =
            await _contributionRepository.GetByMemberAndCycleAsync(
                stokvelId,
                userId,
                request.Cycle);

        if (existingContribution is not null)
        {
            return new ContributionResult(
                ContributionOutcome.DuplicateContribution);
        }

        // 8. Create the contribution.
        var contribution = new Contribution(
            stokvelId,
            userId,
            request.Amount,
            request.Cycle);

        await _contributionRepository.AddAsync(contribution);

        // Convert the domain entity into an HTTP response DTO.
        var response =
            ContributionResponse.FromEntity(contribution);

        // 9. Store the successful result against the
        //    Idempotency-Key so retries are safe.
        var idempotencyRecord = new IdempotencyRecord(
            idempotencyKey,
            requestHash,
            StatusCodes.Status201Created,
            response);

        await _idempotencyStore.SaveAsync(idempotencyRecord);

        return new ContributionResult(
            ContributionOutcome.Created,
            response,
            StatusCodes.Status201Created);
    }

    private static bool IsValidCycle(string cycle)
    {
        if (string.IsNullOrWhiteSpace(cycle))
            return false;

        return DateTime.TryParseExact(
            cycle,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string CreateRequestHash(
        Guid stokvelId,
        Guid userId,
        RecordContributionRequest request)
    {
        var requestData =
            $"{stokvelId}|{userId}|{request.Amount}|{request.Cycle}";

        var bytes = Encoding.UTF8.GetBytes(requestData);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
