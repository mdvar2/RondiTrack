using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Idempotency;

public sealed class PostgresIdempotencyStore(RondiTrackDbContext db, TimeProvider clock) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> GetAsync(string key)
    {
        var saved = await db.PersistedIdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == key);
        if (saved is null) return null;
        var body = JsonSerializer.Deserialize<RondiTrack.DTOs.Contributions.ContributionResponse>(saved.ResponseJson)
            ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
        return new IdempotencyRecord(saved.Key, saved.RequestHash, saved.ResponseStatus, body);
    }

    public async Task SaveAsync(IdempotencyRecord record)
    {
        db.PersistedIdempotencyRecords.Add(new PersistedIdempotencyRecord
        {
            Key = record.Key,
            RequestHash = record.RequestHash,
            ResponseStatus = record.ResponseStatus,
            ResponseJson = JsonSerializer.Serialize(record.ResponseBody),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync();
    }
}
