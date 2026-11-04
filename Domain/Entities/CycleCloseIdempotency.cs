namespace RondiTrack.Domain.Entities;

public sealed class CycleCloseIdempotency
{
    public string Key { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public string ResponseJson { get; private set; } = null!;
    public int ResponseStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CycleCloseIdempotency() { }

    public CycleCloseIdempotency(string key, string requestHash, string responseJson, int responseStatus)
    {
        Key = key;
        RequestHash = requestHash;
        ResponseJson = responseJson;
        ResponseStatus = responseStatus;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
