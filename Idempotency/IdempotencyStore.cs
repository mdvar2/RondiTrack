namespace RondiTrack.Idempotency;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _records = new();

    public Task<IdempotencyRecord?> GetAsync(string key)
    {
        _records.TryGetValue(key, out var record);

        return Task.FromResult(record);
    }

    public Task SaveAsync(IdempotencyRecord record)
    {
        _records[record.Key] = record;

        return Task.CompletedTask;
    }
}