using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Idempotency;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;
using RondiTrack.Observability;

namespace RondiTrack.Services;

public class ContributionService
{
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionCycleRepository _cycleRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStokvelMemberRepository _stokvelMemberRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly RondiTrackTelemetry _telemetry;
    private readonly RondiTrackDbContext? _db;

    public ContributionService(
        IContributionRepository contributionRepository,
        IContributionCycleRepository cycleRepository,
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository,
        IStokvelMemberRepository stokvelMemberRepository,
        IIdempotencyStore idempotencyStore,
        RondiTrackTelemetry telemetry,
        RondiTrackDbContext? db = null)
    {
        _contributionRepository = contributionRepository;
        _cycleRepository = cycleRepository;
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
        _stokvelMemberRepository = stokvelMemberRepository;
        _idempotencyStore = idempotencyStore;
        _telemetry = telemetry;
        _db = db;
    }

    // One transaction covers both the payment and the saved response.
    // The transaction-scoped PostgreSQL advisory lock serializes the same
    // key across different API instances, not merely within this process.
    public async Task<ContributionResponse> RecordContributionAsync(
        Guid stokvelId, Guid userId, string idempotencyKey,
        RecordContributionRequest request)
    {
        if (_db is null)
            return await RecordCoreAsync(stokvelId, userId, idempotencyKey, request);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new RequestValidationException("Idempotency-Key header is required.");

        if (idempotencyKey.Length > 256)
            throw new RequestValidationException("Idempotency-Key must be 256 characters or fewer.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            // Parameterized SQL: never interpolate the key into SQL text.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({idempotencyKey}, 0))");
            var response = await RecordCoreAsync(stokvelId, userId, idempotencyKey, request);
            await transaction.CommitAsync();
            return response;
        });
    }

    private async Task<ContributionResponse> RecordCoreAsync(
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

        var requestHash =
            CreateRequestHash(
                stokvelId,
                userId,
                request);

        var existingRecord =
            await _idempotencyStore.GetAsync(
                idempotencyKey);

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
            await _stokvelRepository.GetByIdReadOnlyAsync(
                stokvelId);

        if (stokvel is null)
        {
            throw new NotFoundException(
                "Stokvel not found.");
        }

        var user =
            await _userRepository.GetByIdReadOnlyAsync(
                userId);

        if (user is null)
        {
            throw new NotFoundException(
                "User not found.");
        }

        var membershipExists =
            await _stokvelMemberRepository.ExistsAsync(
                stokvelId,
                userId);

        if (!membershipExists)
        {
            throw new BusinessRuleException(
                "User is not a member of this stokvel.");
        }

        var cycle =
            await _cycleRepository.GetByIdReadOnlyAsync(
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

        if (cycle.Status != "Open")
        {
            throw new BusinessRuleException(
                "This contribution cycle is no longer open.");
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

        var contribution =
            new Contribution(
                stokvelId,
                userId,
                request.ContributionCycleId,
                request.AmountInCents / 100m);

        await _contributionRepository.AddAsync(
            contribution);
        _telemetry.ContributionRecorded();

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
            $"{stokvelId}|{userId}|{request.AmountInCents}|" +
            $"{request.ContributionCycleId}";

        var bytes =
            Encoding.UTF8.GetBytes(
                requestData);

        var hash =
            SHA256.HashData(
                bytes);

        return Convert.ToHexString(
            hash);
    }
}
