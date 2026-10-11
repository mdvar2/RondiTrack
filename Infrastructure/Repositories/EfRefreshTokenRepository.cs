using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Infrastructure.Repositories;

public class EfRefreshTokenRepository(RondiTrackDbContext context) : IRefreshTokenRepository
{
    public async Task StartSessionAsync(Guid userId, string tokenHash, DateTime now, DateTime expires)
    {
        var family = new RefreshTokenFamily(userId, now);
        context.RefreshTokenFamilies.Add(family);
        context.RefreshTokens.Add(new RefreshToken(family.Id, tokenHash, now, expires));
        // Both inserts commit atomically through EF's implicit transaction.
        await context.SaveChangesAsync();
    }

    public async Task<RefreshRotationResult> RotateAsync(string tokenHash, string successorHash, DateTime now, DateTime expires)
    {
        var familyId = await context.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == tokenHash).Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync();
        if (familyId is null) return new(null, Guid.Empty, false);

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            // All family mutations take this same row lock, including logout.
            var family = await context.RefreshTokenFamilies
                .FromSqlInterpolated($"""SELECT * FROM "RefreshTokenFamilies" WHERE "Id" = {familyId.Value} FOR UPDATE""")
                .AsNoTracking().SingleAsync();
            var current = await context.RefreshTokens.AsNoTracking().SingleAsync(token => token.TokenHash == tokenHash);
            if (current.UsedAtUtc is not null)
            {
                await RevokeFamilyAsync(family.Id, now);
                await transaction.CommitAsync();
                // Commit revocation before returning a refusal; throwing would roll it back.
                return new RefreshRotationResult(null, family.Id, true);
            }
            if (family.RevokedAtUtc is not null || family.ExpiresAtUtc <= now || current.ExpiresAtUtc <= now)
                return new RefreshRotationResult(null, family.Id, false);

            var successor = new RefreshToken(family.Id, successorHash, now,
                expires < family.ExpiresAtUtc ? expires : family.ExpiresAtUtc);
            await context.RefreshTokens.Where(token => token.Id == current.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.UsedAtUtc, now)
                    .SetProperty(token => token.ReplacedByTokenId, successor.Id));
            // Insert directly through EF SQL to keep execution-strategy retries free of stale tracked inserts.
            await context.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "RefreshTokens" ("Id", "FamilyId", "TokenHash", "CreatedAtUtc", "ExpiresAtUtc") VALUES ({successor.Id}, {successor.FamilyId}, {successor.TokenHash}, {successor.CreatedAtUtc}, {successor.ExpiresAtUtc})""");
            await transaction.CommitAsync();
            return new RefreshRotationResult(family.UserId, family.Id, false);
        });
    }

    public async Task RevokeSessionAsync(string tokenHash, DateTime now)
    {
        var familyId = await context.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == tokenHash).Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync();
        if (familyId is null) return;
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.RefreshTokenFamilies
                .FromSqlInterpolated($"""SELECT * FROM "RefreshTokenFamilies" WHERE "Id" = {familyId.Value} FOR UPDATE""")
                .AsNoTracking().SingleAsync();
            await RevokeFamilyAsync(familyId.Value, now);
            await transaction.CommitAsync();
        });
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTime now) =>
        context.RefreshTokenFamilies.Where(family => family.Id == familyId && family.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(family => family.RevokedAtUtc, now));
}
