using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Exceptions;
using RondiTrack.DTOs.ContributionCycles;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Authorize(Policy = Policies.SignedIn)]
[ApiController]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/close")]
public sealed class CycleCloseController(
    RondiTrackDbContext db,
    StokvelAuthorization authorization,
    CycleCloseCalculationService calculator) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<ActionResult<CycleClosePreviewResponse>> Preview(Guid stokvelId, Guid cycleId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync(User, stokvelId, Policies.StokvelManage);
        var preview = await calculator.CalculateAsync(stokvelId, cycleId, cancellationToken)
            ?? throw new NotFoundException("Contribution cycle not found.");
        Response.Headers.ETag = $"\"{preview.Version}\"";
        return Ok(preview);
    }

    [HttpPost]
    public async Task<ActionResult<CloseCycleResponse>> Close(
        Guid stokvelId,
        Guid cycleId,
        CloseCycleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync(User, stokvelId, Policies.StokvelManage);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 256)
            throw new RequestValidationException("A valid Idempotency-Key header is required.");
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{stokvelId}|{cycleId}|{request.Version}")));
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended(CAST({stokvelId} AS text), 0))",
                cancellationToken);
            var existing = await db.CycleCloseIdempotencies.SingleOrDefaultAsync(x => x.Key == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestHash != requestHash) throw new BusinessRuleException("Idempotency-Key was already used with a different request.");
                return new ContentResult { Content = existing.ResponseJson, ContentType = "application/json", StatusCode = existing.ResponseStatus };
            }
            var preview = await calculator.CalculateAsync(stokvelId, cycleId, cancellationToken)
                ?? throw new NotFoundException("Contribution cycle not found.");
            if (preview.Version != request.Version)
                throw new BusinessRuleException("The close preview is stale. Fetch a new preview before confirming.");
            var cycle = await db.ContributionCycles.SingleOrDefaultAsync(x => x.Id == cycleId && x.StokvelId == stokvelId, cancellationToken)
                ?? throw new NotFoundException("Contribution cycle not found.");
            if (cycle.Status != "Open") throw new BusinessRuleException("This contribution cycle is already closed.");
            foreach (var arrear in preview.Arrears)
                db.ContributionArrears.Add(new Domain.Entities.ContributionArrear(stokvelId, arrear.UserId, cycleId, arrear.AmountCents));
            cycle.Close();
            var nextStart = (cycle.EndDate ?? preview.AsOfDate).AddDays(1);
            var nextPeriod = nextStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (!await db.ContributionCycles.AnyAsync(x => x.StokvelId == stokvelId && x.Period == nextPeriod, cancellationToken))
                db.ContributionCycles.Add(new Domain.Entities.ContributionCycle(stokvelId, nextPeriod, cycle.TargetAmount, nextStart.AddMonths(1).AddDays(-1)));
            await db.SaveChangesAsync(cancellationToken);
            var result = new CloseCycleResponse(cycleId, cycle.Period, preview.AsOfDate, preview.Arrears, preview.TotalCents);
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            db.CycleCloseIdempotencies.Add(new Domain.Entities.CycleCloseIdempotency(idempotencyKey, requestHash, json, StatusCodes.Status200OK));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (ActionResult<CloseCycleResponse>)Ok(result);
        });
    }
}
