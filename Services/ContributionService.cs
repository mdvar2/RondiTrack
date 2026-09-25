using System.Security.Cryptography;
using System.Text;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Exceptions;
using RondiTrack.Idempotency;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Services;

public class ContributionService
{
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionCycleRepository _cycleRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;
    private readonly IIdempotencyStore _idempotencyStore;

    public ContributionService(
        IContributionRepository contributionRepository,
        IContributionCycleRepository cycleRepository,
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository,
        IIdempotencyStore idempotencyStore)
    {
        _contributionRepository = contributionRepository;
        _cycleRepository = cycleRepository;
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<ContributionResponse> RecordContributionAsync(
        Guid stokvelId,
        Guid userId,
        string idempotencyKey,
        RecordContributionRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new RequestValidationException(
                "Idempotency-Key header is required.");
        }

        var requestHash = CreateRequestHash(
            stokvelId,
            userId,
            request);

        var existingRecord =
            await _idempotencyStore.GetAsync(idempotencyKey);

        if (existingRecord is not null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException(
                    "Idempotency-Key was already used with a different request.");
            }

            return existingRecord.ResponseBody;
        }

        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new NotFoundException("User not found.");

        var isMember =
            stokvel.Members.Any(
                member => member.Id == userId);

        if (!isMember)
        {
            throw new BusinessRuleException(
                "User is not a member of this stokvel.");
        }

        var cycle =
            await _cycleRepository.GetByIdAsync(
                request.ContributionCycleId);

        if (cycle is null)
        {
            throw new NotFoundException(
                "Contribution cycle not found.");
        }

        if (cycle.StokvelId != stokvelId)
        {
            throw new BusinessRuleException(
                "Contribution cycle does not belong to this stokvel.");
        }

        var existingContribution =
            await _contributionRepository
                .GetByMemberAndCycleAsync(
                    stokvelId,
                    userId,
                    request.ContributionCycleId);

        if (existingContribution is not null)
        {
            throw new BusinessRuleException(
                "A contribution already exists for this member and cycle.");
        }

        var contribution = new Contribution(
            stokvelId,
            userId,
            request.ContributionCycleId,
            request.Amount);

        await _contributionRepository.AddAsync(
            contribution);

        var response =
            ContributionResponse.FromEntity(
                contribution);

        var idempotencyRecord =
            new IdempotencyRecord(
                idempotencyKey,
                requestHash,
                StatusCodes.Status201Created,
                response);

        await _idempotencyStore.SaveAsync(
            idempotencyRecord);

        return response;
    }

    private static string CreateRequestHash(
        Guid stokvelId,
        Guid userId,
        RecordContributionRequest request)
    {
        var requestData =
            $"{stokvelId}|{userId}|{request.Amount}|" +
            $"{request.ContributionCycleId}";

        var bytes =
            Encoding.UTF8.GetBytes(requestData);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}